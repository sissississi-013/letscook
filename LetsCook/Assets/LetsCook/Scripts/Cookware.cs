using UnityEngine;

namespace LetsCook
{
    /// Something that holds heat (the pan). Heats up while sitting on a lit burner, cools in air.
    public class Cookware : MonoBehaviour
    {
        public float temperature = FoodState.Ambient;
        public float heatRate = 0.02f;   // 1/s toward the burner's target (about a 1–2 minute preheat, like real cast iron)
        public float coolRate = 0.004f;
        public float oil;                 // grams of oil in the pan
        public const float SmokePoint = 230f;   // typical refined vegetable oil smokes around 230 °C

        Transform oilLayer;
        bool smoking;

        /// How much better heat flows into food in this pan (1 = dry, up to 1.8 with a good coating).
        public float ContactBoost => 1f + Mathf.Clamp01(oil / 10f) * 0.8f;
        public bool Smoking => smoking;

        public void AddOil(float grams)
        {
            oil = Mathf.Min(oil + grams, 200f);
        }

        void FixedUpdate()
        {
            float dt = Time.fixedDeltaTime * Food.TimeScale;
            var burner = Burner.Under(transform.position);
            float target = burner != null ? burner.TargetTemp : FoodState.Ambient;
            float rate = burner != null ? heatRate : coolRate;
            temperature += (target - temperature) * (1f - Mathf.Exp(-rate * dt));

            if (oil > 1f && temperature > SmokePoint && !smoking)
            {
                smoking = true;
                CookEvents.Emit("smoke", "oil", $"{temperature:0} °C");
                GameHud.Toast("the oil is smoking! turn the heat down (-)", 3f);
            }
            else if (temperature < SmokePoint - 15f) smoking = false;
            if (oil > 1f && temperature > 120f) oil = Mathf.Max(0f, oil - 0.002f * dt);   // a little evaporates/absorbs
        }

        void Update()
        {
            if (oil <= 0.5f) { if (oilLayer != null) oilLayer.gameObject.SetActive(false); return; }
            if (oilLayer == null)
            {
                var go = Kitchen.Disc(transform, "Oil", transform.position, 0.11f, 0.002f, Kitchen.Mat("oil", new Color(0.95f, 0.78f, 0.25f), 0.98f));
                Destroy(go.GetComponent<Collider>());
                oilLayer = go.transform;
            }
            oilLayer.gameObject.SetActive(true);
            float r = Mathf.Lerp(0.05f, 0.11f, Mathf.Clamp01(oil / 15f));
            oilLayer.position = transform.position + transform.up * 0.0055f;
            oilLayer.rotation = transform.rotation;
            oilLayer.localScale = new Vector3(r * 2f / transform.lossyScale.x, 0.001f, r * 2f / transform.lossyScale.z);
        }
    }
}
