using UnityEngine;

namespace LetsCook
{
    /// The cutting part of a knife. A fast enough hit splits food in two across the blade's plane.
    /// Primitive split: the piece is halved along whichever of its axes lines up best with the blade normal.
    public class KnifeEdge : MonoBehaviour
    {
        public float minSpeed = 0.3f;          // m/s relative speed needed to cut
        public float minThickness = 0.008f;    // don't cut pieces thinner than this
        public static int MaxPieces = 40;

        /// The blade's thinnest axis is its normal, whatever way the part was modelled.
        public Vector3 BladeNormal
        {
            get
            {
                Vector3 s = transform.lossyScale;
                if (Mathf.Abs(s.x) <= Mathf.Abs(s.y) && Mathf.Abs(s.x) <= Mathf.Abs(s.z)) return transform.right;
                return Mathf.Abs(s.y) <= Mathf.Abs(s.z) ? transform.up : transform.forward;
            }
        }

        void OnCollisionEnter(Collision c) => TryCut(c.collider, c.relativeVelocity.magnitude);

        public bool TryCut(Collider other, float speed)
        {
            var food = other.GetComponentInParent<Food>();
            if (food == null || speed < minSpeed) return false;
            if (food.TryGetComponent<EggCracker>(out var egg)) return egg.Crack(food.transform.position) != null;
            return Cut(food, BladeNormal) != null;
        }

        /// Returns the new piece, or null if the food can't be cut further.
        public Food Cut(Food food, Vector3 bladeNormal)
        {
            if (Food.All.Count >= MaxPieces || Time.time < food.cutCooldownUntil) return null;

            // Pick the food's local axis most aligned with the blade normal.
            var t = food.transform;
            Vector3[] axes = { t.right, t.up, t.forward };
            int axis = 0;
            float best = -1f;
            for (int i = 0; i < 3; i++)
            {
                float d = Mathf.Abs(Vector3.Dot(axes[i], bladeNormal.normalized));
                if (d > best) { best = d; axis = i; }
            }
            Vector3 scale = t.localScale;
            if (scale[axis] * 0.5f < minThickness) return null;

            scale[axis] *= 0.5f;
            Vector3 offset = axes[axis] * (scale[axis] * 0.5f + 0.001f);
            Vector3 center = t.position;

            var piece = Instantiate(food.gameObject, center - offset, t.rotation, t.parent).GetComponent<Food>();
            t.position = center + offset;
            t.localScale = scale;
            piece.transform.localScale = scale;

            if (food.cut == "whole" && (food.ingredient == "tomato" || food.ingredient == "onion"))
            {
                // Round food: give both halves a dome + a flesh-coloured cut face pointing at the cut.
                var flesh = Kitchen.Mat("flesh_" + food.ingredient, food.ingredient == "tomato" ? new Color(0.93f, 0.38f, 0.3f) : new Color(0.96f, 0.93f, 0.84f), 0.7f);
                ShowHalf(food, axis, false, flesh);    // food sits on the +axis side: its cut faces -axis
                ShowHalf(piece, axis, true, flesh);    // piece sits on the -axis side: its cut faces +axis
            }
            string cut = NextCut(food.cut);
            food.cut = piece.cut = cut;
            piece.state = food.state.Clone();
            food.cutCooldownUntil = piece.cutCooldownUntil = Time.time + 0.25f;
            piece.name = food.name;

            if (food.TryGetComponent<Rigidbody>(out var rb))
            {
                rb.mass *= 0.5f;
                var prb = piece.GetComponent<Rigidbody>();
                prb.mass = rb.mass;
                prb.linearVelocity = rb.linearVelocity - offset.normalized * 0.05f;
                rb.linearVelocity += offset.normalized * 0.05f;
            }
            CookEvents.Emit("cut", food.ingredient, cut);
            GameHud.Toast($"sliced the {food.ingredient} ({cut})", 1f);
            return piece;
        }

        static void ShowHalf(Food f, int axis, bool cutFacesPositive, Material flesh)
        {
            var mf = f.GetComponent<MeshFilter>();
            var r = f.GetComponent<Renderer>();
            if (mf == null || r == null) return;
            mf.sharedMesh = Shapes.HalfRound(axis, cutFacesPositive);
            r.sharedMaterials = new[] { r.sharedMaterials[0], flesh };
            foreach (Transform child in f.transform) if (child.name == "Stem") child.gameObject.SetActive(false);
        }

        static string NextCut(string cut)
        {
            switch (cut)
            {
                case "whole": return "halves";
                case "halves": return "quarters";
                case "quarters": return "chunks";
                case "chunks": return "small pieces";
                default: return "minced";
            }
        }
    }
}
