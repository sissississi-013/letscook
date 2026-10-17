using UnityEngine;

namespace LetsCook
{
    /// A stove burner. B toggles it; the ring glows when lit.
    public class Burner : MonoBehaviour
    {
        public bool on = true;
        public float level = 1f;         // 0..1
        public float maxTemp = 260f;     // pan equilibrium at full power
        public Renderer ring;
        static Burner instance;

        public float TargetTemp => on ? Mathf.Lerp(FoodState.Ambient, maxTemp, level) : FoodState.Ambient;

        void OnEnable() => instance = this;

        public static Burner Under(Vector3 p)
        {
            if (instance == null || !instance.on) return null;
            var b = instance.transform.position;
            bool over = new Vector2(p.x - b.x, p.z - b.z).magnitude < 0.09f && p.y > b.y - 0.01f && p.y < b.y + 0.06f;
            return over ? instance : null;
        }

        void Update()
        {
            if (ToolRequester.Typing) return;
            if (Input.GetKeyDown(KeyCode.B)) { on = !on; GameHud.Toast(on ? "burner on" : "burner off", 1.2f); }
            if (Input.GetKeyDown(KeyCode.Equals)) { level = Mathf.Clamp01(level + 0.25f); GameHud.Toast($"heat {level * 100:0}%", 1f); }
            if (Input.GetKeyDown(KeyCode.Minus)) { level = Mathf.Clamp01(level - 0.25f); GameHud.Toast($"heat {level * 100:0}%", 1f); }
            if (ring != null)
            {
                var m = ring.material;
                m.color = on ? Color.Lerp(new Color(0.35f, 0.08f, 0.05f), new Color(1f, 0.3f, 0.05f), level) : new Color(0.2f, 0.2f, 0.2f);
            }
        }
    }
}
