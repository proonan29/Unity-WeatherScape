using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace WeatherScape.World
{
    /// <summary>Accumulates flat-shaded low-poly geometry. UVs are in metres (÷ uvScale) so facade textures tile per floor.</summary>
    public class MeshBuilder
    {
        public readonly List<Vector3> v = new List<Vector3>();
        public readonly List<Vector3> n = new List<Vector3>();
        public readonly List<Vector2> uv = new List<Vector2>();
        public readonly List<int> t = new List<int>();

        public int VertexCount => v.Count;

        public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector2 ua, Vector2 ub, Vector2 uc, Vector2 ud)
        {
            var nn = Vector3.Cross(b - a, c - a).normalized;
            int i = v.Count;
            v.Add(a); v.Add(b); v.Add(c); v.Add(d);
            n.Add(nn); n.Add(nn); n.Add(nn); n.Add(nn);
            uv.Add(ua); uv.Add(ub); uv.Add(uc); uv.Add(ud);
            t.Add(i); t.Add(i + 1); t.Add(i + 2); t.Add(i); t.Add(i + 2); t.Add(i + 3);
        }

        public void Tri(Vector3 a, Vector3 b, Vector3 c, Vector2 ua, Vector2 ub, Vector2 uc)
        {
            var nn = Vector3.Cross(b - a, c - a).normalized;
            int i = v.Count;
            v.Add(a); v.Add(b); v.Add(c);
            n.Add(nn); n.Add(nn); n.Add(nn);
            uv.Add(ua); uv.Add(ub); uv.Add(uc);
            t.Add(i); t.Add(i + 1); t.Add(i + 2);
        }

        public void Tri(Vector3 a, Vector3 b, Vector3 c) => Tri(a, b, c, new Vector2(a.x, a.z), new Vector2(b.x, b.z), new Vector2(c.x, c.z));

        /// <summary>Axis box (rotated by yaw around its centre). bottom = y of the base. UV: walls (along, up) in metres / uvScale.</summary>
        public void Box(Vector3 basePos, Vector3 size, float yaw = 0f, float uvScale = 1f, bool top = true, bool bottom = false)
        {
            var q = Quaternion.Euler(0, yaw, 0);
            float hx = size.x * 0.5f, hz = size.z * 0.5f, h = size.y;
            Vector3 P(float x, float y, float z) => basePos + q * new Vector3(x, y, z);
            Vector3 p000 = P(-hx, 0, -hz), p100 = P(hx, 0, -hz), p110 = P(hx, h, -hz), p010 = P(-hx, h, -hz);
            Vector3 p001 = P(-hx, 0, hz), p101 = P(hx, 0, hz), p111 = P(hx, h, hz), p011 = P(-hx, h, hz);
            float s = 1f / uvScale;
            float X = size.x * s, Z = size.z * s, Y = h * s;
            // front (-z)
            Quad(p000, p010, p110, p100, new Vector2(0, 0), new Vector2(0, Y), new Vector2(X, Y), new Vector2(X, 0));
            // back (+z)
            Quad(p101, p111, p011, p001, new Vector2(0, 0), new Vector2(0, Y), new Vector2(X, Y), new Vector2(X, 0));
            // left (-x)
            Quad(p001, p011, p010, p000, new Vector2(0, 0), new Vector2(0, Y), new Vector2(Z, Y), new Vector2(Z, 0));
            // right (+x)
            Quad(p100, p110, p111, p101, new Vector2(0, 0), new Vector2(0, Y), new Vector2(Z, Y), new Vector2(Z, 0));
            if (top) Quad(p010, p011, p111, p110, new Vector2(0, 0), new Vector2(0, Z), new Vector2(X, Z), new Vector2(X, 0));
            if (bottom) Quad(p000, p100, p101, p001, Vector2.zero, new Vector2(X, 0), new Vector2(X, Z), new Vector2(0, Z));
        }

        /// <summary>Gable roof over a w × d footprint (ridge along local X), eaves at y0, ridge at y0 + rise.</summary>
        public void Gable(Vector3 basePos, float w, float d, float rise, float yaw = 0f, float overhang = 0.3f)
        {
            var q = Quaternion.Euler(0, yaw, 0);
            float hx = w * 0.5f + overhang, hz = d * 0.5f + overhang;
            Vector3 P(float x, float y, float z) => basePos + q * new Vector3(x, y, z);
            Vector3 a = P(-hx, 0, -hz), b = P(hx, 0, -hz), c = P(hx, 0, hz), dd = P(-hx, 0, hz);
            Vector3 r0 = P(-hx, rise, 0), r1 = P(hx, rise, 0);
            float sl = Mathf.Sqrt(rise * rise + hz * hz);
            Quad(a, r0, r1, b, new Vector2(0, 0), new Vector2(0, sl), new Vector2(2 * hx, sl), new Vector2(2 * hx, 0));
            Quad(c, r1, r0, dd, new Vector2(0, 0), new Vector2(0, sl), new Vector2(2 * hx, sl), new Vector2(2 * hx, 0));
            // gable ends (inset to wall)
            float ix = w * 0.5f, iz = d * 0.5f;
            Vector3 e0 = P(-ix, 0, -iz), e1 = P(-ix, 0, iz), e2 = P(-ix, rise * (hz - overhang * 0f) / hz, 0);
            Tri(e1, P(-ix, rise, 0), e0);
            Tri(P(ix, 0, -iz), P(ix, rise, 0), P(ix, 0, iz));
        }

        /// <summary>Hip roof (four slopes) — good for Asian-style pavilions when given a large overhang.</summary>
        public void Hip(Vector3 basePos, float w, float d, float rise, float yaw = 0f, float overhang = 0.5f, float ridgeFrac = 0.4f)
        {
            var q = Quaternion.Euler(0, yaw, 0);
            float hx = w * 0.5f + overhang, hz = d * 0.5f + overhang, rx = hx * ridgeFrac;
            Vector3 P(float x, float y, float z) => basePos + q * new Vector3(x, y, z);
            Vector3 a = P(-hx, 0, -hz), b = P(hx, 0, -hz), c = P(hx, 0, hz), dd = P(-hx, 0, hz);
            Vector3 r0 = P(-rx, rise, 0), r1 = P(rx, rise, 0);
            Quad(a, r0, r1, b, Vector2.zero, new Vector2(0, hz), new Vector2(2 * hx, hz), new Vector2(2 * hx, 0));
            Quad(c, r1, r0, dd, Vector2.zero, new Vector2(0, hz), new Vector2(2 * hx, hz), new Vector2(2 * hx, 0));
            Tri(dd, r0, a);
            Tri(b, r1, c);
        }

        public void Cylinder(Vector3 basePos, float r0, float r1, float h, int sides = 8, bool cap = true, float uvScale = 1f)
        {
            float circ = 2 * Mathf.PI * Mathf.Max(r0, r1) / uvScale;
            for (int i = 0; i < sides; i++)
            {
                float a0 = i * 2 * Mathf.PI / sides, a1 = (i + 1) * 2 * Mathf.PI / sides;
                Vector3 d0 = new Vector3(Mathf.Cos(a0), 0, Mathf.Sin(a0)), d1 = new Vector3(Mathf.Cos(a1), 0, Mathf.Sin(a1));
                Vector3 b0 = basePos + d0 * r0, b1 = basePos + d1 * r0, t0 = basePos + d0 * r1 + Vector3.up * h, t1 = basePos + d1 * r1 + Vector3.up * h;
                float u0 = circ * i / sides, u1 = circ * (i + 1) / sides;
                if (r1 > 0.0001f) Quad(b0, t0, t1, b1, new Vector2(u0, 0), new Vector2(u0, h / uvScale), new Vector2(u1, h / uvScale), new Vector2(u1, 0));
                else Tri(b0, t0, b1, new Vector2(u0, 0), new Vector2((u0 + u1) * 0.5f, h / uvScale), new Vector2(u1, 0));
                if (cap && r1 > 0.0001f) Tri(basePos + Vector3.up * h, t1, t0);
            }
        }

        public void Cone(Vector3 basePos, float r, float h, int sides = 8) => Cylinder(basePos, r, 0f, h, sides, false);

        /// <summary>Low-poly blob (octahedron subdivided once), squashed by scale.</summary>
        public void Blob(Vector3 center, Vector3 scale, int seed = 0)
        {
            Vector3[] o = { Vector3.up, Vector3.down, Vector3.left, Vector3.right, Vector3.forward, Vector3.back };
            int[,] f = { { 0, 4, 3 }, { 0, 3, 5 }, { 0, 5, 2 }, { 0, 2, 4 }, { 1, 3, 4 }, { 1, 5, 3 }, { 1, 2, 5 }, { 1, 4, 2 } };
            var rng = new System.Random(seed);
            Vector3 J(Vector3 p) { float k = 1f + (float)(rng.NextDouble() - 0.5) * 0.18f; return Vector3.Scale(p.normalized * k, scale) + center; }
            for (int i = 0; i < 8; i++)
            {
                Vector3 a = o[f[i, 0]], b = o[f[i, 1]], c = o[f[i, 2]];
                Vector3 ab = (a + b).normalized, bc = (b + c).normalized, ca = (c + a).normalized;
                Vector3 A = J(a), B = J(b), C = J(c), AB = J(ab), BC = J(bc), CA = J(ca);
                Tri(A, AB, CA); Tri(AB, B, BC); Tri(CA, BC, C); Tri(AB, BC, CA);
            }
        }

        public void Append(MeshBuilder other, Matrix4x4 m)
        {
            int baseI = v.Count;
            for (int i = 0; i < other.v.Count; i++) { v.Add(m.MultiplyPoint3x4(other.v[i])); n.Add(m.MultiplyVector(other.n[i]).normalized); uv.Add(other.uv[i]); }
            foreach (var i in other.t) t.Add(baseI + i);
        }

        public Mesh ToMesh(string name)
        {
            var m = new Mesh { name = name, indexFormat = v.Count > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16 };
            m.SetVertices(v); m.SetNormals(n); m.SetUVs(0, uv); m.SetTriangles(t, 0);
            m.RecalculateBounds();
            return m;
        }

        public void Clear() { v.Clear(); n.Clear(); uv.Clear(); t.Clear(); }
    }

    /// <summary>A mesh per material key; flushed into GameObjects under one parent.</summary>
    public class MeshBatch
    {
        readonly Dictionary<string, MeshBuilder> parts = new Dictionary<string, MeshBuilder>();
        public MeshBuilder this[string key] { get { if (!parts.TryGetValue(key, out var b)) parts[key] = b = new MeshBuilder(); return b; } }

        public List<GameObject> Flush(Transform parent, string name, Mats mats, bool shadows = true)
        {
            var list = new List<GameObject>();
            foreach (var kv in parts)
            {
                if (kv.Value.VertexCount == 0) continue;
                var go = new GameObject($"{name}_{kv.Key}");
                go.transform.SetParent(parent, false);
                go.AddComponent<MeshFilter>().sharedMesh = kv.Value.ToMesh($"{name}_{kv.Key}");
                var r = go.AddComponent<MeshRenderer>();
                r.sharedMaterial = mats.Get(kv.Key);
                r.shadowCastingMode = shadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
                list.Add(go);
            }
            parts.Clear();
            return list;
        }
    }
}
