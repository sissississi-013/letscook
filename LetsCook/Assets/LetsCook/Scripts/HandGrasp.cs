using System;
using System.Collections.Generic;
using UnityEngine;

namespace LetsCook
{
    /// Physical fingertips + grasp detection for one glove hand.
    /// No force feedback, so grasping is inferred: the thumb tip and an opposing fingertip touch the same
    /// object while curling past `grabCurl` → the object is attached with a breakable joint ("assisted grasp").
    /// It lets go when the touching fingers open past `releaseCurl` (hysteresis stops flicker).
    [RequireComponent(typeof(GloveHand))]
    public class HandGrasp : MonoBehaviour
    {
        public float tipRadius = 0.009f;
        public float touchRadius = 0.016f;
        public float grabCurl = 0.3f;
        public float releaseCurl = 0.2f;
        public float breakForce = 250f;

        public Rigidbody Held { get; private set; }
        /// What each fingertip (thumb → little) and the palm (index 5) is touching this physics step, or null.
        public readonly Rigidbody[] Contacts = new Rigidbody[6];
        public float contactRadius = 0.013f;
        public bool Assisted => Held != null;
        public string HeldName => Held != null ? NameOf(Held) : null;
        public static event Action<HandGrasp, Rigidbody> Grabbed, Released;

        GloveHand hand;
        readonly Rigidbody[] tips = new Rigidbody[5];
        Rigidbody palm, grip;
        Vector3 gripOffset;   // grip point in wrist space, fixed at grab time
        FixedJoint joint;
        readonly List<int> gripFingers = new List<int>();
        readonly List<Collider> ignored = new List<Collider>();
        static readonly Collider[] hits = new Collider[16];

        public GloveHand Hand => hand != null ? hand : hand = GetComponent<GloveHand>();

        void Awake()
        {
            hand = GetComponent<GloveHand>();
            for (int i = 0; i < 5; i++) tips[i] = Kinematic($"{hand.side} tip {i + 1}", tipRadius);
            palm = Kinematic($"{hand.side} palm", 0.028f);
            grip = Kinematic($"{hand.side} grip", 0f);
        }

        Rigidbody Kinematic(string name, float radius)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var rb = go.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            if (radius > 0f) go.AddComponent<SphereCollider>().radius = radius;
            return rb;
        }

        string TipName(int finger) => $"{hand.side}Finger{finger}Tip";

        bool TipPosition(int finger, out Vector3 p) => hand.TryGetPosition(TipName(finger), out p);

        bool PalmPosition(out Vector3 p)
        {
            p = default;
            if (!hand.TryGetPosition(hand.side + "Hand", out var w) ||
                !hand.TryGetPosition(hand.side + "Finger3Proximal", out var k)) return false;
            p = Vector3.Lerp(w, k, 0.6f) + Vector3.down * 0.015f;
            return true;
        }

        void FixedUpdate()
        {
            if (!hand.HasPose) return;
            bool solid = hand.receiver == null || hand.receiver.DrivesPhysics;
            if (solid != Solid) SetSolid(solid);
            if (!solid) { System.Array.Clear(Contacts, 0, Contacts.Length); return; }
            if (Held == null || joint == null) { if (Held != null) Release("joint broke"); TryGrab(); }
            else MaybeRelease();

            for (int f = 1; f <= 5; f++)
                if (TipPosition(f, out var p)) { tips[f - 1].MovePosition(p); Contacts[f - 1] = FirstNear(p, contactRadius); }
            if (PalmPosition(out var palmPos)) { palm.MovePosition(palmPos); Contacts[5] = FirstNear(palmPos, 0.032f); }
            if (Held != null && WristPosition(out var w))
            {
                // Follow the wrist rigidly from where the object was grabbed: finger re-curls must not yank it.
                grip.MovePosition(w + hand.WristRotation * gripOffset);
                grip.MoveRotation(hand.WristRotation);
            }
        }

        public bool Solid { get; private set; } = true;

