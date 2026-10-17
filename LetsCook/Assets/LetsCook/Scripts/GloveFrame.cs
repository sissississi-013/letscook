using System;
using System.Collections.Generic;
using UnityEngine;

namespace LetsCook
{
    // JSON v4 ("newtons") shapes for JsonUtility. See research/rokoko-streaming.md §2.4.
    [Serializable] public class V3 { public float x, y, z; }
    [Serializable] public class V4 { public float x, y, z, w; }
    [Serializable] public class JointData { public string name; public int parent; public V3 position; public V4 rotation; }
    [Serializable] public class NewtonData { public string name; public JointData[] joints; }
    [Serializable] public class SceneData { public double timestamp; public NewtonData[] newtons; }
    [Serializable] public class FrameData { public SceneData scene; }

    public struct JointPose
    {
        public Vector3 position;     // Studio world space: left-handed, Y-up, metres (same as Unity)
        public Quaternion rotation;
        public string parent;        // null for roots
    }

    /// One parsed glove frame: joints by name.
    public class GloveFrame
    {
        public double timestamp;
        public string source;
        public readonly Dictionary<string, JointPose> joints = new Dictionary<string, JointPose>();

        public bool TryGet(string name, out JointPose pose) => joints.TryGetValue(name, out pose);

        /// Returns null when the JSON has no v4 newton (e.g. a v3 frame).
        public static GloveFrame Parse(string json)
        {
            FrameData data;
            try { data = JsonUtility.FromJson<FrameData>(json); }
            catch (ArgumentException) { return null; }
            if (data?.scene?.newtons == null || data.scene.newtons.Length == 0) return null;

            var newton = data.scene.newtons[0];
            if (newton.joints == null) return null;
            var frame = new GloveFrame { timestamp = data.scene.timestamp, source = newton.name };
            foreach (var j in newton.joints)
            {
                if (j?.name == null || j.position == null || j.rotation == null) continue;
                string parent = j.parent >= 0 && j.parent < newton.joints.Length ? newton.joints[j.parent].name : null;
                frame.joints[j.name] = new JointPose
                {
                    position = new Vector3(j.position.x, j.position.y, j.position.z),
                    rotation = new Quaternion(j.rotation.x, j.rotation.y, j.rotation.z, j.rotation.w),
                    parent = parent,
                };
            }
            return frame;
        }

        /// Blend two frames joint by joint (used for the built-in mock animation).
        public static GloveFrame Lerp(GloveFrame a, GloveFrame b, float t)
        {
            var f = new GloveFrame { timestamp = a.timestamp + (b.timestamp - a.timestamp) * t, source = a.source };
            foreach (var kv in a.joints)
            {
                if (!b.joints.TryGetValue(kv.Key, out var jb)) continue;
                f.joints[kv.Key] = new JointPose
                {
                    position = Vector3.Lerp(kv.Value.position, jb.position, t),
                    rotation = Quaternion.Slerp(kv.Value.rotation, jb.rotation, t),
                    parent = kv.Value.parent,
                };
            }
            return f;
        }
    }

    public static class HandMath
    {
        public static readonly string[] FingerParts = { "Metacarpal", "Proximal", "Medial", "Distal", "Tip" };
        public static readonly string[] ThumbParts = { "Metacarpal", "Proximal", "Distal", "Tip" };

        public static string[] PartsFor(int finger) => finger == 1 ? ThumbParts : FingerParts;
        public static string JointName(string side, int finger, string part) => $"{side}Finger{finger}{part}";

        /// 0 = straight, 1 = fully curled. Sum of bend angles between consecutive finger segments.
        public static float Curl(GloveFrame frame, string side, int finger)
        {
            var parts = PartsFor(finger);
            float bend = 0f;
            Vector3? prev = null;
            for (int i = 0; i < parts.Length - 1; i++)
            {
                if (!frame.TryGet(JointName(side, finger, parts[i]), out var a) ||
                    !frame.TryGet(JointName(side, finger, parts[i + 1]), out var b)) return 0f;
                Vector3 dir = (b.position - a.position).normalized;
                if (prev.HasValue) bend += Vector3.Angle(prev.Value, dir);
                prev = dir;
            }
            float max = finger == 1 ? 125f : 245f;
            return Mathf.Clamp01(bend / max);
        }
    }
}
