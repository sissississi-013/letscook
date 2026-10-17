using System;
using System.Collections.Generic;
using UnityEngine;

namespace LetsCook
{
    [Serializable] public class ToolPart { public string role, shape, material; public float[] size, offset, rotation; }
    [Serializable] public class ToolSpec { public string name; public float mass_kg; public string[] abilities; public ToolPart[] parts; }
    [Serializable] public class ToolResponse { public string source; public ToolSpec tool; }

    /// What a built tool can do (for logging, robot labels and future behaviours).
    public class ToolInfo : MonoBehaviour
    {
        public string source;
        public string[] abilities = new string[0];
        public bool Can(string ability) => Array.IndexOf(abilities, ability) >= 0;
    }

    /// Assembles a physical tool from a part list (from the library or Claude). Tool space: handle along +z, y up.
    public static class ToolBuilder
    {
        static readonly Dictionary<string, (Color color, float gloss)> Materials = new Dictionary<string, (Color, float)>
        {
            { "steel", (new Color(0.82f, 0.84f, 0.88f), 0.9f) },
            { "iron", (new Color(0.2f, 0.2f, 0.22f), 0.55f) },
            { "wood", (new Color(0.72f, 0.52f, 0.32f), 0.25f) },
            { "silicone", (new Color(0.95f, 0.45f, 0.35f), 0.3f) },
            { "ceramic", (new Color(0.95f, 0.95f, 0.92f), 0.8f) },
            { "plastic", (new Color(0.3f, 0.5f, 0.8f), 0.5f) },
            { "stone", (new Color(0.5f, 0.5f, 0.48f), 0.2f) },
            { "glass", (new Color(0.8f, 0.9f, 0.95f), 0.95f) },
            { "copper", (new Color(0.8f, 0.45f, 0.25f), 0.8f) },
        };

        public static GameObject Build(ToolSpec spec, Vector3 position, string source = "library", Transform parent = null)
        {
            var root = new GameObject(spec.name);
            root.transform.SetParent(parent, false);
            root.transform.position = position;

            foreach (var part in spec.parts ?? new ToolPart[0])
            {
                var go = Part(root.transform, part);
                if (go == null) continue;
                bool cuts = spec.abilities != null && Array.IndexOf(spec.abilities, "cuts") >= 0;
                if (part.role == "blade" && cuts) go.AddComponent<KnifeEdge>();
            }

            Kitchen.Physical(root, Mathf.Max(0.01f, spec.mass_kg));
            var item = root.AddComponent<KitchenItem>();
            item.displayName = spec.name;
            item.kind = ItemKind.Tool;
            var info = root.AddComponent<ToolInfo>();
            info.source = source;
            info.abilities = spec.abilities ?? new string[0];
            CookEvents.Emit("tool_spawned", spec.name, source);
            return root;
        }

        static GameObject Part(Transform root, ToolPart p)
        {
            Vector3 size = V(p.size, new Vector3(0.05f, 0.05f, 0.05f));
            Vector3 pos = root.position + V(p.offset, Vector3.zero);
            Quaternion rot = Quaternion.Euler(V(p.rotation, Vector3.zero));
            var mat = Mat(p.material);
            string name = $"{root.name} {p.role}";
            GameObject go;
            switch (p.shape)
            {
                case "cylinder":
                    go = Kitchen.Disc(root, name, pos, size.x * 0.5f, size.y, mat);
                    go.transform.localScale = new Vector3(size.x, size.y * 0.5f, size.z);
                    break;
                case "sphere":
                    go = Kitchen.Sphere(root, name, pos, size, mat);
                    break;
                case "capsule":
                    go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                    go.name = name;
                    go.transform.SetParent(root, false);
                    go.transform.position = pos;
                    go.transform.localScale = new Vector3(size.x, size.y * 0.5f, size.z);
                    go.GetComponent<Renderer>().sharedMaterial = mat;
                    break;
                case "bowl":
                    go = new GameObject(name);
                    go.transform.SetParent(root, false);
                    go.transform.position = pos;
                    float r = Mathf.Max(size.x, size.z) * 0.5f;
                    Kitchen.Disc(go.transform, name + " base", pos + Vector3.up * 0.004f, r * 0.65f, 0.008f, mat);
                    Kitchen.Ring(go.transform, pos + Vector3.up * size.y * 0.5f, r, size.y, Mathf.Clamp(r * 0.08f, 0.004f, 0.012f), 12, mat, 15f);
                    break;
                default:
                    go = Kitchen.Box(root, name, pos, size, mat);
                    break;
            }
            go.transform.rotation = rot * go.transform.rotation;
            return go;
        }

        static Vector3 V(float[] a, Vector3 fallback) =>
            a != null && a.Length >= 3 ? new Vector3(a[0], a[1], a[2]) : fallback;

        static Material Mat(string material)
        {
            if (material == null || !Materials.TryGetValue(material, out var m)) m = Materials["steel"];
            return Kitchen.Mat("tool_" + (material ?? "steel"), m.color, m.gloss);
        }
    }
}