        void SetSolid(bool solid)
        {
            Solid = solid;
            if (!solid && Held != null) Release("glove stream stopped");
            foreach (var c in GetComponentsInChildren<Collider>()) c.enabled = solid;
        }

        void TryGrab()
        {
            if (!TipPosition(1, out var thumbPos)) return;
            var thumbBodies = BodiesNear(thumbPos);
            if (thumbBodies.Count == 0) return;

            gripFingers.Clear();
            Rigidbody target = null;
            for (int f = 2; f <= 5; f++)
            {
                if (!TipPosition(f, out var p)) continue;
                foreach (var rb in BodiesNear(p))
                {
                    if (!thumbBodies.Contains(rb) || (target != null && rb != target)) continue;
                    target = rb;
                    gripFingers.Add(f);
                }
            }
            if (target == null || GripCurl() < grabCurl) return;
            Grab(target);
        }

        float GripCurl()
        {
            float c = hand.Curl(1);
            foreach (int f in gripFingers) c = Mathf.Max(c, hand.Curl(f));
            return c;
        }

        bool WristPosition(out Vector3 p) => hand.TryGetPosition(hand.side + "Hand", out p);

        void Grab(Rigidbody target)
        {
            Held = target;
            grip.position = GripPoint();
            grip.rotation = hand.WristRotation;
            gripOffset = WristPosition(out var w) ? Quaternion.Inverse(hand.WristRotation) * (grip.position - w) : Vector3.zero;
            foreach (var mine in GetComponentsInChildren<Collider>())
                foreach (var theirs in target.GetComponentsInChildren<Collider>())
                {
                    Physics.IgnoreCollision(mine, theirs, true);
                    ignored.Add(theirs);
                }
            joint = target.gameObject.AddComponent<FixedJoint>();
            joint.connectedBody = grip;
            joint.breakForce = breakForce;
            GameHud.Toast($"{hand.side.ToLower()} hand grabbed the {NameOf(target)}", 1.5f);
            CookEvents.Emit("grab", NameOf(target), hand.side);
            Grabbed?.Invoke(this, target);
        }

        void MaybeRelease()
        {
            if (GripCurl() < releaseCurl) Release("fingers opened");
        }

        public void Release(string why = "released")
        {
            var was = Held;
            if (joint != null) Destroy(joint);
            joint = null;
            Held = null;
            foreach (var mine in GetComponentsInChildren<Collider>())
                foreach (var theirs in ignored)
                    if (theirs != null) Physics.IgnoreCollision(mine, theirs, false);
            ignored.Clear();
            gripFingers.Clear();
            if (was != null)
            {
                CookEvents.Emit("release", NameOf(was), $"{hand.side} {why}");
                Released?.Invoke(this, was);
            }
        }

        Vector3 GripPoint()
        {
            TipPosition(1, out var thumb);
            Vector3 sum = thumb;
            int n = 1;
            foreach (int f in gripFingers)
                if (TipPosition(f, out var p)) { sum += p; n++; }
            return sum / n;
        }

        Rigidbody FirstNear(Vector3 p, float radius)
        {
            int count = Physics.OverlapSphereNonAlloc(p, radius, hits, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                var rb = hits[i].attachedRigidbody;
                if (rb != null && !rb.isKinematic && rb.GetComponentInParent<KitchenItem>() != null) return rb;
            }
            return null;
        }

        List<Rigidbody> BodiesNear(Vector3 p)
        {
            var found = new List<Rigidbody>();
            int count = Physics.OverlapSphereNonAlloc(p, touchRadius, hits, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                var rb = hits[i].attachedRigidbody;
                if (rb == null || rb.isKinematic || found.Contains(rb)) continue;
                if (rb.GetComponentInParent<KitchenItem>() == null) continue;
                found.Add(rb);
            }
            return found;
        }

        public static string NameOf(Rigidbody rb)
        {
            var item = rb.GetComponentInParent<KitchenItem>();
            return item != null ? item.displayName : rb.name;
        }
    }
}
