using System.Collections.Generic;
using UnityEngine;

namespace WeatherScape.World
{
    public static partial class SceneBuilder
    {
        // ================================================================== 3. Village

        /// <summary>Countryside: a flat valley with a patchwork of rice paddies and fields, a stream, a winding farm road,
        /// a cluster of hanok and farmhouses, an old village tree with a pavilion, wooded hills around.</summary>
        static void Village(BuiltScene s, Props props)
        {
            var b = new MeshBatch();
            s.focus = new Vector3(30f, 12f, 60f);
            s.camDistance = 290f; s.camHeight = 50f; s.camYaw = 20f;
            s.extent = 230f;
            s.floodLevel = 0.9f;
            const float streamZ = 150f;
            float seed = Random.value * 50f;
            float Hf(float x, float z)
            {
                float r = new Vector2(x, z * 1.15f).magnitude;
                float hills = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(260f, 780f, r)) * (35f + 70f * Mathf.PerlinNoise(x / 260f + seed, z / 260f + seed));
                hills += Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(350f, 1000f, r)) * 45f * Mathf.PerlinNoise(x / 90f + seed * 2, z / 90f);
                float channel = Mathf.Abs(z - streamZ) < 9f ? Mathf.Lerp(-2.4f, 0f, Mathf.InverseLerp(5f, 9f, Mathf.Abs(z - streamZ))) : 0f;
                return hills + channel * (1f - Mathf.InverseLerp(200f, 260f, r));
            }
            s.height = Hf;
            s.fireCenter = new Vector3(-320f, 0, 380f); s.fireCenter.y = Hf(-320f, 380f);
            Ground(s, "grass", 8000f, -2f);
            HeightField(s, b, 1000f, 12f, Hf, (h, ny) => h < 0.5f ? "meadow" : ny < 0.72f && h > 50f ? "mountain_rock" : h > 6f ? "mountain" : "grass");

            s.lakeMat = s.mats.NewWater(new Color(0.08f, 0.2f, 0.2f), new Color(0.16f, 0.3f, 0.26f), 0.05f, 0f);
            s.lakeMat.SetFloat("_WaveLength", 5f);
            Water(s, "Stream", s.lakeMat, -260f, 260f, streamZ - 7f, streamZ + 7f, -1.0f, new Vector2(0, streamZ), 2f, 120f);
            // stone bridge
            b["wall_stone"].Box(new Vector3(-20f, -0.6f, streamZ), new Vector3(5f, 0.8f, 18f));

            // farm road
            var road = Curve(t => { float x = Mathf.Lerp(-700f, 700f, t); return new Vector3(x, 0, -40f + 30f * Mathf.Sin(x / 150f)); }, 80);
            RoadStrip(b, road, 6f, Hf, "dirt", 0.06f);
            var spur = Curve(t => Vector3.Lerp(new Vector3(-20f, 0, -40f + 30f * Mathf.Sin(-20f / 150f)), new Vector3(-20f, 0, 260f), t), 20);
            RoadStrip(b, spur, 4.5f, Hf, "dirt", 0.07f);
            var lane = new BuiltScene.Lane();
            foreach (var p in road) lane.pts.Add(p + Vector3.up * Hf(p.x, p.z));
            for (int i = 0; i < lane.pts.Count - 1; i++) lane.length += Vector3.Distance(lane.pts[i], lane.pts[i + 1]);
            s.lanes.Add(lane);
            for (int i = 0; i < road.Count; i += 4)
            {
                var p = road[i] + new Vector3(0, 0, 5f);
                p.y = Hf(p.x, p.z);
                b["wall_wood"].Cylinder(p, 0.14f, 0.12f, 8f, 5);
                b["wall_wood"].Box(p + Vector3.up * 7.4f, new Vector3(1.6f, 0.1f, 0.1f));
                if (i % 8 == 0 && Mathf.Abs(p.x) < 260f) props.Lamp(b, p, 180f, 6.5f, 0.8f);
            }

