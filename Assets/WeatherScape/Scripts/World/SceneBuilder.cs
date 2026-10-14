using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using Random = UnityEngine.Random;

namespace WeatherScape.World
{
    /// <summary>Builds one of the six scenes procedurally (seeded, so the same place always looks the same).</summary>
    public static partial class SceneBuilder
    {
        public static BuiltScene Build(SceneKind kind, int seed, bool coastal, Transform parent, Mats mats)
        {
            var oldState = Random.state;
            Random.InitState(seed);
            var root = new GameObject($"Scene_{kind}").transform;
            root.SetParent(parent, false);
            var s = new BuiltScene { kind = kind, root = root, mats = mats };
            var props = new Props(mats, s);
            try
            {
                switch (kind)
                {
                    case SceneKind.Metropolis: Metropolis(s, props, coastal); break;
                    case SceneKind.Town: Town(s, props, coastal); break;
                    case SceneKind.Village: Village(s, props); break;
                    case SceneKind.Tourist: Tourist(s, props); break;
                    case SceneKind.Forest: Forest(s, props); break;
                    case SceneKind.Coast: Coast(s, props); break;
                }
            }
            finally { Random.state = oldState; }
            foreach (var t in root.GetComponentsInChildren<Transform>(true)) t.gameObject.hideFlags = HideFlags.DontSave;
            return s;
        }

        // ================================================================== shared helpers

        static float R(float a, float b) => Random.Range(a, b);
        static int RI(int a, int b) => Random.Range(a, b);
        static T Pick<T>(params T[] arr) => arr[Random.Range(0, arr.Length)];

        static GameObject Mesh(Transform parent, string name, Mesh mesh, Material mat, bool shadows = true)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = shadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
            return go;
        }

        static void Ground(BuiltScene s, string key, float size, float y = -0.02f)
        {
            float h = size * 0.5f;
            GroundRect(s, key, -h, h, -h, h, y);
        }

        static void GroundRect(BuiltScene s, string key, float x0, float x1, float z0, float z1, float y)
        {
            var b = new MeshBuilder();
            b.Quad(new Vector3(x0, y, z0), new Vector3(x0, y, z1), new Vector3(x1, y, z1), new Vector3(x1, y, z0),
                   new Vector2(x0, z0), new Vector2(x0, z1), new Vector2(x1, z1), new Vector2(x1, z0));
            var m = b.ToMesh("Ground"); s.mats.Own(m);
            var go = Mesh(s.root, "Ground", m, s.mats.Get(key), false);
            go.GetComponent<MeshRenderer>().receiveShadows = true;
        }

        /// <summary>Grid coordinates from lo to hi with fine spacing near 'center' growing geometrically outside 'fine'.</summary>
        static List<float> Spaced(float lo, float hi, float center, float step, float fine, float growth = 1.06f)
        {
            var pos = new List<float>(); var neg = new List<float>();
            float x = 0, st = step;
            while (center + x < hi) { pos.Add(center + x); if (x > fine) st *= growth; x += st; }
            pos.Add(hi);
            x = st = step;
            while (center - x > lo) { neg.Add(center - x); if (x > fine) st *= growth; x += st; }
            neg.Add(lo);
            neg.Reverse();
            var all = new List<float>(neg);
            foreach (var p in pos) if (all.Count == 0 || p > all[all.Count - 1] + 0.01f) all.Add(p);
            return all;
        }

        /// <summary>Flat water grid (waves are in the shader). Fine near the focus, coarse far away.</summary>
        static GameObject Water(BuiltScene s, string name, Material mat, float x0, float x1, float z0, float z1, float y, Vector2 center, float step = 4f, float fine = 300f)
        {
            var xs = Spaced(x0, x1, Mathf.Clamp(center.x, x0, x1), step, fine);
            var zs = Spaced(z0, z1, Mathf.Clamp(center.y, z0, z1), step, fine);
            var v = new List<Vector3>(xs.Count * zs.Count); var n = new List<Vector3>(); var t = new List<int>();
            foreach (var z in zs) foreach (var x in xs) { v.Add(new Vector3(x, y, z)); n.Add(Vector3.up); }
            int W = xs.Count;
            for (int j = 0; j < zs.Count - 1; j++)
                for (int i = 0; i < W - 1; i++)
                {
                    int a = j * W + i;
                    t.Add(a); t.Add(a + W); t.Add(a + W + 1); t.Add(a); t.Add(a + W + 1); t.Add(a + 1);
                }
            var m = new Mesh { name = name, indexFormat = IndexFormat.UInt32 };
            m.SetVertices(v); m.SetNormals(n); m.SetTriangles(t, 0);
            m.bounds = new Bounds(new Vector3((x0 + x1) / 2, y, (z0 + z1) / 2), new Vector3(x1 - x0, 40f, z1 - z0));
            s.mats.Own(m);
            var go = Mesh(s.root, name, m, mat, false);
            return go;
        }

