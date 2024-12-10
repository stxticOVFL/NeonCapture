using MelonLoader;
using UniverseLib.Input;
using UnityEngine;
using NeonCapture.Objects;
using Steamworks;

namespace NeonCapture
{
    public class NeonCapture : MelonMod
    {
        public static new HarmonyLib.Harmony Harmony { get; private set; }
        public static CaptureManager manager = null;
        public static bool timelinesAble = false;
        public override void OnLateInitializeMelon()
        {
            Harmony = new("NeonCapture");
            Settings.Register();
            if (Settings.Enabled.Value)
            {
                manager = new GameObject("NeonCapture", typeof(CaptureManager)).GetComponent<CaptureManager>();
                Object.DontDestroyOnLoad(manager.gameObject);
            }

            timelinesAble = SteamNatives.Setup();
            if (timelinesAble)
            {
                SteamNatives.SetLogCB(LoggerInstance.Msg);
                if (Settings.UseSteam.Value)
                {
                    LoggerInstance.Msg($"Steam timelines initialized!");
                    SteamTimeline.SetTimelineGameMode(ETimelineGameMode.k_ETimelineGameMode_Menus);
                }
            }
            else if (Settings.UseSteam.Value)
                LoggerInstance.Msg("Steam timelines failed to initialize.");
        }

        public override void OnUpdate()
        {
            if (!manager)
                return;
            if (InputManager.GetKeyDown(Settings.EarlyFinish.Value))
            {
                if (manager.usedBonus == null)
                {
                    manager.usedBonus = Settings.ManualType.Value;
                    manager.usedSteamIcon = "steam_marker";
                    manager.time = Singleton<Game>.Instance.GetCurrentLevelTimerMicroseconds();
                }
                manager.SaveVideo();
            }
        }

        public static class Settings
        {
            public static MelonPreferences_Category MainCategory;

            public static MelonPreferences_Entry<bool> Enabled;
            public static MelonPreferences_Entry<bool> UseSteam;
            public static MelonPreferences_Entry<bool> EnableRecord;
            public static MelonPreferences_Entry<bool> StallLoad;
            public static MelonPreferences_Entry<int> Port;
            public static MelonPreferences_Entry<string> Password;
            public static MelonPreferences_Entry<bool> GhostDir;
            public static MelonPreferences_Entry<string> OutputFile;

            public static MelonPreferences_Entry<string> PBType;
            public static MelonPreferences_Entry<string> DNFType;
            public static MelonPreferences_Entry<string> NoPBType;
            public static MelonPreferences_Entry<string> ManualType;

            public static MelonPreferences_Entry<bool> OnDNF;
            public static MelonPreferences_Entry<bool> OnRestart;
            public static MelonPreferences_Entry<bool> NonPBs;
            public static MelonPreferences_Entry<int> AutoSeconds;
            public static MelonPreferences_Entry<KeyCode> EarlyFinish;
            public static MelonPreferences_Entry<bool> StartAlert;
            public static MelonPreferences_Entry<bool> AutoAlert;


            public static void Register()
            {
                MainCategory = MelonPreferences.CreateCategory("NeonCapture");

                Enabled = MainCategory.CreateEntry("Enabled", false, description: "REQUIRES RESTART!");
                UseSteam = MainCategory.CreateEntry("Use Steam Timelines", false, description: "REQUIRES RESTART!\nWhether or not to use the Steam Timelines automatic recording system instead of OBS.\nWhen this setting is enabled, *all OBS related settings will not work.*");
                EnableRecord = MainCategory.CreateEntry("Enable Recording", true, description: "Only affects OBS.");
                StallLoad = MainCategory.CreateEntry("Stall Loading", true, description: "Wait to load the level until recording has started. This ensures everything's captured.\nDisable this if your load times are unreasonably long.\nOnly affects OBS.");
                Port = MainCategory.CreateEntry("OBS Port", 4455);
                Password = MainCategory.CreateEntry("OBS Password", "", description: "Your OBS Websockets password, if required. Be careful about sharing this!");
                AutoSeconds = MainCategory.CreateEntry("Seconds until Auto-clip", 3, description: "How many seconds to wait after leaderboard screen/DNF to automatically clip the recording.\nSet to -1 to disable.");
                StartAlert = MainCategory.CreateEntry("Start Recording Notification", false);
                AutoAlert = MainCategory.CreateEntry("Auto-clip Notification", true);
                OnDNF = MainCategory.CreateEntry("Clip recording on DNF", true);
                NonPBs = MainCategory.CreateEntry("Clip recording on non-PBs", false);
                OnRestart = MainCategory.CreateEntry("Clip recording on all restarts", false);

                OutputFile = MainCategory.CreateEntry("Output File Format", "Neon White/%L/%B/%d - %T", description: """
                What to name the output file when using OBS. The file extension will be automatically added. The format is as follows:
                %% - Actual %
                %l - Unlocalized level name, e.g. GRID_PORT
                %L - Localized level name, e.g. Glass Port
                %t - Pure time in microseconds, e.g. 80030999
                %T - Time in just seconds + milliseconds, e.g. 80.030
                %D - Formatted date/time, e.g. 2024-03-31 21-50-17
                %d - Unformatted date/time, e.g. 20240331215017 (good for sorting!)
                %B - The type, as listed by the settings below
                """);

                PBType = MainCategory.CreateEntry("PB Format String", "PBs");
                DNFType = MainCategory.CreateEntry("DNF Format String", "DNFs");
                NoPBType = MainCategory.CreateEntry("Non-PB Format String", "Non-PBs");
                ManualType = MainCategory.CreateEntry("Manual Format String", "Manual");

                GhostDir = MainCategory.CreateEntry("Use Ghost Directory", false, description: "Whether or not to place new recordings in their respective ghost directories or to keep the current video directory.\nOnly in use when using OBS.");
                EarlyFinish = MainCategory.CreateEntry("Early Finish Key", KeyCode.Semicolon, description: "The key to press to either save before the auto-save, or save the current run despite never touching the goal.\nOnly in use when using OBS.");
            }
        }
    }
}
