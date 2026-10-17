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
            var logger = rig.AddComponent<SessionLogger>();
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

            // Keep one session as a fixture for the Python converter tests.
            File.Copy(path, Path.Combine(dir, "sample-session.jsonl"), true);
            File.Delete(path);
        }
    }
}
