using MelonLoader;
using NeonCapture.Objects;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using UnityEngine;
using UniverseLib.Input;

namespace NeonCapture
{
    public class NeonCapture : MelonMod
    {
        internal static NeonCapture instance;

#if DEBUG
        internal static bool DEBUG { get { return Settings.Debug.Value; } }
#else
        internal const bool DEBUG = false;
#endif
        public static MelonLogger.Instance Log => instance.LoggerInstance;


        internal static Handler handler = null;

        public override void OnInitializeMelon()
        {
            instance = this;

            Settings.Register();
            NeonLite.NeonLite.LoadModules(MelonAssembly);
        }

        public override void OnLateInitializeMelon()
        {
            handler = new GameObject("NeonCapture", typeof(Handler), typeof(FileIO), typeof(StatusText)).GetComponent<Handler>();
            handler.gameObject.AddComponent<Scheduler>();
            Object.DontDestroyOnLoad(handler.gameObject);
        }

        public override void OnUpdate()
        {
            if (!handler)
                return;
            if (InputManager.GetKeyDown(Settings.EarlyFinish.Value))
            {
                if (!handler.ready)
                {
                    StatusText.i.SetStatus("Looking for OBS...");
                    Object.Destroy(Awaiter.i);
                }
                else if (Singleton<Game>.Instance.GetCurrentLevelType() != LevelData.LevelType.Hub) {
                    if (handler.usedBonus == null)
                    {
                        handler.usedBonus ??= Settings.ManualType.Value;
                        handler.time = Singleton<Game>.Instance.GetCurrentLevelTimerMicroseconds();
                    }
                    handler.SaveVideo();
                }
            }
        }

        public static class Settings
        {
            public static readonly string h = "NeonCapture";

            public static MelonPreferences_Category MainCategory;

            public static MelonPreferences_Entry<bool> Debug;

            public static MelonPreferences_Entry<bool> Enabled;
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
            public static MelonPreferences_Entry<bool> OnPBDNF;
            public static MelonPreferences_Entry<bool> OnRestart;
            public static MelonPreferences_Entry<bool> NonPBs;
            public static MelonPreferences_Entry<int> AutoSeconds;
            public static MelonPreferences_Entry<KeyCode> EarlyFinish;
            public static MelonPreferences_Entry<bool> StartAlert;
            public static MelonPreferences_Entry<bool> AutoAlert;

            public static MelonPreferences_Entry<bool> SplitFile;
            public static MelonPreferences_Entry<int> MaxDiscards;

