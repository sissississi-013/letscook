using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

namespace LetsCook
{
    /// Writes everything a robot-learning pipeline needs to sessions/unity-<time>.jsonl at 30 fps:
    /// wrists, 21 MediaPipe-order keypoints per hand, raw glove joint rotations, curls, held objects,
    /// assisted-grasp flags, every object's pose + cooking state, and the event history.
    /// Unity frame: left-handed, Y-up, metres. tools/to_lerobot.py converts to right-handed Z-up.
    public class SessionLogger : MonoBehaviour
    {
        public RokokoReceiver receiver;
        public GloveHand left, right;
        public float fps = 30f;
        public string directory;   // default: <repo>/sessions
        public SessionVideo video; // optional: egocentric + agentview video, frame-aligned with the log

        public string FilePath { get; private set; }
        public int FramesWritten { get; private set; }

        StreamWriter writer;
        float nextFrame;
        readonly Dictionary<KitchenItem, int> ids = new Dictionary<KitchenItem, int>();
        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        /// MediaPipe hand landmark order mapped to Rokoko v4 joint names.
        public static readonly string[] Keypoints =
        {
            "Hand",
            "Finger1Metacarpal", "Finger1Proximal", "Finger1Distal", "Finger1Tip",
            "Finger2Proximal", "Finger2Medial", "Finger2Distal", "Finger2Tip",
            "Finger3Proximal", "Finger3Medial", "Finger3Distal", "Finger3Tip",
            "Finger4Proximal", "Finger4Medial", "Finger4Distal", "Finger4Tip",
            "Finger5Proximal", "Finger5Medial", "Finger5Distal", "Finger5Tip",
        };

        /// The 19 rotating finger joints per hand (tips carry no rotation of their own).
        public static readonly string[] RotJoints =
        {
            "Finger1Metacarpal", "Finger1Proximal", "Finger1Distal",
            "Finger2Metacarpal", "Finger2Proximal", "Finger2Medial", "Finger2Distal",
            "Finger3Metacarpal", "Finger3Proximal", "Finger3Medial", "Finger3Distal",
            "Finger4Metacarpal", "Finger4Proximal", "Finger4Medial", "Finger4Distal",
            "Finger5Metacarpal", "Finger5Proximal", "Finger5Medial", "Finger5Distal",
        };

        void OnEnable()
        {
            if (string.IsNullOrEmpty(directory)) directory = DefaultDirectory();
            Directory.CreateDirectory(directory);
            string id = "unity-" + DateTime.Now.ToString("yyyyMMdd-HHmmss");
            FilePath = Path.Combine(directory, id + ".jsonl");
            writer = new StreamWriter(FilePath, false, new UTF8Encoding(false)) { AutoFlush = false };
            bool hasVideo = video != null && video.Begin(Path.Combine(directory, id + "-video"));

            var sb = new StringBuilder();
            sb.Append("{\"type\":\"meta\",\"session_id\":").Append(Q(id))
              .Append(",\"fps\":").Append(F(fps))
              .Append(",\"frame\":\"unity: left-handed, Y-up, metres, quaternions xyzw\"")
              .Append(",\"keypoints\":").Append(Names(Keypoints))
              .Append(",\"rot_joints\":").Append(Names(RotJoints))
              .Append(",\"unity\":").Append(Q(Application.unityVersion))
              .Append(",\"started\":").Append(Q(DateTime.UtcNow.ToString("o")))
              .Append(",\"video_dir\":").Append(hasVideo ? Q(id + "-video") : "null")
              .Append(",\"videos\":").Append(hasVideo ? "[\"ego\",\"agentview\"]" : "[]")
              .Append('}');
            writer.WriteLine(sb.ToString());
            CookEvents.Emitted += OnEvent;
            Debug.Log("[let's cook] Logging session to " + FilePath);
        }

        /// <repo>/sessions: the folder holding chef_server.py above the project (editor) or the app (build).
        public static string DefaultDirectory()
        {
            var dir = new DirectoryInfo(Application.dataPath);
            for (int i = 0; i < 6 && dir != null; i++, dir = dir.Parent)
                if (File.Exists(Path.Combine(dir.FullName, "chef_server.py"))) return Path.Combine(dir.FullName, "sessions");
            return Path.Combine(Application.persistentDataPath, "sessions");
        }

        void OnDisable()
        {
            CookEvents.Emitted -= OnEvent;
            video?.End();
            writer?.Flush();
            writer?.Dispose();
            writer = null;
        }

        void OnEvent(CookEvents.Entry e)
        {
            writer?.WriteLine($"{{\"type\":\"event\",\"t\":{F(Time.time)},\"event\":{Q(e.type)},\"subject\":{Q(e.subject)},\"detail\":{Q(e.detail)}}}");
        }

        void LateUpdate()
        {
            if (writer == null || Time.time < nextFrame) return;
            nextFrame = Mathf.Max(nextFrame + 1f / fps, Time.time);
            int videoFrame = video != null ? video.Capture() : -1;
            writer.WriteLine(FrameJson(videoFrame));
            FramesWritten++;
            if (FramesWritten % 30 == 0) writer.Flush();
        }

