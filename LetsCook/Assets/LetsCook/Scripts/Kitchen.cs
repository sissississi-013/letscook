using System.Collections.Generic;
using UnityEngine;

namespace LetsCook
{
    /// Builds the first kitchen from primitives. Counter top is at y = 0.9 m; the player stands at -z looking +z.
    public static class Kitchen
    {
        public const float CounterTop = 0.9f;
        static readonly Dictionary<string, Material> materials = new Dictionary<string, Material>();

        public static Transform Build(Transform parent = null)
        {
            var root = new GameObject("Kitchen").transform;
            root.SetParent(parent, false);

            // Fixtures
            var counter = Box(root, "Counter", new Vector3(0f, CounterTop / 2f, 0.45f), new Vector3(1.6f, CounterTop, 0.7f), Mat("counter", new Color(0.72f, 0.69f, 0.64f), 0.45f));
            Tag(counter, "Counter", ItemKind.Fixture);
            Box(root, "Backsplash", new Vector3(0f, CounterTop + 0.35f, 0.82f), new Vector3(1.6f, 0.7f, 0.04f), Mat("tile", new Color(0.62f, 0.78f, 0.74f), 0.6f));
            Box(root, "Floor", new Vector3(0f, -0.01f, 0.2f), new Vector3(4f, 0.02f, 3f), Mat("floor", new Color(0.55f, 0.42f, 0.32f), 0.2f));

            var burner = Disc(root, "Burner", new Vector3(0.32f, CounterTop + 0.004f, 0.5f), 0.11f, 0.008f, Mat("burner", new Color(0.12f, 0.12f, 0.13f), 0.4f));
            Tag(burner, "Burner", ItemKind.Fixture);
            var ring = Disc(burner.transform, "Burner Ring", Vector3.zero, 0.075f, 0.002f, Mat("burnerRing", new Color(0.35f, 0.08f, 0.05f), 0.5f));
            ring.transform.localPosition = new Vector3(0f, 0.6f, 0f);
            Object.DestroyImmediate(ring.GetComponent<Collider>());
            burner.AddComponent<Burner>().ring = ring.GetComponent<Renderer>();

            // Tools and containers
            var board = Box(root, "Cutting Board", new Vector3(-0.17f, CounterTop + 0.011f, 0.42f), new Vector3(0.36f, 0.022f, 0.25f), Mat("wood", new Color(0.78f, 0.58f, 0.36f), 0.25f));
            Physical(board, 1.2f); Tag(board, "cutting board", ItemKind.Container);

            Physical(Pan(root, new Vector3(0.32f, CounterTop + 0.02f, 0.5f)), 1.0f);
            Physical(Bowl(root, new Vector3(0.02f, CounterTop + 0.005f, 0.66f)), 0.5f);

            var plate = Disc(root, "Plate", new Vector3(-0.5f, CounterTop + 0.006f, 0.6f), 0.13f, 0.012f, Mat("ceramic", new Color(0.97f, 0.97f, 0.95f), 0.8f));
            Physical(plate, 0.5f); Tag(plate, "plate", ItemKind.Container);

            Physical(Knife(root, new Vector3(-0.17f, CounterTop + 0.03f, 0.27f)), 0.15f);
            Physical(Spatula(root, new Vector3(0.12f, CounterTop + 0.012f, 0.3f)), 0.08f);

            // Ingredients
            Ingredient(Sphere(root, "Tomato", new Vector3(-0.25f, CounterTop + 0.06f, 0.45f), new Vector3(0.07f, 0.062f, 0.07f), Mat("tomato", new Color(0.86f, 0.16f, 0.1f), 0.75f)), "tomato", 0.12f);
            Ingredient(Sphere(root, "Onion", new Vector3(-0.1f, CounterTop + 0.06f, 0.47f), new Vector3(0.075f, 0.068f, 0.075f), Mat("onion", new Color(0.82f, 0.62f, 0.34f), 0.5f)), "onion", 0.15f);
            Ingredient(Sphere(root, "Egg", new Vector3(-0.38f, CounterTop + 0.03f, 0.72f), new Vector3(0.044f, 0.058f, 0.044f), Mat("egg", new Color(0.98f, 0.93f, 0.84f), 0.55f)), "egg", 0.06f);
            Ingredient(Box(root, "Butter", new Vector3(-0.28f, CounterTop + 0.016f, 0.72f), new Vector3(0.07f, 0.032f, 0.04f), Mat("butter", new Color(1f, 0.9f, 0.45f), 0.6f)), "butter", 0.06f);
            Ingredient(Box(root, "Bread", new Vector3(-0.55f, CounterTop + 0.01f, 0.4f), new Vector3(0.11f, 0.018f, 0.1f), Mat("bread", new Color(0.93f, 0.8f, 0.55f), 0.1f)), "bread", 0.04f);

            return root;
        }

        // --- composite tools -------------------------------------------------

        static GameObject Pan(Transform root, Vector3 pos)
        {
            var pan = new GameObject("Pan");
            pan.transform.SetParent(root, false);
            pan.transform.position = pos;
            var iron = Mat("iron", new Color(0.2f, 0.2f, 0.22f), 0.55f);
            Disc(pan.transform, "Pan Base", pos, 0.12f, 0.008f, iron);
            Ring(pan.transform, pos + Vector3.up * 0.018f, 0.12f, 0.035f, 0.006f, 16, iron);
            var handle = Box(pan.transform, "Pan Handle", pos + new Vector3(0.2f, 0.022f, -0.02f), new Vector3(0.17f, 0.014f, 0.024f), Mat("handle", new Color(0.15f, 0.1f, 0.08f), 0.3f));
            handle.transform.rotation = Quaternion.Euler(0f, 10f, 8f);
            Tag(pan, "pan", ItemKind.Container);
            pan.AddComponent<Cookware>();
            return pan;
        }