            public static void Register()
            {
                NeonLite.Settings.AddHolder(h);
                
                Enabled = NeonLite.Settings.Add(h, "", "enabled", "Enabled", null, false);
#if DEBUG
                Debug = NeonLite.Settings.Add(h, "", "debug", "Debug Mode", null, true, true);
#endif


                StallLoad = NeonLite.Settings.Add(h, "", "stallLoad", "Stall Loading", "Wait to load the level until recording has started. This ensures everything's captured.\nDisable this if your load times are unreasonably long.", true);
                StartAlert = NeonLite.Settings.Add(h, "", "startAlert", "Start Recording Notification", null, true);
                EarlyFinish = NeonLite.Settings.Add(h, "", "earlyFinish", "Control Key", "The key to press to either save before the auto-save, or save the current run despite never touching the goal.", KeyCode.Semicolon);
                SplitFile = NeonLite.Settings.Add(h, "", "splitFile", "Split Output File", """
                    Use OBS's file splitting to make clips. Ensure Automatic File Splitting is on and set to split manually.
                    Lowering the keyframe interval typically makes it work better.
                    If able, add keyint=X to your custom encoder options, where X is b-frames plus one for absolute best results.
                    """, false);
                MaxDiscards = NeonLite.Settings.Add(h, "", "max_discards", "Maximum Discards", "The maximum amount of files to save to the .NCdiscard folder. Set to 0 to disable entirely.", 5);
                Port = NeonLite.Settings.Add(h, "", "port", "Port", null, 4455);
                Password = NeonLite.Settings.Add(h, "", "password", "Password", "Your OBS Websockets password, if required. Be careful about sharing this!", "");

                AutoSeconds = NeonLite.Settings.Add(h, "Saving", "autoSave", "Seconds until Auto-save", "How many seconds to wait after leaderboard screen/DNF to automatically save the recording.\nSet to -1 to disable.\nYou can always use the Control Key to save and they will use the appropriate folder.", 5);
                AutoAlert = NeonLite.Settings.Add(h, "Saving", "autoAlert", "Auto-save Notification", null, true);
                OnDNF = NeonLite.Settings.Add(h, "Saving", "autoDNF", "Auto-save on DNF", null, true);
                OnPBDNF = NeonLite.Settings.Add(h, "Saving", "autoPBDNF", "Auto-save on DNF PB", "Auto-saves DNF runs only if they were PBs.", true);
                NonPBs = NeonLite.Settings.Add(h, "Saving", "autoNoPB", "Auto-save on non-PBs", null, false);
                OnRestart = NeonLite.Settings.Add(h, "Saving", "autoRestart", "Auto-save on all restarts", null, false);

                OutputFile = NeonLite.Settings.Add(h, "Output", "format", "Output File Format", """
                What to name the output file. The file extension will be automatically added. The format is as follows:
                %% - Actual %
                %l - Unlocalized level name, e.g. GRID_PORT
                %L - Localized level name, e.g. Glass Port
                %t - Pure time in microseconds, e.g. 80030999
                %T - Time in just seconds + milliseconds, e.g. 80.030
                %D - Formatted date/time, e.g. 2024-03-31 21-50-17
                %d - Unformatted date/time, e.g. 20240331215017 (good for sorting!)
                %B - The type, as listed by the settings below
                """, "Neon White/%L/%B/%d - %T");

                PBType = NeonLite.Settings.Add(h, "Output", "pb", "PB Format String", null, "PBs");
                DNFType = NeonLite.Settings.Add(h, "Output", "dnf", "DNF Format String", null, "DNFs");
                NoPBType = NeonLite.Settings.Add(h, "Output", "nonPB", "Non-PB Format String", null, "Non-PBs");
                ManualType = NeonLite.Settings.Add(h, "Output", "manual", "Manual Format String", null, "Manual");
                GhostDir = NeonLite.Settings.Add(h, "Output", "ghostDir", "Use Ghost Directory", "Whether or not to place new recordings in their respective ghost directories or to keep the current video directory.", false);


                // check for migration  
                var category = MelonPreferences.CreateCategory("NeonCapture");
                var migrated = category.CreateEntry("MIGRATED", false, is_hidden: true).Value;
                if (!migrated)
                {
                    category.GetEntry<bool>("MIGRATED").Value = true;

                    Enabled.Value = category.CreateOrFind("Enabled", true, true).Value;
                    StallLoad.Value = category.CreateOrFind("Stall Loading", true, true).Value;
                    Port.Value = category.CreateOrFind("Port", 4455, true).Value;
                    Password.Value = category.CreateOrFind("Password", "", true).Value;
                    AutoSeconds.Value = category.CreateOrFind("Seconds until Auto-save", 3, true).Value;
                    StartAlert.Value = category.CreateOrFind("Start Recording Notification", false, true).Value;
                    AutoAlert.Value = category.CreateOrFind("Auto-save Notification", true, true).Value;
                    OnDNF.Value = category.CreateOrFind("Save recording on DNF", true, true).Value;
                    OnPBDNF.Value = category.CreateOrFind("Save recording on DNF PB", true, true).Value;
                    NonPBs.Value = category.CreateOrFind("Save recording on non-PBs", false, true).Value;
                    OnRestart.Value = category.CreateOrFind("Save recording on all restarts", false, true).Value;
                    OutputFile.Value = category.CreateOrFind("Output File Format", "Neon White/%L/%B/%d - %T", true).Value;
                    PBType.Value = category.CreateOrFind("PB Format String", "PBs", true).Value;
                    DNFType.Value = category.CreateOrFind("DNF Format String", "DNFs", true).Value;
                    NoPBType.Value = category.CreateOrFind("Non-PB Format String", "Non-PBs", true).Value;
                    ManualType.Value = category.CreateOrFind("Manual Format String", "Manual", true).Value;
                    GhostDir.Value = category.CreateOrFind("Use Ghost Directory", false, true).Value;
                    EarlyFinish.Value = category.CreateOrFind("Early Finish Key", KeyCode.Semicolon, true).Value;
                    
                    //MelonPreferences.Categories.Remove(category);
                }
            }
        }
    }

    public static class Extensions
    {
#pragma warning disable CS0162
        [Conditional("DEBUG")]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void DebugMsg(this MelonLogger.Instance log, string msg)
        {
            if (NeonCapture.DEBUG)
            {
                log.Msg(msg);
                //UnityEngine.NeonCapture.Log.Msg($"[SuperPotato] {msg}");
            }
        }

        [Conditional("DEBUG")]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void DebugMsg(this MelonLogger.Instance log, object obj) => DebugMsg(log, obj.ToString());

        internal static MelonPreferences_Entry<T> CreateOrFind<T>(this MelonPreferences_Category category, string name, T defaultVal, bool hide = false)
        {
            try
            {
                return category.CreateEntry(name, defaultVal, dont_save_default: true, is_hidden: hide);
            }
            catch
            {
                var c = category.GetEntry<T>(name);
                c.IsHidden = hide;
                return c;
            }
        }
    }
}