        /// <summary>Flat-shaded heightfield over [-half, half]², materials chosen per triangle by height and slope.</summary>
        static void HeightField(BuiltScene s, MeshBatch batch, float half, float cell, Func<float, float, float> h, Func<float, float, string> material)
        {
            int n = Mathf.CeilToInt(half * 2 / cell);
            for (int j = 0; j < n; j++)
                for (int i = 0; i < n; i++)
                {
                    float x0 = -half + i * cell, z0 = -half + j * cell, x1 = x0 + cell, z1 = z0 + cell;
                    Vector3 a = new Vector3(x0, h(x0, z0), z0), b = new Vector3(x0, h(x0, z1), z1), c = new Vector3(x1, h(x1, z1), z1), d = new Vector3(x1, h(x1, z0), z0);
                    bool flip = ((i + j) & 1) == 0;
                    if (flip) { Put(batch, a, b, c, material); Put(batch, a, c, d, material); }
                    else { Put(batch, a, b, d, material); Put(batch, b, c, d, material); }
                }
        }

        static void Put(MeshBatch batch, Vector3 a, Vector3 b, Vector3 c, Func<float, float, string> material)
        {
            var nrm = Vector3.Cross(b - a, c - a).normalized;
            float avgH = (a.y + b.y + c.y) / 3f;
            batch[material(avgH, nrm.y)].Tri(a, b, c);
        }

