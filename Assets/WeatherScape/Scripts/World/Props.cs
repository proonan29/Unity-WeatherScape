using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace WeatherScape.World
{
    public enum TreeType { Broadleaf, Conifer, Street, Cherry, Palm }

    /// <summary>Reusable low-poly props: trees, street lamps, cars, boats, houses. Meshes are built once per world and shared.</summary>
    public class Props
    {
        readonly Mats mats;
        readonly Dictionary<string, Mesh> meshes = new Dictionary<string, Mesh>();
        public readonly BuiltScene scene;

        public Props(Mats mats, BuiltScene scene) { this.mats = mats; this.scene = scene; }

        Mesh Multi(string name, params MeshBuilder[] parts)
        {
            if (meshes.TryGetValue(name, out var m)) return m;
            m = new Mesh { name = name, indexFormat = IndexFormat.UInt32 };
            var v = new List<Vector3>(); var n = new List<Vector3>(); var uv = new List<Vector2>();
            foreach (var p in parts) { v.AddRange(p.v); n.AddRange(p.n); uv.AddRange(p.uv); }
            m.SetVertices(v); m.SetNormals(n); m.SetUVs(0, uv);
            m.subMeshCount = parts.Length;
            int off = 0;
            for (int i = 0; i < parts.Length; i++)
            {
                var tris = new List<int>(parts[i].t.Count);
                foreach (var t in parts[i].t) tris.Add(t + off);
                m.SetTriangles(tris, i);
                off += parts[i].v.Count;
            }
            m.RecalculateBounds();
            mats.Own(m);
            meshes[name] = m;
            return m;
        }

        GameObject Instance(string name, Mesh mesh, Material[] materials, Transform parent, Vector3 pos, float yaw, float scale, bool shadows = true)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localRotation = Quaternion.Euler(0, yaw, 0);
            go.transform.localScale = Vector3.one * scale;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterials = materials;
            r.shadowCastingMode = shadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
            return go;
        }

        // ------------------------------------------------------------------ trees

        Mesh TreeMesh(TreeType type)
        {
            string key = "tree_" + type;
            if (meshes.TryGetValue(key, out var m)) return m;
            var trunk = new MeshBuilder(); var crown = new MeshBuilder();
            switch (type)
            {
                case TreeType.Conifer:
                    trunk.Cylinder(Vector3.zero, 0.28f, 0.18f, 2.4f, 6);
                    crown.Cone(new Vector3(0, 1.6f, 0), 2.5f, 4.2f, 7);
                    crown.Cone(new Vector3(0, 3.7f, 0), 1.95f, 3.6f, 7);
                    crown.Cone(new Vector3(0, 5.6f, 0), 1.35f, 3.2f, 7);
                    break;
                case TreeType.Street:
                    trunk.Cylinder(Vector3.zero, 0.16f, 0.12f, 2.6f, 6);
                    crown.Blob(new Vector3(0, 3.8f, 0), new Vector3(1.6f, 1.7f, 1.6f), 3);
                    break;
                case TreeType.Palm:
                    trunk.Cylinder(Vector3.zero, 0.22f, 0.16f, 6.5f, 6);
                    for (int i = 0; i < 6; i++)
                    {
                        float a = i * 60f;
                        var q = Quaternion.Euler(0, a, 0);
                        Vector3 b = new Vector3(0, 6.5f, 0), tip = b + q * new Vector3(0, -1.2f, 3.2f), side = q * new Vector3(0.55f, 0, 0);
                        crown.Tri(b, tip + side, tip - side); crown.Tri(b, tip - side, tip + side);
                    }
                    break;
                default:
                    trunk.Cylinder(Vector3.zero, 0.3f, 0.2f, 3.2f, 6);
                    crown.Blob(new Vector3(0, 4.9f, 0), new Vector3(2.5f, 2.3f, 2.5f), 7);
                    crown.Blob(new Vector3(0.9f, 4.2f, 0.5f), new Vector3(1.6f, 1.5f, 1.6f), 11);
                    crown.Blob(new Vector3(-0.8f, 4.4f, -0.6f), new Vector3(1.5f, 1.4f, 1.5f), 13);
                    break;
            }
            return Multi(key, trunk, crown);
        }

        public Transform Tree(Transform parent, TreeType type, Vector3 pos, float scale, float yaw, string leafKey = null, float flex = 1f)
        {
            string leaf = leafKey ?? (type == TreeType.Conifer ? "conifer" : type == TreeType.Cherry ? "leaf2" : "leaf");
            var go = Instance("Tree", TreeMesh(type == TreeType.Cherry ? TreeType.Broadleaf : type), new[] { mats.Get("trunk"), mats.Get(leaf) }, parent, pos, yaw, scale);
            scene.sway.Add(new BuiltScene.Sway { t = go.transform, baseRot = go.transform.localRotation, phase = Random.value * 6.28f, flex = flex * (type == TreeType.Conifer ? 0.7f : 1f) / Mathf.Max(0.6f, scale) });
            return go.transform;
        }

        // ------------------------------------------------------------------ street furniture

        public void Lamp(MeshBatch batch, Vector3 pos, float yaw, float height = 7f, float arm = 1.6f)
        {
            batch["dark_metal"].Cylinder(pos, 0.12f, 0.08f, height, 6);
            var q = Quaternion.Euler(0, yaw, 0);
            Vector3 armEnd = pos + Vector3.up * height + q * Vector3.forward * arm;
            batch["dark_metal"].Box(pos + Vector3.up * (height - 0.08f) + q * Vector3.forward * (arm * 0.5f), new Vector3(0.12f, 0.12f, arm), yaw);
            batch["lamp"].Box(armEnd + Vector3.down * 0.25f, new Vector3(0.5f, 0.25f, 0.8f), yaw, 1f, true, true);
            scene.lamps.Add(armEnd + Vector3.down * 0.4f);
        }

        public void Bench(MeshBatch batch, Vector3 pos, float yaw)
        {
            batch["wall_wood"].Box(pos + Vector3.up * 0.42f, new Vector3(1.8f, 0.08f, 0.5f), yaw);
            batch["dark_metal"].Box(pos, new Vector3(0.1f, 0.42f, 0.45f), yaw);
        }

        // ------------------------------------------------------------------ cars

        static readonly string[] CarColors = { "car_white", "car_black", "car_silver", "car_red", "car_blue", "car_white", "car_silver", "car_yellow" };

        Mesh CarMesh(int variant, out string bodyKey)
        {
            bodyKey = CarColors[variant % CarColors.Length];
            bool bus = variant % 13 == 12;
            string key = bus ? "bus" : "car";
            if (meshes.TryGetValue(key, out var m)) return m;
            var body = new MeshBuilder(); var glass = new MeshBuilder(); var head = new MeshBuilder(); var tail = new MeshBuilder(); var tyre = new MeshBuilder();
            if (bus)
            {
                body.Box(new Vector3(0, 0.4f, 0), new Vector3(2.5f, 2.6f, 11f));
                glass.Box(new Vector3(0, 1.5f, 0.2f), new Vector3(2.52f, 0.9f, 10.2f), 0, 1, false);
                head.Box(new Vector3(0.9f, 0.7f, 5.5f), new Vector3(0.35f, 0.2f, 0.05f)); head.Box(new Vector3(-0.9f, 0.7f, 5.5f), new Vector3(0.35f, 0.2f, 0.05f));
                tail.Box(new Vector3(0.95f, 0.8f, -5.52f), new Vector3(0.3f, 0.3f, 0.05f)); tail.Box(new Vector3(-0.95f, 0.8f, -5.52f), new Vector3(0.3f, 0.3f, 0.05f));
            }
            else
            {
                body.Box(new Vector3(0, 0.3f, 0), new Vector3(1.8f, 0.7f, 4.4f));
                body.Box(new Vector3(0, 1.0f, -0.2f), new Vector3(1.6f, 0.55f, 2.3f));
                glass.Box(new Vector3(0, 1.02f, -0.2f), new Vector3(1.62f, 0.45f, 2.2f), 0, 1, false);
                head.Box(new Vector3(0.6f, 0.65f, 2.2f), new Vector3(0.4f, 0.14f, 0.04f)); head.Box(new Vector3(-0.6f, 0.65f, 2.2f), new Vector3(0.4f, 0.14f, 0.04f));
                tail.Box(new Vector3(0.65f, 0.7f, -2.22f), new Vector3(0.35f, 0.14f, 0.04f)); tail.Box(new Vector3(-0.65f, 0.7f, -2.22f), new Vector3(0.35f, 0.14f, 0.04f));
            }
            float len = bus ? 4f : 1.4f, wx = bus ? 1.15f : 0.85f;
            foreach (var zz in new[] { len, -len }) foreach (var xx in new[] { wx, -wx }) tyre.Box(new Vector3(xx, 0f, zz), new Vector3(0.25f, 0.62f, 0.62f));
            return Multi(key, body, glass, head, tail, tyre);
        }

        public Transform Car(Transform parent, int variant)
        {
            var mesh = CarMesh(variant, out var bodyKey);
            bool bus = variant % 13 == 12;
            var go = Instance(bus ? "Bus" : "Car", mesh, new[] { mats.Get(bus ? "bus" : bodyKey), mats.Get("glass"), mats.Get("headlight"), mats.Get("taillight"), mats.Get("dark_metal") }, parent, Vector3.zero, 0, 1f);
            return go.transform;
        }

        // ------------------------------------------------------------------ boats

        public Transform Boat(Transform parent, Vector3 pos, float yaw, float length, int variant)
        {
            string key = "boat" + (variant % 3);
            Mesh mesh;
            if (!meshes.TryGetValue(key, out mesh))
            {
                var hull = new MeshBuilder(); var deck = new MeshBuilder(); var cabin = new MeshBuilder(); var mast = new MeshBuilder();
                float L = 1f, W = 0.32f;
                // hull: tapered box (bow) — two boxes + wedge
                hull.Box(new Vector3(0, -0.12f, -0.1f), new Vector3(W, 0.22f, L * 0.8f));
                hull.Tri(new Vector3(-W / 2, -0.12f, 0.3f), new Vector3(-W / 2, 0.1f, 0.3f), new Vector3(0, 0.1f, 0.5f));
                hull.Tri(new Vector3(W / 2, -0.12f, 0.3f), new Vector3(0, 0.1f, 0.5f), new Vector3(W / 2, 0.1f, 0.3f));
                hull.Tri(new Vector3(-W / 2, -0.12f, 0.3f), new Vector3(0, 0.1f, 0.5f), new Vector3(0, -0.12f, 0.5f));
                hull.Tri(new Vector3(W / 2, -0.12f, 0.3f), new Vector3(0, -0.12f, 0.5f), new Vector3(0, 0.1f, 0.5f));
                deck.Box(new Vector3(0, 0.1f, -0.1f), new Vector3(W * 0.95f, 0.01f, L * 0.78f));
                cabin.Box(new Vector3(0, 0.1f, variant % 3 == 2 ? 0.05f : -0.2f), new Vector3(W * 0.6f, 0.14f, 0.22f));
                if (variant % 3 == 1) mast.Cylinder(new Vector3(0, 0.1f, 0.05f), 0.008f, 0.006f, 0.7f, 4);
                else mast.Box(new Vector3(0, 0.24f, -0.2f), new Vector3(0.02f, 0.18f, 0.02f));
                mesh = Multi(key, hull, deck, cabin, mast);
            }
            string hullKey = variant % 3 == 0 ? "hull_blue" : variant % 3 == 1 ? "hull_white" : "hull_red";
            var go = Instance("Boat", mesh, new[] { mats.Get(hullKey), mats.Get("deck_wood"), mats.Get("hull_white"), mats.Get("dark_metal") }, parent, pos, yaw, length);
            scene.boats.Add(new BuiltScene.Bob { t = go.transform, basePos = go.transform.localPosition, baseRot = go.transform.localRotation, phase = Random.value * 6.28f });
            return go.transform;
        }

        // ------------------------------------------------------------------ houses

        /// <summary>Small house: walls (textured) + gable or hip roof. Returns roof top height.</summary>
        public float House(MeshBatch batch, Vector3 pos, float w, float d, float wallH, float yaw, string wallKey, string roofKey, bool hip = false, float roofRise = -1f)
        {
            batch[wallKey].Box(pos, new Vector3(w, wallH, d), yaw, 1f, false);
            float rise = roofRise > 0 ? roofRise : d * 0.38f;
            if (hip) batch[roofKey].Hip(pos + Vector3.up * wallH, w, d, rise, yaw, 0.7f, 0.45f);
            else batch[roofKey].Gable(pos + Vector3.up * wallH, w, d, rise, yaw, 0.35f);
            return wallH + rise;
        }
    }
}