            // patchwork fields in the valley (south of the stream, avoiding road and village)
            var village = new Rect(-10f, 40f, 170f, 100f);
            for (float x = -220f; x < 220f; x += 32f)
                for (float z = -200f; z < 135f; z += 22f)
                {
                    var c = new Vector3(x + 16f, 0, z + 11f);
                    if (village.Contains(new Vector2(c.x, c.z))) continue;
                    float roadZ = -40f + 30f * Mathf.Sin(c.x / 150f);
                    if (Mathf.Abs(c.z - roadZ) < 16f || new Vector2(c.x, c.z * 1.15f).magnitude > 225f || Mathf.Abs(c.x + 20f) < 18f) continue;
                    string m = Pick("field_a", "field_a", "field_b", "field_c", "paddy_water");
                    b[m].Box(c, new Vector3(29f, 0.12f, 19f));
                    b["dirt"].Box(c + new Vector3(0, 0, 10f), new Vector3(30f, 0.2f, 1.2f));
                    if (m != "paddy_water" && Random.value < 0.6f)
                        for (float rz = -8f; rz <= 8f; rz += 2f) b["hedge"].Box(c + new Vector3(0, 0.12f, rz), new Vector3(27f, 0.25f, 0.7f));
                    if (m == "paddy_water")
                        for (float rx = -13f; rx <= 13f; rx += 2f) for (float rz = -8f; rz <= 8f; rz += 2f) b["field_a"].Box(c + new Vector3(rx, 0.1f, rz), new Vector3(0.45f, 0.45f, 0.45f), 0, 1, true, false);
                }

            // village houses
            placed.Clear();
            int nHouses = 0;
            for (int k = 0; k < 60 && nHouses < 20; k++)
            {
                var p = new Vector3(R(village.xMin + 8, village.xMax - 8), 0, R(village.yMin + 8, village.yMax - 8));
                bool clash = false;
                for (int q = 0; q < placed.Count; q++) if (Vector3.Distance(placed[q], p) < 22f) { clash = true; break; }
                if (clash) continue;
                placed.Add(p); nHouses++;
                float yaw = R(-12f, 12f) + (Random.value < 0.5f ? 0f : 90f);
                if (Random.value < 0.5f)
                {
                    // hanok: stone base, white walls with wood frame, dark tiled hip roof with big overhang
                    b["wall_stone"].Box(p, new Vector3(15f, 0.7f, 8f), yaw);
                    props.House(b, p + Vector3.up * 0.7f, 13f, 6.5f, 3f, yaw, "facade_wood", "roof_dark", true, 2.6f);
                    b["wall_stone"].Box(p + Quaternion.Euler(0, yaw, 0) * new Vector3(0, 0, -11f), new Vector3(20f, 1.4f, 0.6f), yaw);
                }
                else
                {
                    props.House(b, p, R(9f, 12f), R(7f, 9f), R(2.8f, 3.4f), yaw, "facade_house", Pick("roof_red", "roof_blue", "roof_orange", "roof_green"));
                    if (Random.value < 0.5f) props.House(b, p + Quaternion.Euler(0, yaw, 0) * new Vector3(10f, 0, 4f), 7f, 6f, 3.5f, yaw + 90f, "wall_wood", "roof_flat");
                }
                if (Random.value < 0.6f) props.Tree(s.root, Random.value < 0.4f ? TreeType.Cherry : TreeType.Broadleaf, p + new Vector3(R(9, 13), 0, R(-8, 8)), R(0.7f, 1f), R(0, 360));
            }
            placed.Clear();
            // greenhouses (vinyl tunnels) west of the spur road
            for (int k = 0; k < 5; k++)
            {
                var p = new Vector3(-75f, 0, 60f + k * 9f);
                for (int a = 0; a < 6; a++)
                {
                    float a0 = a * Mathf.PI / 6, a1 = (a + 1) * Mathf.PI / 6;
                    Vector3 p0 = p + new Vector3(0, Mathf.Sin(a0) * 3f, Mathf.Cos(a0) * 3.2f), p1 = p + new Vector3(0, Mathf.Sin(a1) * 3f, Mathf.Cos(a1) * 3.2f);
                    b["wall_white"].Quad(p0 + Vector3.left * 22f, p1 + Vector3.left * 22f, p1 + Vector3.right * 22f, p0 + Vector3.right * 22f, Vector2.zero, Vector2.up, Vector2.one, Vector2.right);
                }
            }
            // old village tree + pavilion (정자)
            var ent = new Vector3(-10f, 0, 30f);
            props.Tree(s.root, TreeType.Broadleaf, ent, 2.4f, 30f, "leaf", 0.4f);
            var pav = ent + new Vector3(14f, 0, -4f);
            for (int a = -1; a <= 1; a += 2) for (int c = -1; c <= 1; c += 2) b["wall_wood"].Cylinder(pav + new Vector3(a * 2.2f, 0, c * 2.2f), 0.15f, 0.15f, 3f, 6);
            b["deck_wood"].Box(pav + Vector3.up * 0.6f, new Vector3(5f, 0.2f, 5f));
            b["roof_dark"].Hip(pav + Vector3.up * 3f, 4.6f, 4.6f, 1.7f, 0f, 0.9f, 0.2f);

