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

        /// Extrude a convex 2D outline (counter-clockwise, unit box space -0.5..0.5) into a solid with flat-shaded
        /// sides. axes: which local axes the outline's (a, b) map to; the remaining axis is the thickness.
        public static Mesh Extrude(string name, IList<Vector2> outline, int axisA, int axisB)
        {
            if (cache.TryGetValue(name, out var cached) && cached != null) return cached;
            int axisC = 3 - axisA - axisB;
            Vector3 P(Vector2 p, float c) { var v = Vector3.zero; v[axisA] = p.x; v[axisB] = p.y; v[axisC] = c; return v; }
            // Orientation: does (A × B) point along +C? If not, flip the winding so faces point outwards.
            var axA = Vector3.zero; axA[axisA] = 1; var axB = Vector3.zero; axB[axisB] = 1; var axC = Vector3.zero; axC[axisC] = 1;
            bool flip = Vector3.Dot(Vector3.Cross(axA, axB), axC) > 0;   // Unity is left-handed: clockwise faces are front faces

            var verts = new List<Vector3>(); var uvs = new List<Vector2>(); var tris = new List<int>();
            void Tri(int a, int b, int c) { if (flip) tris.AddRange(new[] { a, c, b }); else tris.AddRange(new[] { a, b, c }); }
            Vector2 centre = Vector2.zero; foreach (var p in outline) centre += p; centre /= outline.Count;
            foreach (float c in new[] { 0.5f, -0.5f })
            {
                int start = verts.Count;
                verts.Add(P(centre, c)); uvs.Add(centre + Vector2.one * 0.5f);
                foreach (var p in outline) { verts.Add(P(p, c)); uvs.Add(p + Vector2.one * 0.5f); }
                for (int i = 0; i < outline.Count; i++)
                {
                    int a = start + 1 + i, b = start + 1 + (i + 1) % outline.Count;
                    if (c > 0) Tri(start, b, a); else Tri(start, a, b);
                }
            }
            for (int i = 0; i < outline.Count; i++)
            {
                var p0 = outline[i]; var p1 = outline[(i + 1) % outline.Count];
                int s0 = verts.Count;
                verts.Add(P(p0, 0.5f)); verts.Add(P(p1, 0.5f)); verts.Add(P(p1, -0.5f)); verts.Add(P(p0, -0.5f));
                float u0 = (float)i / outline.Count, u1 = (float)(i + 1) / outline.Count;
                uvs.Add(new Vector2(u0, 1)); uvs.Add(new Vector2(u1, 1)); uvs.Add(new Vector2(u1, 0)); uvs.Add(new Vector2(u0, 0));
                Tri(s0, s0 + 1, s0 + 2); Tri(s0, s0 + 2, s0 + 3);
            }
            var mesh = new Mesh { name = name };
            mesh.SetVertices(verts); mesh.SetUVs(0, uvs); mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals(); mesh.RecalculateTangents(); mesh.RecalculateBounds();
            cache[name] = mesh;
            return mesh;
        }

        /// Rounded rectangle filling the unit square, corner radius r (fraction of the side).
        public static List<Vector2> RoundedRect(float r, int perCorner = 5)
        {
            var pts = new List<Vector2>();
            var corners = new[] { new Vector2(0.5f - r, -0.5f + r), new Vector2(0.5f - r, 0.5f - r), new Vector2(-0.5f + r, 0.5f - r), new Vector2(-0.5f + r, -0.5f + r) };
            for (int c = 0; c < 4; c++)
                for (int i = 0; i <= perCorner; i++)
                {
                    float a = (c * 90f - 90f + 90f * i / perCorner) * Mathf.Deg2Rad;
                    pts.Add(corners[c] + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r);
                }
            return pts;
        }

        /// Sandwich-bread slice seen from above (outline in XZ): squared bottom, domed crown.
        public static Mesh BreadSlice()
        {
            var pts = new List<Vector2> { new Vector2(-0.45f, -0.5f), new Vector2(0.45f, -0.5f), new Vector2(0.5f, -0.45f), new Vector2(0.5f, 0.12f) };
            for (int i = 0; i <= 10; i++)
            {
                float a = Mathf.PI * i / 10f;   // the crown: two lobes like a real loaf top
                pts.Add(new Vector2(0.5f * Mathf.Cos(a), 0.12f + 0.38f * Mathf.Sin(a) * (1f - 0.08f * Mathf.Sin(2f * a))));
            }
            pts.Add(new Vector2(-0.5f, -0.45f));
            return Extrude("breadSlice", pts, 0, 2);
        }

        /// Butter block with softened edges (outline in XZ).
        public static Mesh ButterBlock() => Extrude("butter", RoundedRect(0.12f), 0, 2);

        /// Chef's knife blade (outline in XY, thin along Z): straight spine, curved belly, pointed tip on +X.
        public static Mesh KnifeBlade() => Extrude("knifeBlade", new List<Vector2>
        {
            new Vector2(-0.5f, -0.5f), new Vector2(0.15f, -0.5f), new Vector2(0.32f, -0.42f), new Vector2(0.43f, -0.25f),
            new Vector2(0.5f, 0.45f), new Vector2(0.42f, 0.5f), new Vector2(-0.5f, 0.5f),
        }, 0, 1);

        /// Glass hob: nearly square corners (outline in XZ).
        public static Mesh HobSlab() => Extrude("hobSlab", RoundedRect(0.07f), 0, 2);

        /// Rounded tool head/handle (outline in XZ).
        public static Mesh RoundedSlab() => Extrude("roundedSlab", RoundedRect(0.3f), 0, 2);

        /// Half of a round food after a cut, in the halved piece's unit space: dome on one side of local axis
        /// `axis`, a flat cut face on the other (submesh 1, for the flesh material). cutFacesPositive: the flat
        /// face points along +axis (true) or -axis (false).
        public static Mesh HalfRound(int axis, bool cutFacesPositive)
        {
            string name = $"half{axis}{(cutFacesPositive ? "+" : "-")}";
            if (cache.TryGetValue(name, out var cached) && cached != null) return cached;
            const int seg = 32, rings = 10;
            var verts = new List<Vector3>(); var tris0 = new List<int>(); var tris1 = new List<int>();
            // Canonical: flat face at y = -0.5 facing -y, dome up to y = +0.5 (the piece is half as thick, so the
            // hemisphere fills the whole unit box along y).
            for (int s2 = 0; s2 <= seg; s2++)
            {
                float a = s2 * Mathf.PI * 2f / seg;
                for (int r = 0; r <= rings; r++)
                {
                    float th = Mathf.PI * 0.5f * r / rings;
                    verts.Add(new Vector3(0.5f * Mathf.Sin(th) * Mathf.Cos(a), -0.5f + Mathf.Cos(th), 0.5f * Mathf.Sin(th) * Mathf.Sin(a)));
                }
            }
            for (int s2 = 0; s2 < seg; s2++)
                for (int r = 0; r < rings; r++)
                {
                    int i0 = s2 * (rings + 1) + r, i1 = (s2 + 1) * (rings + 1) + r;
                    tris0.AddRange(new[] { i0, i1, i0 + 1, i1, i1 + 1, i0 + 1 });
                }
            int centre = verts.Count;
            verts.Add(new Vector3(0f, -0.5f, 0f));
            int rim = verts.Count;
            for (int s2 = 0; s2 <= seg; s2++)
            {
                float a = s2 * Mathf.PI * 2f / seg;
                verts.Add(new Vector3(0.5f * Mathf.Cos(a), -0.5f, 0.5f * Mathf.Sin(a)));
            }
            for (int s2 = 0; s2 < seg; s2++) tris1.AddRange(new[] { centre, rim + s2, rim + s2 + 1 });

            // Rotate the canonical frame so -y becomes the cut direction.
            Vector3 cutDir = Vector3.zero; cutDir[axis] = cutFacesPositive ? 1f : -1f;
            var rot = Quaternion.FromToRotation(Vector3.down, cutDir);
            for (int i = 0; i < verts.Count; i++) verts[i] = rot * verts[i];

            var mesh = new Mesh { name = name, subMeshCount = 2 };
            mesh.SetVertices(verts);
            mesh.SetTriangles(tris0, 0);
            mesh.SetTriangles(tris1, 1);
            // Face the right way in Unity (clockwise = front): if the dome ended up inside-out, flip both submeshes.
            mesh.RecalculateNormals();
            var n = mesh.normals; var top = rot * new Vector3(0f, 0.5f, 0f);
            int topIndex = rings == 0 ? 0 : 0;   // vertex 0 is the dome's top in canonical space
            if (Vector3.Dot(n[topIndex], top) < 0f)
            {
                tris0.Reverse(); tris1.Reverse();
                mesh.SetTriangles(tris0, 0); mesh.SetTriangles(tris1, 1);
                mesh.RecalculateNormals();
            }
            mesh.RecalculateBounds();
            cache[name] = mesh;
            return mesh;
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
