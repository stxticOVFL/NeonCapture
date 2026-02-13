using System.Collections;
using UnityEngine;

namespace NeonCapture.Objects
{
    internal class FileIO : MonoBehaviour
    {
        static internal FileIO i;

        void Awake() => i = this;

        public static void Move(string source, string destination, Action<bool> callback, float delay = 0, int tries = 20) => i.StartCoroutine(MoveCoro(source, destination, callback, delay, tries));

        static IEnumerator MoveCoro(string source, string destination, Action<bool> callback, float delay = 0, int tries = 20)
        {
            if (delay > 0)
                yield return new WaitForSecondsRealtime(delay);

            Directory.CreateDirectory(Path.GetDirectoryName(destination));

            while (tries-- != 0)
            {
                bool success = false;
                try
                {
                    File.Move(source, destination);
                    success = true;
                    break;
                }
                catch { }
                if (!success)
                    yield return new WaitForSecondsRealtime(0.05f);
            }

            NeonCapture.Log.DebugMsg($"Move {tries != -1} {source} to {destination}");
            callback?.Invoke(tries != -1);
        }

        public static void Delete(string path, Action<bool> callback, float delay = 0, int tries = 20) => i.StartCoroutine(DeleteCoro(path, callback, delay, tries));

        static IEnumerator DeleteCoro(string path, Action<bool> callback, float delay = 0, int tries = 20)
        {
            if (delay > 0)
                yield return new WaitForSecondsRealtime(delay);
            while (tries-- != 0)
            {
                bool success = false;
                try
                {
                    File.Delete(path);
                    success = true;
                    break;
                }
                catch { }
                if (!success)
                    yield return new WaitForSecondsRealtime(0.05f);
            }
            NeonCapture.Log.DebugMsg($"Delete {tries != -1} {path}");
            callback?.Invoke(tries != -1);
        }

        public static void Discard(string path, Action<bool> callback, float delay = 0, int tries = 20)
        {
            if (NeonCapture.Settings.MaxDiscards.Value <= 0)
                Delete(path, callback, delay, tries);
            else
                i.StartCoroutine(DiscardCoro(path, delay, tries));
        }

        static IEnumerator DiscardCoro(string path, float delay = 0, int tries = 20)
        {
            if (delay > 0)
                yield return new WaitForSecondsRealtime(delay);

            var dir = Path.Combine(NeonCapture.handler.basePath, ".NCdiscard");
            yield return MoveCoro(path, Path.Combine(dir, Path.GetFileName(path)), null, 0, tries);

            var dirs = Directory.EnumerateFiles(dir);
            while (dirs.Count() > NeonCapture.Settings.MaxDiscards.Value)
            {
                var oldest = dirs.OrderBy(File.GetCreationTimeUtc).First();
                yield return DeleteCoro(oldest, null, 0, tries);
                dirs = Directory.EnumerateFiles(dir);
            }
        }
    }
}