            // forests on the hills
            for (int k = 0; k < 900; k++)
            {
                float ang = R(0, Mathf.PI * 2), r = R(250f, 900f);
                float x = Mathf.Sin(ang) * r, z = Mathf.Cos(ang) * r;
                float h = Hf(x, z);
                if (h < 3f) continue;
                bool con = h > 60f || Random.value < 0.35f;
                props.Tree(s.root, con ? TreeType.Conifer : TreeType.Broadleaf, new Vector3(x, h - 0.3f, z), R(1.1f, 1.9f), R(0, 360), null, 0.8f);
            }
            for (int k = 0; k < 40; k++) props.Tree(s.root, TreeType.Broadleaf, new Vector3(R(-240, 240), 0, streamZ + Pick(-12f, 12f) + R(-3, 3)), R(0.8f, 1.2f), R(0, 360));

            b.Flush(s.root, "Village", s.mats);
            Cars(s, props, 3, 5f, 8f);
            s.volcanoPos = new Vector3(-700f, 0, 2200f);
        }

        static readonly List<Vector3> placed = new List<Vector3>();

        // ================================================================== 5. Forest / national park

        /// <summary>National park: a lake in a mountain bowl, dense mixed forest (conifers higher up), rocky peaks,
        /// a trail, a wooden pier and a visitor cabin with a small car park.</summary>
        static void Forest(BuiltScene s, Props props)
        {
            var b = new MeshBatch();
            s.focus = new Vector3(0, 45f, 160f);
            s.camDistance = 420f; s.camHeight = 55f; s.camYaw = 0f;
            s.extent = 420f;
            s.floodLevel = 2.5f;
            float seed = Random.value * 50f;
            float Hf(float x, float z)
            {
                float r = new Vector2(x, z).magnitude;
                float bowl = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(110f, 750f, r));
                float n1 = Mathf.PerlinNoise(x / 320f + seed, z / 320f + seed), n2 = Mathf.PerlinNoise(x / 110f + seed * 3, z / 110f + seed);
                float ridge = 1f - Mathf.Abs(Mathf.PerlinNoise(x / 420f + 11f, z / 420f + seed) * 2f - 1f);
                float h = bowl * (120f + 210f * n1 + 70f * ridge) + bowl * bowl * 40f * n2;
                float shore = Mathf.Lerp(-7f, 1.5f, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(70f, 135f, r)));
                return r < 135f ? shore : h + 1.5f;
            }
            s.height = Hf;
            s.groundLevel = 1.5f;
            Ground(s, "mountain", 9000f, -2f);
            HeightField(s, b, 1500f, 15f, Hf, (h, ny) => h < 0.2f ? "dirt" : h < 2.5f ? "grass" : (ny < 0.7f && h > 80f) || h > 300f ? "mountain_rock" : "mountain");

            s.lakeMat = s.mats.NewWater(new Color(0.04f, 0.15f, 0.18f), new Color(0.10f, 0.28f, 0.28f), 0.12f, 0.05f);
            s.lakeMat.SetFloat("_WaveLength", 9f);
            Water(s, "Lake", s.lakeMat, -170f, 170f, -170f, 170f, 0f, Vector2.zero, 3f, 170f);
            s.seaLevel = 0f;
            s.fireCenter = new Vector3(-330f, 0, 420f); s.fireCenter.y = Hf(-330f, 420f);

            // trail from the cabin up the south slope
            var trail = Curve(t => new Vector3(60f + 80f * Mathf.Sin(t * 5f), 0, Mathf.Lerp(-140f, -560f, t)), 60);
            RoadStrip(b, trail, 2.4f, Hf, "dirt", 0.15f);
            // cabin + car park + pier
            var cabin = new Vector3(55f, Hf(55f, -150f), -150f);
            b["wall_stone"].Box(cabin + Vector3.down * 1f, new Vector3(16f, 1.2f, 10f));
            props.House(b, cabin, 14f, 8f, 3.6f, 0f, "facade_wood", "roof_green");
            b["window_warm"].Box(cabin + new Vector3(0, 1.6f, -4.06f), new Vector3(6f, 1.4f, 0.1f));
            var park = cabin + new Vector3(-30f, 0, -6f);
            b["asphalt"].Box(park + Vector3.down * 0.9f, new Vector3(26f, 1f, 16f));
            for (int k = 0; k < 3; k++) { var car = props.Car(s.root, RI(0, 11)); car.localPosition = park + new Vector3(-8f + k * 7f, 0.1f, 2f); car.localRotation = Quaternion.Euler(0, 180f, 0); }
            props.Lamp(b, park + new Vector3(12f, 0, 8f), 270f, 5f, 0.6f);
            props.Lamp(b, cabin + new Vector3(-8f, 0, -6f), 180f, 4f, 0.5f);
            b["deck_wood"].Box(new Vector3(20f, -0.2f, -112f), new Vector3(3f, 0.6f, 40f));
            for (int k = 0; k < 5; k++) b["wall_wood"].Cylinder(new Vector3(18.6f + (k % 2) * 2.8f, -3f, -130f + k * 8f), 0.15f, 0.15f, 3f, 5);
            props.Boat(s.root, new Vector3(26f, 0f, -98f), 80f, 4.5f, 1);
            props.Boat(s.root, new Vector3(-40f, 0f, 30f), 20f, 4f, 0);

            // forest
            int n = 0;
            for (int k = 0; k < 9000 && n < 2600; k++)
            {
                float x = R(-800f, 800f), z = R(-500f, 850f);
                float r = new Vector2(x, z).magnitude;
                if (r < 140f || r > 880f) continue;
                if (Mathf.Abs(x - 55f) < 25f && Mathf.Abs(z + 150f) < 25f) continue;
                float h = Hf(x, z);
                if (h > 260f && Random.value < 0.8f) continue;
                bool con = h > 90f || Random.value < 0.45f;
                props.Tree(s.root, con ? TreeType.Conifer : TreeType.Broadleaf, new Vector3(x, h - 0.4f, z), R(1.9f, 3.3f), R(0, 360), Random.value < 0.25f && !con ? "leaf2" : null, 0.8f);
                n++;
            }
            // boulders around the lake
            for (int k = 0; k < 40; k++)
            {
                float ang = R(0, Mathf.PI * 2), r = R(120f, 200f);
                float x = Mathf.Sin(ang) * r, z = Mathf.Cos(ang) * r;
                b["rock"].Blob(new Vector3(x, Hf(x, z), z), new Vector3(R(1.5f, 4f), R(1f, 2.5f), R(1.5f, 4f)), k);
            }
            Backdrop(s, 1450f, 4000f, 520f, Vector3.zero, "mountain", "mountain_rock", 0.5f);
            b.Flush(s.root, "Park", s.mats);
            s.volcanoPos = new Vector3(400f, 0, 2600f);
        }
    }
}
