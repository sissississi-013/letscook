using System.Collections.Generic;
using UnityEngine;

namespace LetsCook
{
    /// Draws one glove hand as joint spheres + bone capsules, straight from world-space joint positions.
    /// The gloves give no reliable wrist position, so the wrist is pinned to `anchor` (moved by keyboard for now).
    public class GloveHand : MonoBehaviour
    {
        public RokokoReceiver receiver;
        public string side = "Left";
        public Transform anchor;
        public float jointRadius = 0.0095f;
        public Color skinColor = new Color(0.93f, 0.74f, 0.6f);

        readonly Dictionary<string, Transform> spheres = new Dictionary<string, Transform>();
        readonly Dictionary<string, Transform> bones = new Dictionary<string, Transform>();
        readonly Dictionary<string, Vector3> positions = new Dictionary<string, Vector3>();
        Material material;

        public GloveFrame Frame { get; private set; }
        /// Wrist rotation in game space (glove rotation turned by the anchor).
        public Quaternion WristRotation { get; private set; } = Quaternion.identity;
        public bool HasPose => positions.Count > 0;

        /// Game-space position of a joint (e.g. "LeftFinger2Tip"), after anchoring.
        public bool TryGetPosition(string joint, out Vector3 position) => positions.TryGetValue(joint, out position);
        public float Curl(int finger) => Frame != null ? HandMath.Curl(Frame, side, finger) : 0f;

        void LateUpdate()
        {
            if (receiver != null) Apply(receiver.Latest);
        }

        public void Apply(GloveFrame frame)
        {
            if (frame == null || !frame.TryGet(side + "Hand", out var wrist)) return;
            Frame = frame;
            Vector3 origin = anchor != null ? anchor.position : transform.position;
            Quaternion turn = anchor != null ? anchor.rotation : Quaternion.identity;
            WristRotation = turn * wrist.rotation;

            foreach (var kv in frame.joints)
            {
                if (!IsMine(kv.Key)) continue;
                Vector3 p = origin + turn * (kv.Value.position - wrist.position);
                positions[kv.Key] = p;
                Sphere(kv.Key).position = p;
            }
            foreach (var kv in frame.joints)
            {
                if (!IsMine(kv.Key) || kv.Value.parent == null || !positions.ContainsKey(kv.Value.parent)) continue;
                if (kv.Key.EndsWith("Metacarpal") && !kv.Key.Contains("Finger1")) continue;   // the palm covers these
                PlaceBone(Bone(kv.Key), positions[kv.Value.parent], positions[kv.Key], (Radius(kv.Value.parent) + Radius(kv.Key)) * 0.5f);
            }
            PlacePalm(turn * wrist.rotation);
        }

        bool IsMine(string joint) => joint == side + "Hand" || joint.StartsWith(side + "Finger");

        Material Mat()
        {
            if (material == null)
            {
                material = new Material(Shader.Find("Standard")) { color = skinColor };
                material.SetFloat("_Glossiness", 0.22f);
            }
            return material;
        }

        Transform Sphere(string joint)
        {
            if (spheres.TryGetValue(joint, out var t)) return t;
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = joint;
            Destroy(go.GetComponent<Collider>());
            go.GetComponent<Renderer>().sharedMaterial = Mat();
            float r = Radius(joint);
            go.transform.localScale = Vector3.one * r * 2f;
            go.transform.SetParent(transform, true);
            spheres[joint] = go.transform;
            return go.transform;
        }

        Transform Bone(string joint)
        {
            if (bones.TryGetValue(joint, out var t)) return t;
            var go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            go.name = joint + "_bone";
            Destroy(go.GetComponent<Collider>());
            go.GetComponent<Renderer>().sharedMaterial = Mat();
            go.transform.SetParent(transform, true);
            bones[joint] = go.transform;
            return go.transform;
        }

        /// Fingers taper from knuckle to tip; the thumb is a little thicker.
        float Radius(string joint)
        {
            if (joint.EndsWith("Hand")) return jointRadius * 1.4f;
            float r = jointRadius;
            if (joint.EndsWith("Metacarpal") || joint.EndsWith("Proximal")) r *= 1.12f;
            else if (joint.EndsWith("Distal")) r *= 0.9f;
            else if (joint.EndsWith("Tip")) r *= 0.82f;
            if (joint.Contains("Finger1")) r *= 1.15f;
            if (joint.Contains("Finger5")) r *= 0.85f;
            return r;
        }

        void PlaceBone(Transform bone, Vector3 from, Vector3 to, float radius)
        {
            Vector3 d = to - from;
            float len = d.magnitude;
            if (len < 1e-5f) { bone.gameObject.SetActive(false); return; }
            bone.gameObject.SetActive(true);
            bone.position = (from + to) * 0.5f;
            bone.rotation = Quaternion.FromToRotation(Vector3.up, d / len);
            float w = radius * 2f;
            bone.localScale = new Vector3(w, len * 0.5f + radius * 0.5f, w);
        }

        Transform palm;

        /// A flattened rounded palm spanning the wrist and the index–little knuckles.
        void PlacePalm(Quaternion handRotation)
        {
            if (!positions.TryGetValue(side + "Hand", out var w) ||
                !positions.TryGetValue(side + "Finger2Proximal", out var index) ||
                !positions.TryGetValue(side + "Finger5Proximal", out var little)) return;
            if (palm == null)
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                go.name = side + " Palm";
                Destroy(go.GetComponent<Collider>());
                go.GetComponent<Renderer>().sharedMaterial = Mat();
                go.transform.SetParent(transform, true);
                palm = go.transform;
            }
            Vector3 knuckles = (index + little) * 0.5f;
            Vector3 forward = knuckles - w;
            Vector3 across = index - little;
            Vector3 up = Vector3.Cross(forward, across).normalized;
            if (side == "Left") up = -up;
            palm.position = Vector3.Lerp(w, knuckles, 0.5f);
            palm.rotation = Quaternion.LookRotation(forward.normalized, up);
            palm.localScale = new Vector3(across.magnitude + jointRadius * 2.6f, jointRadius * 2.6f, forward.magnitude + jointRadius * 2.2f);
        }

        static void Destroy(Object o)
        {
            if (Application.isPlaying) Object.Destroy(o); else Object.DestroyImmediate(o);
        }
    }
}
