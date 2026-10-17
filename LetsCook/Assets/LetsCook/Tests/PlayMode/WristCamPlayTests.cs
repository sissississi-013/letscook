using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace LetsCook.Tests
{
    public class WristCamPlayTests
    {
        [UnityTest]
        public IEnumerator CameraPacketsGlideTheWristsAndPauseKeyboard()
        {
            var go = new GameObject("rig");
            go.SetActive(false);
            var wrists = go.AddComponent<WristController>();
            wrists.left = new GameObject("L").transform;
            wrists.right = new GameObject("R").transform;
            wrists.left.position = new Vector3(-0.22f, 1f, 0.35f);
            wrists.right.position = new Vector3(0.22f, 1f, 0.35f);
            var cam = go.AddComponent<WristCamInput>();
            cam.port = 15048;
            cam.wrists = wrists;
            go.SetActive(true);

            float until = Time.realtimeSinceStartup + 0.6f;
            while (Time.realtimeSinceStartup < until)
            {
                cam.Inject("{\"t\":0,\"hands\":{\"Left\":{\"ok\":true,\"pos\":[-0.4,1.1,0.5]},\"Right\":{\"ok\":false}}}");
                yield return null;
            }
            Assert.IsTrue(cam.Active);
            Assert.IsTrue(wrists.externallyDriven);
            Assert.Less(Vector3.Distance(wrists.left.position, new Vector3(-0.4f, 1.1f, 0.5f)), 0.02f, "left wrist should glide to the camera position");
            Assert.AreEqual(new Vector3(0.22f, 1f, 0.35f), wrists.right.position, "an untracked hand stays put");

            cam.Inject("{\"t\":0,\"hands\":{\"Left\":{\"ok\":true,\"pos\":[5,5,5]}}}");
            yield return new WaitForSecondsRealtime(0.3f);
            Assert.LessOrEqual(wrists.left.position.x, wrists.max.x + 1e-4f, "camera positions are clamped to the kitchen");

            yield return new WaitForSecondsRealtime(0.7f);
            Assert.IsFalse(cam.Active, "stale camera hands control back to the keyboard");
            Assert.IsFalse(wrists.externallyDriven);
            Object.Destroy(go);
        }
    }
}