        public string FrameJson(int videoFrame = -1)
        {
            var sb = new StringBuilder(4096);
            sb.Append("{\"type\":\"frame\",\"t\":").Append(F(Time.time))
              .Append(",\"video_frame\":").Append(videoFrame)
              .Append(",\"live\":").Append(receiver != null && receiver.IsLive ? "true" : "false")
              .Append(",\"hands\":{");
            Hand(sb, left); sb.Append(',');
            Hand(sb, right);
            sb.Append("},\"objects\":[");
            bool first = true;
            foreach (var item in KitchenItem.All)
            {
                if (item.kind == ItemKind.Fixture) continue;
                if (!first) sb.Append(',');
                first = false;
                Item(sb, item);
            }
            sb.Append(']');
            var pan = FindFirstObjectByType<Cookware>();
            if (pan != null) sb.Append(",\"pan_temp\":").Append(F(pan.temperature));
            sb.Append('}');
            return sb.ToString();
        }

        void Hand(StringBuilder sb, GloveHand hand)
        {
            string side = hand != null ? hand.side : "?";
            sb.Append(Q(side)).Append(":{");
            if (hand == null || !hand.HasPose) { sb.Append("\"ok\":false}"); return; }
            hand.TryGetPosition(side + "Hand", out var wrist);
            sb.Append("\"ok\":true,\"wrist\":").Append(V(wrist)).Append(',').Append("\"wrist_rot\":").Append(Rot(hand.WristRotation));
            sb.Append(",\"kp\":[");
            for (int i = 0; i < Keypoints.Length; i++)
            {
                if (i > 0) sb.Append(',');
                hand.TryGetPosition(side + Keypoints[i], out var p);
                sb.Append(V(p));
            }
            sb.Append("],\"rot\":[");
            for (int i = 0; i < RotJoints.Length; i++)
            {
                if (i > 0) sb.Append(',');
                var r = Quaternion.identity;
                if (hand.Frame != null && hand.Frame.TryGet(side + RotJoints[i], out var j)) r = j.rotation;
                sb.Append(Rot(r));
            }
            sb.Append("],\"curl\":[");
            for (int f = 1; f <= 5; f++) { if (f > 1) sb.Append(','); sb.Append(F(hand.Curl(f))); }
            sb.Append(']');
            var grasp = hand.GetComponent<HandGrasp>();
            int held = -1;
            if (grasp != null && grasp.Held != null)
            {
                var item = grasp.Held.GetComponentInParent<KitchenItem>();
                if (item != null) held = Id(item);
            }
            sb.Append(",\"contacts\":[");
            for (int i = 0; i < 6; i++)
            {
                if (i > 0) sb.Append(',');
                var rb = grasp != null ? grasp.Contacts[i] : null;
                var touched = rb != null ? rb.GetComponentInParent<KitchenItem>() : null;
                sb.Append(touched != null ? Id(touched) : -1);
            }
            sb.Append(']');
            sb.Append(",\"held\":").Append(held).Append(",\"assisted\":").Append(grasp != null && grasp.Assisted ? "true" : "false");
            sb.Append('}');
        }

        void Item(StringBuilder sb, KitchenItem item)
        {
            var t = item.transform;
            sb.Append("{\"id\":").Append(Id(item)).Append(",\"name\":").Append(Q(item.displayName))
              .Append(",\"kind\":").Append(Q(item.kind.ToString().ToLowerInvariant()))
              .Append(",\"pos\":").Append(V(t.position)).Append(",\"rot\":").Append(Rot(t.rotation));
            var food = item.GetComponent<Food>();
            if (food != null && food.state != null)
            {
                var s = food.state;
                sb.Append(",\"food\":{\"cut\":").Append(Q(food.cut)).Append(",\"doneness\":").Append(Q(s.Doneness))
                  .Append(",\"surface\":").Append(F(s.surfaceTemp)).Append(",\"core\":").Append(F(s.coreTemp))
                  .Append(",\"browning\":").Append(F(s.browning)).Append(",\"char\":").Append(F(s.charred))
                  .Append(",\"set\":").Append(F(s.set)).Append(",\"melted\":").Append(F(s.melted))
                  .Append(",\"salt\":").Append(F(s.salt)).Append(",\"pepper\":").Append(F(s.pepper)).Append('}');
            }
            sb.Append('}');
        }

        int Id(KitchenItem item)
        {
            if (!ids.TryGetValue(item, out int id)) { id = ids.Count; ids[item] = id; }
            return id;
        }

        static string F(float v) => v.ToString("0.#####", Inv);
        static string V(Vector3 v) => $"[{F(v.x)},{F(v.y)},{F(v.z)}]";
        static string Rot(Quaternion q) => $"[{F(q.x)},{F(q.y)},{F(q.z)},{F(q.w)}]";
        static string Q(string s) => "\"" + (s ?? "").Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
        static string Names(string[] names) => "[" + string.Join(",", Array.ConvertAll(names, Q)) + "]";
    }
}
