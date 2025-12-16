using NeonLite;
using NeonLite.Modules;
using System.Linq;
using UnityEngine;

namespace NeonCapture.Objects
{
    internal class Scheduler : MonoBehaviour, IModule
    {
        static Scheduler i;

        internal static bool active = true;
        const bool priority = true;

        static string host;

        static void Setup()
        {
            active = NeonCapture.Settings.Enabled.SetupForModule(Activate, (_, after) => after);
            NeonCapture.Settings.Port.OnEntryValueChanged.Subscribe((_, after) => host = $"ws://localhost:{after}");
            host = $"ws://localhost:{NeonCapture.Settings.Port.Value}";
        }

        static void Activate(bool activate)
        {
            if (!activate)
                Handler.i.OnDestroy();
            
            active = activate;
            if (i)
                i.enabled = active;
        }

        void Awake()
        {
            i = this;
            i.enabled = active;
        }

        void Update()
        {
            if (!Awaiter.instances.Any(x => x.hostname == host))
            {
                var a = gameObject.AddComponent<Awaiter>();
                a.hostname = host;
            }

            bool calledDestroy = false;
            foreach (var a in Awaiter.instances)
            {
                if (a.hostname != host)
                {
                    Destroy(a);
                    if (!calledDestroy)
                    {
                        calledDestroy = true;
                        Handler.i.OnDestroy();
                    }
                }
            }
        }
    }
}