        /// <summary>Ring of mountains far around the scene. seaDir (xz) leaves a gap for the sea.</summary>
        static void Backdrop(BuiltScene s, float r0, float r1, float maxH, Vector3 seaDir, string low = "mountain", string high = "mountain_rock", float rockFrom = 0.62f)
        {
            var batch = new MeshBatch();
            int na = 120; float[] rs = { r0, r0 + 150, r0 + 350, r0 + 600, r0 + 900, r0 + 1300, r1 };
            float seed = Random.value * 100f;
            float H(float ang, float r)
            {
                float env = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(r0, r0 + 450f, r));
                float n = Mathf.PerlinNoise(Mathf.Cos(ang) * 2.2f + seed, Mathf.Sin(ang) * 2.2f + r / 700f) * 0.7f
                        + Mathf.PerlinNoise(Mathf.Cos(ang) * 6f + seed * 2, Mathf.Sin(ang) * 6f + r / 300f) * 0.3f;
                float hh = env * maxH * Mathf.Lerp(0.25f, 1.15f, n);
                if (seaDir != Vector3.zero)
                {
                    var dir = new Vector3(Mathf.Sin(ang), 0, Mathf.Cos(ang));
                    float sea = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(-0.05f, 0.45f, Vector3.Dot(dir, seaDir)));
                    hh = Mathf.Lerp(hh, -8f, sea);
                }
                return hh;
            }
            for (int i = 0; i < na; i++)
            {
                float a0 = i * Mathf.PI * 2 / na, a1 = (i + 1) * Mathf.PI * 2 / na;
                for (int k = 0; k < rs.Length - 1; k++)
                {
                    Vector3 P(float a, float r) => new Vector3(Mathf.Sin(a) * r, H(a, r), Mathf.Cos(a) * r);
                    Vector3 p00 = P(a0, rs[k]), p10 = P(a1, rs[k]), p01 = P(a0, rs[k + 1]), p11 = P(a1, rs[k + 1]);
                    string Mat(Vector3 a, Vector3 b, Vector3 c) => (a.y + b.y + c.y) / 3f > maxH * rockFrom ? high : low;
                    if (p00.y < -4 && p10.y < -4 && p01.y < -4 && p11.y < -4) continue;
                    batch[Mat(p00, p01, p11)].Tri(p00, p01, p11);
                    batch[Mat(p00, p11, p10)].Tri(p00, p11, p10);
                }
            }
            batch.Flush(s.root, "Backdrop", s.mats, false);
        }

        static BuiltScene.Lane Lane(BuiltScene s, bool loop, params Vector3[] pts)
        {
            var l = new BuiltScene.Lane { loop = loop };
            l.pts.AddRange(pts);
            for (int i = 0; i < pts.Length - 1; i++) l.length += Vector3.Distance(pts[i], pts[i + 1]);
            if (loop) l.length += Vector3.Distance(pts[pts.Length - 1], pts[0]);
            s.lanes.Add(l);
            return l;
        }

        static void Cars(BuiltScene s, Props props, int count, float minSpeed, float maxSpeed)
        {
            if (s.lanes.Count == 0) return;
            var parent = new GameObject("Traffic").transform; parent.SetParent(s.root, false);
            for (int i = 0; i < count; i++)
            {
                var lane = s.lanes[i % s.lanes.Count];
                var t = props.Car(parent, RI(0, 40));
                s.cars.Add(new BuiltScene.CarOnLane { t = t, lane = lane, s = R(0, lane.length), speed = R(minSpeed, maxSpeed) });
            }
        }

        /// <summary>Road strip (asphalt) along a polyline with optional centre line and edge lamps.</summary>
        static void RoadStrip(MeshBatch batch, List<Vector3> pts, float width, Func<float, float, float> h, string mat = "asphalt", float lift = 0.03f)
        {
            for (int i = 0; i < pts.Count - 1; i++)
            {
                Vector3 a = pts[i], b = pts[i + 1];
                Vector3 dir = (b - a); dir.y = 0; dir.Normalize();
                Vector3 side = Vector3.Cross(Vector3.up, dir) * width * 0.5f;
                Vector3 a0 = a - side, a1 = a + side, b0 = b - side, b1 = b + side;
                a0.y = h(a0.x, a0.z) + lift; a1.y = h(a1.x, a1.z) + lift; b0.y = h(b0.x, b0.z) + lift; b1.y = h(b1.x, b1.z) + lift;
                batch[mat].Quad(a0, b0, b1, a1, new Vector2(a0.x, a0.z), new Vector2(b0.x, b0.z), new Vector2(b1.x, b1.z), new Vector2(a1.x, a1.z));
            }
        }

        static List<Vector3> Curve(Func<float, Vector3> f, int n)
        {
            var l = new List<Vector3>(n + 1);
            for (int i = 0; i <= n; i++) l.Add(f(i / (float)n));
            return l;
        }

        /// <summary>Box building with a facade texture, flat roof and roof clutter. Height snapped to whole floors.</summary>
        static float Tower(MeshBatch batch, Vector3 pos, float w, float d, float h, float yaw, string facade, float floorH, bool clutter = true, bool beacon = false)
        {
            h = Mathf.Max(floorH, Mathf.Round(h / floorH) * floorH);
            batch[facade].Box(pos, new Vector3(w, h, d), yaw, 1f, false);
            var q = Quaternion.Euler(0, yaw, 0);
            batch["roof_flat"].Box(pos + Vector3.up * h, new Vector3(w + 0.3f, 0.4f, d + 0.3f), yaw);
            if (clutter)
            {
                int n = RI(1, 4);
                for (int i = 0; i < n; i++)
                {
                    var off = q * new Vector3(R(-w * 0.3f, w * 0.3f), 0, R(-d * 0.3f, d * 0.3f));
                    batch["concrete"].Box(pos + off + Vector3.up * (h + 0.4f), new Vector3(R(2f, 5f), R(1.2f, 3f), R(2f, 5f)), yaw);
                }
            }
            if (beacon)
            {
                batch["dark_metal"].Cylinder(pos + Vector3.up * (h + 0.4f), 0.25f, 0.12f, h * 0.12f + 6f, 5);
                batch["beacon"].Box(pos + Vector3.up * (h + 0.4f + h * 0.12f + 6f), new Vector3(0.6f, 0.6f, 0.6f), 0, 1, true, true);
            }
            return h;
        }
    }
}
