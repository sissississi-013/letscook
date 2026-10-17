using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace LetsCook.Tests
{
    public class SessionLoggerPlayTests
    {
        [UnityTest]
        public IEnumerator LogsFramesObjectsAndEvents()
        {
            var kitchen = Kitchen.Build();
            var rig = new GameObject("rig");
            rig.SetActive(false);
            var rx = rig.AddComponent<RokokoReceiver>();
            rx.port = 15047;
            GloveHand MakeHand(string side, Vector3 at)
            {
                var anchor = new GameObject(side + " anchor").transform;
                anchor.SetParent(rig.transform);
                anchor.position = at;
                var go = new GameObject(side + " Hand");
                go.transform.SetParent(rig.transform);
                var h = go.AddComponent<GloveHand>();
                h.side = side; h.receiver = rx; h.anchor = anchor;
                go.AddComponent<HandGrasp>();
                return h;
            }
            var cam = new GameObject("cam").AddComponent<Camera>();
            cam.transform.SetParent(rig.transform);
            cam.transform.position = new Vector3(0f, 1.5f, -0.25f);
            cam.transform.LookAt(new Vector3(0f, 0.9f, 0.5f));
            var video = rig.AddComponent<SessionVideo>();
            video.ego = cam;
            video.agent = cam;
            var logger = rig.AddComponent<SessionLogger>();
            logger.video = video;
            logger.receiver = rx;
            logger.left = MakeHand("Left", new Vector3(-0.22f, 1.1f, 0.35f));
            logger.right = MakeHand("Right", new Vector3(0.22f, 1.1f, 0.35f));
            string dir = Path.Combine(Path.GetFullPath(Path.Combine(Application.dataPath, "../../tests/fixtures")));
            logger.directory = dir;
            rig.SetActive(true);

            yield return new WaitForSeconds(2.2f);
            CookEvents.Emit("cut", "tomato", "halves");
            yield return new WaitForSeconds(0.2f);
            string path = logger.FilePath;
            Object.Destroy(rig);
            Object.Destroy(kitchen.gameObject);
            yield return null;

            var lines = File.ReadAllLines(path);
            Assert.That(lines[0], Does.Contain("\"type\":\"meta\""));
            int frames = lines.Count(l => l.Contains("\"type\":\"frame\""));
            Assert.That(frames, Is.InRange(55, 80), "about 30 fps for 2.4 s");
            var frame = lines.Last(l => l.Contains("\"type\":\"frame\""));
            Assert.That(frame, Does.Contain("\"Left\":{\"ok\":true"));
            Assert.That(frame, Does.Contain("\"name\":\"tomato\""));
            Assert.That(frame, Does.Contain("\"doneness\":\"raw\""));
            Assert.That(frame, Does.Contain("\"contacts\":["));
            Assert.IsTrue(lines.Any(l => l.Contains("\"event\":\"cut\"")));

            Assert.That(frame, Does.Contain("\"video_frame\":"));
            Assert.That(lines[0], Does.Contain("\"videos\":[\"ego\",\"agentview\"]"));
            string videoDir = path.Replace(".jsonl", "-video");
            Assert.IsTrue(File.Exists(Path.Combine(videoDir, "ego.mp4")), "ego video");
            Assert.Greater(new FileInfo(Path.Combine(videoDir, "agentview.mp4")).Length, 1000, "agentview video has content");

            // Keep one session (+ its video) as a fixture for the Python converter tests.
            File.Copy(path, Path.Combine(dir, "sample-session.jsonl"), true);
            string fixtureVideo = Path.Combine(dir, "sample-session-video");
            Directory.CreateDirectory(fixtureVideo);
            foreach (var name in new[] { "ego.mp4", "agentview.mp4" })
                File.Copy(Path.Combine(videoDir, name), Path.Combine(fixtureVideo, name), true);
            File.Delete(path);
            Directory.Delete(videoDir, true);
        }
    }
}
