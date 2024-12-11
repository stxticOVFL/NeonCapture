using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using NeonLite.Modules;
using UnityEngine;
using NC = NeonCapture.NeonCapture;

#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed

namespace NeonCapture
{
    [HarmonyPatch]
    public class Hooks : IModule
    {
#pragma warning disable CS0414
        const bool priority = true;
        const bool active = true;
        static void Setup() { }
        static void Activate(bool _) { }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(Game), "PlayLevel", typeof(LevelData), typeof(bool), typeof(bool))]
        private static void PlayLevel(LevelData newLevel, bool fromRestart)
        {
            if (!NC.manager || !NC.manager.ready || !newLevel || !NC.Settings.EnableRecord.Value) return;
            if (newLevel.type == LevelData.LevelType.None || newLevel.type == LevelData.LevelType.Hub)
            {
                if (NC.manager.recording)
                    NC.manager.DiscardVideo();
            }
            else
            {
                if (NC.manager.recording)
                {
                    if (fromRestart)
                    {
                        if (NC.Settings.OnRestart.Value || NC.manager.queuedPath != null)
                        {
                            if (NC.manager.usedBonus == null)
                            {
                                NC.manager.usedBonus = NC.Settings.ManualType.Value;
                                NC.manager.time = Singleton<Game>.Instance.GetCurrentLevelTimerMicroseconds();
                            }
                            NC.manager.SaveVideo();
                        }
                        else
                            NC.manager.DiscardVideo();
                    }
                    else
                        NC.manager.DiscardVideo();
                }
                NC.manager.StartRecording();
            }
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(Game), "PlayLevel", typeof(string), typeof(bool), typeof(Action))]
        private static void PlayLevel(string newLevelID) => PlayLevel(Singleton<Game>.Instance.GetGameData().GetLevelData(newLevelID), false);

        [HarmonyPostfix]
        [HarmonyPatch(typeof(LevelGate), "OnTriggerStay")]
        private static void OnTriggerStay(ref LevelGate __instance)
        {
            if (!NC.manager || !NC.manager.ready) return;
            NC.manager.time = Singleton<Game>.Instance.GetCurrentLevelTimerMicroseconds();
            if (__instance.Unlocked)
                return;
            if (NC.Settings.OnDNF.Value && NC.manager.recording && NC.manager.queuedPath == null)
                NC.manager.QueueVideo(NC.Settings.DNFType.Value);
        }

        static bool LevelCompleteOverride()
        {
            if (!NC.manager || !NC.manager.ready || !NC.manager.recording || LevelRush.IsLevelRush()) return LevelRush.IsLevelRush();

            Game game = Singleton<Game>.Instance;
            var stats = GameDataManager.levelStats[game.GetCurrentLevel().levelID];

            NC.manager.time = stats._timeLastMicroseconds;
            if (NC.Settings.NonPBs.Value || stats.IsNewBest())
                NC.manager.QueueVideo(stats.IsNewBest() ? NC.Settings.PBType.Value : NC.Settings.NoPBType.Value);

            return false;
        }

        [HarmonyTranspiler]
        [HarmonyPatch(typeof(MenuScreenResults), "LevelCompleteRoutine", MethodType.Enumerator)]
        static IEnumerable<CodeInstruction> LevelCompletePatch(IEnumerable<CodeInstruction> instructions)
        {
            int count = 0;
            var method = AccessTools.Method(typeof(LevelRush), "IsLevelRush");
            bool patched = false;
            var changed = instructions.Select((code, index) =>
            {
                // count to 13 calls
                if (code.Calls(method))
                {
                    patched = ++count == 13;
                    // patch the level rush function to call elsewhere
                    if (patched)
                        return CodeInstruction.Call(() => LevelCompleteOverride()).MoveLabelsFrom(code);
                }
                return code;
            });

            return changed;
        }

        internal static bool showWaiting;
        static public bool OnLevelLoad(LevelData level)
        {

            if (!level || level.type == LevelData.LevelType.Hub || !NC.manager || !NC.manager.ready || !NC.Settings.EnableRecord.Value || !NC.Settings.StallLoad.Value) return true;
            if (showWaiting)
                NC.manager.SetStatus("Waiting for OBS...");
            return NC.manager.recording;
        }
    }
}
