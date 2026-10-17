using UnityEngine;

namespace LetsCook
{
    /// A whole egg cracks when it hits something hard fast enough (or meets a knife): the shell splits off and
    /// the white + yolk spill onto whatever is below it (pan, bowl, counter) as a new piece of food that cooks.
    [RequireComponent(typeof(Food))]
    public class EggCracker : MonoBehaviour
    {
        public float crackSpeed = 0.7f;   // m/s impact against a hard surface (a real egg cracks on a sharp rap)

        Food food;
        bool cracked;

        void Awake() => food = GetComponent<Food>();

        void OnCollisionEnter(Collision c)
        {
            if (cracked || food.cut != "whole") return;
            if (c.rigidbody != null && c.rigidbody.isKinematic) return;          // fingers don't crack it
            if (c.collider.GetComponentInParent<Food>() != null) return;          // soft food doesn't either
            if (c.relativeVelocity.magnitude >= crackSpeed) Crack(c.GetContact(0).point);
        }

        /// Returns the spilled contents.
        public Food Crack(Vector3 at)
        {
            if (cracked) return null;
            cracked = true;
            var t = transform;

            // Spill onto the first solid surface below the egg (not food, not the hands, not the egg itself).
            float floorY = Kitchen.CounterTop;
            string surface = "";
            var hits = Physics.RaycastAll(t.position + Vector3.up * 0.02f, Vector3.down, 1f);
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            foreach (var hit in hits)
            {
                if (hit.collider.transform.IsChildOf(t) || hit.collider.GetComponentInParent<Food>() != null) continue;
                if (hit.rigidbody != null && hit.rigidbody.isKinematic) continue;
                floorY = hit.point.y;
                var below = hit.collider.GetComponentInParent<KitchenItem>();
                if (below != null) surface = "into the " + below.displayName;
                break;
            }

            var white = Kitchen.Sphere(t.parent, "Egg (cracked)", new Vector3(t.position.x, floorY + 0.008f, t.position.z),
                new Vector3(0.11f, 0.012f, 0.09f), Kitchen.Mat("eggWhite", new Color(0.95f, 0.92f, 0.8f), 0.9f));
            var yolk = Kitchen.Sphere(white.transform, "Yolk", white.transform.position + Vector3.up * 0.006f,
                Vector3.one * 0.032f, Kitchen.Mat("yolk", new Color(1f, 0.72f, 0.1f), 0.85f));
            yolk.transform.localScale = new Vector3(0.32f, 1.6f, 0.38f);   // relative to the flat white
            Object.Destroy(yolk.GetComponent<Collider>());

            Kitchen.Physical(white, 0.05f);
            var item = white.AddComponent<KitchenItem>();
            item.displayName = "egg";
            item.kind = ItemKind.Ingredient;
            var contents = white.AddComponent<Food>();
            contents.ingredient = "egg";
            contents.cut = "cracked";
            contents.state = new FoodState(IngredientProfile.For("egg_open")) { surfaceTemp = food.state.surfaceTemp, coreTemp = food.state.coreTemp };

            // Two shell halves fall away (not food, just mess).
            var shellMat = Kitchen.Mat("eggshell", new Color(0.96f, 0.9f, 0.8f), 0.4f);
            for (int i = -1; i <= 1; i += 2)
            {
                var shell = Kitchen.Sphere(t.parent, "Eggshell", t.position + new Vector3(i * 0.03f, 0.01f, 0f), new Vector3(0.04f, 0.03f, 0.04f), shellMat);
                Kitchen.Physical(shell, 0.006f).linearVelocity = new Vector3(i * 0.3f, 0.4f, 0f);
            }

            CookEvents.Emit("crack", "egg", surface);
            GameHud.Toast("cracked the egg", 1.2f);
            Destroy(gameObject);
            return contents;
        }
    }
}
