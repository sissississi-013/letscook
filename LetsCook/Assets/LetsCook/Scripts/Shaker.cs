using UnityEngine;

namespace LetsCook
{
    /// A salt or pepper shaker. Tip it upside down over food and seasoning falls onto whatever is below;
    /// shaking it (fast changes in velocity) makes more come out, like the real thing.
    public class Shaker : MonoBehaviour
    {
        public string seasoning = "salt";
        public float tippedFlow = 0.15f;   // g/s while upside down
        public float shakeFlow = 1.2f;     // extra g/s at a vigorous shake

        Rigidbody rb;
        Vector3 lastVelocity;
        ParticleSystem grains;
        float pendingGrams;

        public float GramsDispensed { get; private set; }

        void Awake()
        {
            rb = GetComponent<Rigidbody>();
            grains = MakeGrains(seasoning == "pepper" ? new Color(0.15f, 0.12f, 0.1f) : Color.white);
        }

        void FixedUpdate()
        {
            Vector3 v = rb != null ? rb.linearVelocity : Vector3.zero;
            float accel = (v - lastVelocity).magnitude / Time.fixedDeltaTime;
            lastVelocity = v;

            float tilt = Vector3.Dot(transform.up, Vector3.down);   // 1 = fully upside down
            if (tilt < 0.3f) return;
            float shake = Mathf.Clamp01((accel - 2f) / 10f);
            float grams = (tippedFlow * tilt + shakeFlow * shake) * Time.fixedDeltaTime;
            Pour(grams);
        }

        /// Seasoning leaves the cap and lands on the first food straight below (if any).
        public void Pour(float grams)
        {
            if (grams <= 0f) return;
            GramsDispensed += grams;
            pendingGrams += grams;
            Vector3 cap = transform.position + transform.up * 0.055f;   // just past the cap
            if (grains != null) { grains.transform.position = cap; grains.Emit(Mathf.Max(1, Mathf.RoundToInt(grams * 60f))); }

            if (Physics.Raycast(cap + Vector3.down * 0.01f, Vector3.down, out var hit, 0.6f))
            {
                var food = hit.collider.GetComponentInParent<Food>();
                if (food != null)
                {
                    food.Season(seasoning, grams);
                    if (pendingGrams > 0.25f) { CookEvents.Emit("season", food.ingredient, seasoning); pendingGrams = 0f; }
                }
            }
        }

        ParticleSystem MakeGrains(Color color)
        {
            var go = new GameObject(seasoning + " grains");
            go.transform.SetParent(transform, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.playOnAwake = false;
            main.loop = false;
            main.startSize = 0.004f;
            main.startSpeed = 0.05f;
            main.startLifetime = 0.6f;
            main.gravityModifier = 1f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startColor = color;
            var emission = ps.emission;
            emission.rateOverTime = 0f;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.006f;
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = Kitchen.Mat("grain_" + seasoning, color, 0.2f);
            return ps;
        }
    }
}
