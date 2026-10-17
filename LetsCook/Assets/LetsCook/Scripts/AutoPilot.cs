using System;
using System.Collections;
using System.IO;
using UnityEngine;

namespace LetsCook
{
    /// Headless-ish checks of the real game loop from the command line:
    ///   LetsCook.app/Contents/MacOS/LetsCook -autoshot <dir> -autoquit <seconds> [-autocook]
    /// Takes a screenshot every 2 s and quits. -autocook drops the bread in the pan with 30× cooking speed
    /// and serves it at the end, to exercise heat, plating and the chef server.
    public class AutoPilot : MonoBehaviour
    {
        string shotDir;
        float quitAfter;
        bool cook;

        void Start()
        {
            var args = Environment.GetCommandLineArgs();
            shotDir = Arg(args, "-autoshot");
            float.TryParse(Arg(args, "-autoquit") ?? "0", System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out quitAfter);
            cook = Array.IndexOf(args, "-autocook") >= 0;
            if (shotDir == null && quitAfter <= 0f) { enabled = false; return; }
            StartCoroutine(Run());
        }

        IEnumerator Run()
        {
            if (cook)
            {
                Food.TimeScale = 30f;
                // Lift the hands out of the way so the demo isn't knocked over by the animation.
                var wrists = FindFirstObjectByType<WristController>();
                if (wrists != null) { wrists.left.position += Vector3.up * 0.25f; wrists.right.position += Vector3.up * 0.25f; }
                var pan = FindFirstObjectByType<Cookware>();
                foreach (var f in Food.All.ToArray())
                    if (f.ingredient == "bread") Teleport(f.gameObject, pan.transform.position + new Vector3(-0.02f, 0.03f, 0f));
                    else if (f.ingredient == "butter") Teleport(f.gameObject, pan.transform.position + new Vector3(0.06f, 0.04f, 0.04f));
            }
            int n = 0;
            float end = Time.realtimeSinceStartup + (quitAfter > 0f ? quitAfter : 6f);
            while (Time.realtimeSinceStartup < end)
            {
                yield return new WaitForSecondsRealtime(2f);
                if (shotDir != null)
                {
                    Directory.CreateDirectory(shotDir);
                    ScreenCapture.CaptureScreenshot(Path.Combine(shotDir, $"auto-{n++:00}.png"));
                }
            }
            if (cook)
            {
                var plating = FindFirstObjectByType<Plating>();
                foreach (var f in Food.All.ToArray())
                    if (f.ingredient == "bread" || f.ingredient == "butter")
                        Teleport(f.gameObject, plating.plate.position + new Vector3(f.ingredient == "bread" ? -0.02f : 0.05f, 0.04f, 0f));
                yield return new WaitForSecondsRealtime(1f);
                plating.Serve();
                float until = Time.realtimeSinceStartup + 60f;
                while ((plating.Busy || plating.LastDish == null) && Time.realtimeSinceStartup < until) yield return null;
                yield return new WaitForSecondsRealtime(1f);
                if (shotDir != null) ScreenCapture.CaptureScreenshot(Path.Combine(shotDir, "auto-served.png"));
                yield return new WaitForSecondsRealtime(1f);
                Debug.Log("[let's cook] autopilot served: " + plating.LastDish?.dish_name);
            }
            var rx = FindFirstObjectByType<RokokoReceiver>();
            Debug.Log($"[let's cook] autopilot done: glove {(rx != null ? rx.Status + ", packets " + rx.PacketsReceived : "none")}");
            Application.Quit();
        }

        /// Interpolated rigidbodies ignore transform.position writes, so move the body itself.
        static void Teleport(GameObject go, Vector3 position)
        {
            if (go.TryGetComponent<Rigidbody>(out var rb))
            {
                rb.position = position;
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }
            go.transform.position = position;
        }

        static string Arg(string[] args, string key)
        {
            int i = Array.IndexOf(args, key);
            return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
        }
    }
}
