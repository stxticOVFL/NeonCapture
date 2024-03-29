using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using NC = NeonCapture.NeonCapture;

namespace NeonCapture
{
    [HarmonyPatch]
    public class Hooks
    {
        [HarmonyPrefix]
        [HarmonyPatch(typeof(Game), "PlayLevel", typeof(LevelData), typeof(bool), typeof(bool))]
        private static void PlayLevel(LevelData newLevel, bool fromRestart)
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
                            NC.manager.usedBonus ??= NC.Settings.ManualType.Value;
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
                if (code.Calls(method))
                {
                    patched = ++count == 13;
                    if (patched)
                        return CodeInstruction.Call(() => LevelCompleteOverride()).MoveLabelsFrom(code);
                }
                return code;
            });

            //throw new Exception($"HAIII {log}");
            return changed;
        }
    }
}
