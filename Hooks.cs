using HarmonyLib;
using NeonLite.Modules;
using UnityEngine;
using NC = NeonCapture.NeonCapture;
using EnsureTimer = NeonLite.Modules.Optimization.EnsureTimer;
using System.Reflection;
using NeonCapture.Objects;

namespace NeonCapture
{
    //[HarmonyPatch]
    public class Hooks : IModule
    {
#pragma warning disable CS0414
        const bool priority = true;
        const bool active = true;
        static void Setup() { }
        static void Activate(bool _)
        {
            Patching.PerformHarmonyPatches(typeof(Hooks));
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(Game), "PlayLevel", typeof(LevelData), typeof(bool), typeof(bool))]
        private static void PlayLevel(LevelData newLevel, bool fromRestart)
        {
            if (!NC.handler || !NC.handler.ready) return;

            if (!newLevel || newLevel.type == LevelData.LevelType.None || newLevel.type == LevelData.LevelType.Hub)
            {
                if (NC.handler.CheckStatus(Handler.RecordStatus.Starting))
                    NC.handler.DiscardVideo(false); // discard and fully stop recording
                return;
            }

            if (NC.handler.CheckStatus(Handler.RecordStatus.Recording))
            {
                if (fromRestart)
                {
                    if (NC.Settings.OnRestart.Value || (NC.handler.queuedPath != null && !NC.handler.dontSave))
                    {
                        if (NC.handler.usedBonus == null)
                        {
                            // if no type set as manual type
                            NC.handler.usedBonus = NC.Settings.ManualType.Value;
                            NC.handler.time = Singleton<Game>.Instance.GetCurrentLevelTimerMicroseconds();
                        }
                        NC.handler.SaveVideo();
                    }
                    else // if we don't have anything queued and we don't care about all restarts, discard
                        NC.handler.DiscardVideo();
                }
                else // if not marked as fromrestart, discard
                    NC.handler.DiscardVideo();
            }

            // queue up for recording
            NC.handler.StartRecording();
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(Game), "QuitToTitle")]
        static void PreTitle()
        {
            if (!NC.handler || !NC.handler.ready) return;

            if (NC.handler.CheckStatus(Handler.RecordStatus.Starting))
                NC.handler.DiscardVideo(false); // discard and fully stop recording
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(Game), "PlayLevel", typeof(string), typeof(bool), typeof(Action))]
        private static void PlayLevel(string newLevelID) => PlayLevel(Singleton<Game>.Instance.GetGameData().GetLevelData(newLevelID), false);


        // todo: oops
        static readonly FieldInfo cOverride = NeonLite.Helpers.Field(typeof(EnsureTimer), "cOverride");
        [HarmonyPostfix]
        [HarmonyPatch(typeof(LevelGate), "OnTriggerStay")]
        private static void OnTriggerStay(ref LevelGate __instance)
        {
            if (__instance.Unlocked)
                return;

            if (!NC.handler || !NC.handler.ready || !NC.handler.CheckStatus(Handler.RecordStatus.Recording)) return;

            var c = (Collider)cOverride.GetValue(null);
            NC.handler.time = EnsureTimer.CalculateOffset(c ?? __instance.GetComponentInChildren<MeshCollider>());

            if (NC.handler.queuedPath == null)
            {
                bool save = NC.Settings.OnDNF.Value;
                Game game = Singleton<Game>.Instance;
                var stats = GameDataManager.levelStats[game.GetCurrentLevel().levelID];

                if (!save && NC.Settings.OnPBDNF.Value && NC.handler.time < stats._timeBestMicroseconds)
                    save = true;

                NC.handler.QueueVideo(NC.Settings.DNFType.Value, !save);
            }
        }

        static bool LevelCompleteOverride()
        {
            if (LevelRush.IsLevelRush() || !NC.handler || !NC.handler.ready || !NC.handler.CheckStatus(Handler.RecordStatus.Recording)) return LevelRush.IsLevelRush();

            Game game = Singleton<Game>.Instance;
            var stats = GameDataManager.levelStats[game.GetCurrentLevel().levelID];

            NC.handler.time = stats._timeLastMicroseconds;

            // this happens no matter what queued path is
            NC.handler.QueueVideo(stats.IsNewBest() ? NC.Settings.PBType.Value : NC.Settings.NoPBType.Value, !NC.Settings.NonPBs.Value && !stats.IsNewBest());

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

            if (!level || level.type == LevelData.LevelType.Hub || !NC.handler || !NC.handler.ready || !NC.Settings.StallLoad.Value) return true;
            if (showWaiting)
                StatusText.i.SetStatus("Waiting for OBS...");
            return NC.handler.CheckStatus(Handler.RecordStatus.Recording);
        }
    }
}
