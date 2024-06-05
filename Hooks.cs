using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using UnityEngine;
using NC = NeonCapture.NeonCapture;

#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed

namespace NeonCapture
{
    [HarmonyPatch]
    public class Hooks
    {
        [HarmonyPrefix]
        [HarmonyPatch(typeof(Game), "PlayLevel", typeof(LevelData), typeof(bool), typeof(bool))]
        private static void PlayLevel(LevelData newLevel, bool fromArchive, bool fromRestart)
        {
            if (!NC.manager || !NC.manager.ready || !newLevel) return;

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
        private static void PlayLevel(string newLevelID, bool fromArchive) => PlayLevel(Singleton<Game>.Instance.GetGameData().GetLevelData(newLevelID), fromArchive, false);

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

        static public bool WaitingForRecord()
        {
            if (!NC.manager || !NC.manager.ready || !NC.Settings.StallLoad.Value) return false;
            return !NC.manager.recording;
        }

        [HarmonyTranspiler]
        [HarmonyPatch(typeof(Game), "LevelSetupRoutine", MethodType.Enumerator)]
        static IEnumerable<CodeInstruction> LevelSetupPatch(IEnumerable<CodeInstruction> instructions)
        {
            int index = 0;
            var ghostLoading = AccessTools.PropertyGetter(typeof(GhostPlayback), "IsLoadingData");
            CodeInstruction branch = null;
            foreach (var (code, i) in instructions.Select((value, i) => (value, i)))
            {
                // find first (and only) GhostPlayback.IsLoadingData
                if (code.Calls(ghostLoading))
                {
                    index = i + 1; // keep track of the index directly *after* (it's a branch)
                    break;
                }
            }

            foreach (var (code, i) in instructions.Select((value, i) => (value, i)))
            {
                if (i == index) // if we're that index we kept trakcof earlier
                    branch = code; // remember it, we'll be borrowing it on:
                else if (branch != null) // the very next instruction
                {
                    yield return CodeInstruction.Call(() => WaitingForRecord()); // call waitforrecord, pushing it directly on the stack
                    yield return branch; // same branch from earlier, branch if true to spinning
                    branch = null;
                }
                yield return code;
            }
        }
    }
}
