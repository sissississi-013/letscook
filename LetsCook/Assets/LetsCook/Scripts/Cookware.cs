using UnityEngine;

namespace LetsCook
{
    /// Something that holds heat (the pan). Heats up while sitting on a lit burner, cools in air.
    public class Cookware : MonoBehaviour
    {
        public float temperature = FoodState.Ambient;
        public float heatRate = 0.02f;   // 1/s toward the burner's target (about a 1–2 minute preheat, like real cast iron)
        public float coolRate = 0.004f;

        void FixedUpdate()
        {
            float dt = Time.fixedDeltaTime * Food.TimeScale;
            var burner = Burner.Under(transform.position);
            float target = burner != null ? burner.TargetTemp : FoodState.Ambient;
            float rate = burner != null ? heatRate : coolRate;
            temperature += (target - temperature) * (1f - Mathf.Exp(-rate * dt));
        }
    }
}
