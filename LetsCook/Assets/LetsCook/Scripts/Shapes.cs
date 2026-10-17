using System;
using System.Collections.Generic;
using UnityEngine;

namespace LetsCook
{
    /// Smooth procedural meshes (turned on a lathe) that replace the primitive look while physics keeps its
    /// simple colliders. Meshes are built in the primitive's unit space so they drop into existing objects.
    public static class Shapes
    {
        static readonly Dictionary<string, Mesh> cache = new Dictionary<string, Mesh>();

        /// Revolve a profile of (radius, height) points around the y axis. radiusWobble(angle) shapes lobes.
        public static Mesh Lathe(string name, IList<Vector2> profile, int segments = 48, Func<float, float, float> radiusWobble = null)
        {
            if (cache.TryGetValue(name, out var cached) && cached != null) return cached;
            int rings = profile.Count;
            var verts = new List<Vector3>();
            var uvs = new List<Vector2>();
            for (int s = 0; s <= segments; s++)
            {
                float a = s * Mathf.PI * 2f / segments;
                float c = Mathf.Cos(a), sn = Mathf.Sin(a);
                for (int r = 0; r < rings; r++)
                {
                    float rad = profile[r].x * (radiusWobble != null ? radiusWobble(a, (float)r / (rings - 1)) : 1f);
                    verts.Add(new Vector3(rad * c, profile[r].y, rad * sn));
                    uvs.Add(new Vector2((float)s / segments, (float)r / (rings - 1)));
                }
            }
            var tris = new List<int>();
            for (int s = 0; s < segments; s++)
                for (int r = 0; r < rings - 1; r++)
                {
                    int i0 = s * rings + r, i1 = (s + 1) * rings + r;
                    tris.AddRange(new[] { i0, i0 + 1, i1, i1, i0 + 1, i1 + 1 });
                }
            var mesh = new Mesh { name = name };
            mesh.SetVertices(verts);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();
            cache[name] = mesh;
            return mesh;
        }

        static List<Vector2> Curve(int n, Func<float, Vector2> f)
        {
            var pts = new List<Vector2>();
            for (int i = 0; i <= n; i++) pts.Add(f((float)i / n));
            return pts;
        }

        // ---- food (unit sphere space: radius 0.5) -------------------------------------------------------

        /// Egg: blunt bottom, narrower top.
        public static Mesh Egg() => Lathe("egg", Curve(32, t =>
        {
            float th = Mathf.PI * t;                       // 0 = bottom pole, π = top pole
            float y = -Mathf.Cos(th) * 0.5f;
            float r = Mathf.Sin(th) * 0.5f * (1f - 0.18f * (y + 0.5f) * (y + 0.5f) * 1.2f + 0.08f);
            return new Vector2(Mathf.Max(0f, r), y);
        }));

        /// Tomato: squashed sphere with soft lobes and a dimple on top.
        public static Mesh Tomato() => Lathe("tomato", Curve(28, t =>
        {
            float th = Mathf.PI * t;
            float y = -Mathf.Cos(th) * 0.5f;
            if (t > 0.92f) y -= (t - 0.92f) * 0.6f;        // dimple where the stem sits
            return new Vector2(Mathf.Sin(th) * 0.5f, y);
        }), 64, (a, v) => 1f + 0.045f * Mathf.Cos(a * 5f) * Mathf.Sin(v * Mathf.PI));

        /// Onion: round bulb narrowing to a point on top.
        public static Mesh Onion() => Lathe("onion", Curve(28, t =>
        {
            float th = Mathf.PI * t;
            float y = -Mathf.Cos(th) * 0.5f;
            float r = Mathf.Sin(th) * 0.5f * (t > 0.6f ? Mathf.Lerp(1f, 0.35f, Mathf.SmoothStep(0f, 1f, (t - 0.6f) / 0.4f)) : 1f);
            return new Vector2(r, y);
        }));

        // ---- kitchenware (metres, origin at the base centre) ---------------------------------------------

        /// Frying pan: thick flat base, curved flaring wall, rolled rim. Outside and inside surfaces.
        public static Mesh Pan(float radius = 0.12f, float depth = 0.04f, float wall = 0.005f)
        {
            var outside = Curve(14, t =>
            {
                float r = Mathf.Lerp(radius - 0.025f, radius + 0.008f, Mathf.Sin(t * Mathf.PI * 0.5f));
                return new Vector2(t == 0 ? 0.001f : r, depth * t);
            });
            outside.Insert(0, new Vector2(0f, 0f));
            var inside = new List<Vector2>(outside);
            inside.Reverse();
            for (int i = 0; i < inside.Count; i++) inside[i] = new Vector2(Mathf.Max(0f, inside[i].x - wall), inside[i].y + (i == inside.Count - 1 ? wall * 1.5f : 0f));
            var profile = new List<Vector2>(outside);
            profile.Add(new Vector2(radius + 0.008f + 0.002f, depth + 0.002f));   // rolled lip
            profile.AddRange(inside);
            profile.Add(new Vector2(0f, wall * 1.5f));
            return Lathe($"pan{radius}", profile);
        }

        /// Mixing bowl: rounded, thin wall.
        public static Mesh Bowl(float radius = 0.09f, float depth = 0.075f, float wall = 0.004f)
        {
            var outside = Curve(16, t => new Vector2(Mathf.Lerp(radius * 0.55f, radius, Mathf.Sin(t * Mathf.PI * 0.5f)), depth * t));
            outside.Insert(0, new Vector2(0f, 0f));
            var inside = new List<Vector2>(outside);
            inside.Reverse();
            for (int i = 0; i < inside.Count; i++) inside[i] = new Vector2(Mathf.Max(0f, inside[i].x - wall), inside[i].y + wall);
            var profile = new List<Vector2>(outside);
            profile.AddRange(inside);
            return Lathe($"bowl{radius}", profile);
        }

        /// Dinner plate: low well and a raised rim (unit cylinder space: radius 0.5, height -1..1).
        public static Mesh Plate() => Lathe("plate", new List<Vector2>
        {
            new Vector2(0f, -1f), new Vector2(0.32f, -1f), new Vector2(0.38f, -0.7f), new Vector2(0.48f, 0.6f),
            new Vector2(0.5f, 1f), new Vector2(0.47f, 1f), new Vector2(0.4f, 0.2f), new Vector2(0.33f, -0.2f), new Vector2(0f, -0.2f),
        });

        /// Oil bottle: body, rounded shoulder, neck, cap (metres, centred like the old collider: body centre at 0).
        public static Mesh Bottle() => Lathe("bottle", new List<Vector2>
        {
            new Vector2(0f, -0.08f), new Vector2(0.03f, -0.08f), new Vector2(0.032f, -0.075f), new Vector2(0.032f, 0.05f),
            new Vector2(0.028f, 0.07f), new Vector2(0.016f, 0.09f), new Vector2(0.012f, 0.1f), new Vector2(0.012f, 0.12f),
            new Vector2(0.014f, 0.121f), new Vector2(0.014f, 0.135f), new Vector2(0f, 0.137f),
        });

        /// Shaker: rounded glass body with a domed metal top.
        public static Mesh Shaker() => Lathe("shaker", new List<Vector2>
        {
            new Vector2(0f, -0.0375f), new Vector2(0.02f, -0.0375f), new Vector2(0.022f, -0.033f), new Vector2(0.022f, 0.03f),
            new Vector2(0.02f, 0.042f), new Vector2(0.014f, 0.05f), new Vector2(0f, 0.052f),
        });
    }
}