        static GameObject Bowl(Transform root, Vector3 pos)
        {
            var bowl = new GameObject("Bowl");
            bowl.transform.SetParent(root, false);
            bowl.transform.position = pos;
            var mat = Mat("bowl", new Color(0.36f, 0.55f, 0.82f), 0.75f);
            Disc(bowl.transform, "Bowl Base", pos, 0.06f, 0.01f, mat);
            Ring(bowl.transform, pos + Vector3.up * 0.04f, 0.085f, 0.07f, 0.008f, 14, mat, 18f);
            Tag(bowl, "bowl", ItemKind.Container);
            return bowl;
        }

        static GameObject Knife(Transform root, Vector3 pos)
        {
            var knife = new GameObject("Knife");
            knife.transform.SetParent(root, false);
            knife.transform.position = pos;
            var blade = Box(knife.transform, "Knife Blade", pos + new Vector3(0.07f, 0f, 0f), new Vector3(0.18f, 0.035f, 0.003f), Mat("steel", new Color(0.82f, 0.84f, 0.88f), 0.9f));
            blade.AddComponent<KnifeEdge>();
            Box(knife.transform, "Knife Handle", pos + new Vector3(-0.08f, 0f, 0f), new Vector3(0.11f, 0.022f, 0.02f), Mat("handle", new Color(0.15f, 0.1f, 0.08f), 0.3f));
            knife.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            Tag(knife, "knife", ItemKind.Tool);
            return knife;
        }

        static GameObject Spatula(Transform root, Vector3 pos)
        {
            var spatula = new GameObject("Spatula");
            spatula.transform.SetParent(root, false);
            spatula.transform.position = pos;
            Box(spatula.transform, "Spatula Head", pos + new Vector3(0f, 0f, 0.1f), new Vector3(0.075f, 0.004f, 0.09f), Mat("silicone", new Color(0.95f, 0.45f, 0.35f), 0.3f));
            Box(spatula.transform, "Spatula Handle", pos + new Vector3(0f, 0.004f, -0.04f), new Vector3(0.02f, 0.014f, 0.2f), Mat("handle", new Color(0.15f, 0.1f, 0.08f), 0.3f));
            Tag(spatula, "spatula", ItemKind.Tool);
            return spatula;
        }

        // --- primitives --------------------------------------------------------

        public static GameObject Box(Transform parent, string name, Vector3 pos, Vector3 size, Material mat)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Place(go, parent, name, pos, size, mat);
            return go;
        }

        public static GameObject Sphere(Transform parent, string name, Vector3 pos, Vector3 size, Material mat)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Place(go, parent, name, pos, size, mat);
            return go;
        }

        public static GameObject Disc(Transform parent, string name, Vector3 pos, float radius, float height, Material mat)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Place(go, parent, name, pos, new Vector3(radius * 2f, height * 0.5f, radius * 2f), mat);
            Object.DestroyImmediate(go.GetComponent<Collider>()); // primitive cylinders get capsule colliders
            var mc = go.AddComponent<MeshCollider>();
            mc.convex = true;
            return go;
        }

        /// A ring of thin boxes: walls for pans and bowls (concave containers need several convex pieces).
        public static void Ring(Transform parent, Vector3 center, float radius, float height, float thickness, int segments, Material mat, float flare = 0f)
        {
            float seg = 2f * Mathf.PI * radius / segments * 1.08f;
            for (int i = 0; i < segments; i++)
            {
                float a = i * 360f / segments;
                var dir = Quaternion.Euler(0f, a, 0f) * Vector3.forward;
                var wall = Box(parent, "Wall", center + dir * radius, new Vector3(seg, height, thickness), mat);
                wall.transform.rotation = Quaternion.LookRotation(dir) * Quaternion.Euler(-flare, 0f, 0f);
            }
        }

        static void Place(GameObject go, Transform parent, string name, Vector3 pos, Vector3 size, Material mat)
        {
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            go.transform.localScale = size;
            go.GetComponent<Renderer>().sharedMaterial = mat;
        }

        public static Rigidbody Physical(GameObject go, float mass)
        {
            if (!go.TryGetComponent<Rigidbody>(out var rb)) rb = go.AddComponent<Rigidbody>();
            rb.mass = mass;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            rb.maxDepenetrationVelocity = 0.4f;   // a finger appearing inside food nudges it instead of launching it
            return rb;
        }

        static void Ingredient(GameObject go, string name, float mass)
        {
            Physical(go, mass);
            Tag(go, name, ItemKind.Ingredient);
            go.AddComponent<Food>().Init(name);
            if (name == "egg") go.AddComponent<EggCracker>();
        }

        static void Tag(GameObject go, string name, ItemKind kind)
        {
            var item = go.AddComponent<KitchenItem>();
            item.displayName = name;
            item.kind = kind;
        }

        public static Material Mat(string key, Color color, float gloss)
        {
            if (materials.TryGetValue(key, out var m) && m != null) return m;
            m = new Material(Shader.Find("Standard")) { name = key, color = color };
            m.SetFloat("_Glossiness", gloss);
            materials[key] = m;
            return m;
        }
    }
}
