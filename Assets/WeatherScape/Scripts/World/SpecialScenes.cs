using UnityEngine;

namespace WeatherScape.World
{
    public static partial class SceneBuilder
    {
        // ================================================================== 4. Tourist site

        /// <summary>Heritage sight: a walled palace (gate, main hall on a stone terrace, side halls), a five-storey pagoda,
        /// a market street with lanterns, tour buses, and an observation tower on the hill behind.</summary>
        static void Tourist(BuiltScene s, Props props)
        {
            var b = new MeshBatch();
            s.focus = new Vector3(0, 14f, 70f);
            s.camDistance = 250f; s.camHeight = 50f; s.camYaw = 12f;
            s.extent = 260f;
            s.floodLevel = 1.0f;
            float seed = Random.value * 50f;
            var hill = new Vector2(0f, 380f);
            float Hf(float x, float z)
            {
                float d = new Vector2((x - hill.x) / 1.9f, z - hill.y).magnitude;
                float bump = 75f * Mathf.SmoothStep(0f, 1f, 1f - Mathf.Clamp01(d / 210f)) * (0.7f + 0.6f * Mathf.PerlinNoise(x / 90f + seed, z / 90f));
                float r = new Vector2(x, z).magnitude;
                float outer = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(420f, 900f, r)) * (60f + 90f * Mathf.PerlinNoise(x / 250f + seed, z / 250f));
                return Mathf.Max(bump, outer);
            }
            s.height = Hf;
            s.fireCenter = new Vector3(-150f, 0, 430f); s.fireCenter.y = Hf(-150f, 430f);
            Ground(s, "grass", 8000f, -0.6f);
            HeightField(s, b, 1100f, 12f, Hf, (h, ny) => h < 0.3f ? "grass" : ny < 0.72f && h > 50f ? "mountain_rock" : "mountain");

            // palace precinct
            var P = new Vector3(0, 0, 100f);
            b["plaza"].Box(P + Vector3.back * 5f, new Vector3(170f, 0.06f, 130f));
            foreach (var (c, size) in new[] { (P + new Vector3(0, 0, 60f), new Vector3(172f, 4.2f, 2f)), (P + new Vector3(-85f, 0, -2f), new Vector3(2f, 4.2f, 126f)), (P + new Vector3(85f, 0, -2f), new Vector3(2f, 4.2f, 126f)),
                                             (P + new Vector3(-50f, 0, -65f), new Vector3(70f, 4.2f, 2f)), (P + new Vector3(50f, 0, -65f), new Vector3(70f, 4.2f, 2f)) })
            {
                b["wall_white"].Box(c, size);
                b["roof_dark"].Box(c + Vector3.up * size.y, new Vector3(size.x + 1.2f, 0.6f, size.z + 1.2f));
            }
            // gate (two storeys)
            var gate = P + new Vector3(0, 0, -65f);
            b["wall_stone"].Box(gate, new Vector3(30f, 6f, 10f));
            b["glass"].Box(gate + new Vector3(0, 0, -5.05f), new Vector3(6f, 4.5f, 0.2f));
            Pavilion(b, gate + Vector3.up * 6f, 26f, 9f, 5f, 2);
            // main hall on a two-step terrace
            var hall = P + new Vector3(0, 0, 25f);
            b["wall_stone"].Box(hall, new Vector3(54f, 1.4f, 32f));
            b["wall_stone"].Box(hall + Vector3.up * 1.4f, new Vector3(44f, 1.4f, 24f));
            b["wall_stone"].Box(hall + new Vector3(0, 0, -19f), new Vector3(8f, 1.4f, 6f));
            Pavilion(b, hall + Vector3.up * 2.8f, 36f, 16f, 8f, 2);
            // side halls
            foreach (int sx in new[] { -1, 1 })
            {
                var sh = P + new Vector3(sx * 58f, 0, 0f);
                b["wall_stone"].Box(sh, new Vector3(16f, 0.8f, 44f));
                Pavilion(b, sh + Vector3.up * 0.8f, 12f, 40f, 5f, 1);
            }
            for (int k = 0; k < 12; k++) props.Tree(s.root, TreeType.Conifer, P + new Vector3(Pick(-75f, 75f) + R(-4, 4), 0, R(-55f, 50f)), R(0.8f, 1.2f), R(0, 360), null, 0.6f);

            // five-storey pagoda west of the palace
            var pg = new Vector3(-130f, 0, 40f);
            b["wall_stone"].Box(pg, new Vector3(12f, 1.2f, 12f));
            float y = 1.2f, w = 8f;
            for (int i = 0; i < 5; i++)
            {
                b["wall_red"].Box(pg + Vector3.up * y, new Vector3(w, 3.2f, w));
                b["roof_dark"].Hip(pg + Vector3.up * (y + 3.2f), w, w, 1.6f, 0f, 1.8f, 0.05f);
                y += 4.6f; w *= 0.84f;
            }
            b["gold"].Cylinder(pg + Vector3.up * y, 0.25f, 0.1f, 6f, 6);

            // market street with stalls and lanterns
            for (float x = -150f; x <= 150f; x += 10f)
            {
                if (Mathf.Abs(x) < 18f) continue;
                foreach (int side in new[] { -1, 1 })
                {
                    var st = new Vector3(x, 0, -30f + side * 8f);
                    b["wall_wood"].Box(st, new Vector3(7f, 1f, 3f));
                    for (int a = -1; a <= 1; a += 2) b["wall_wood"].Cylinder(st + new Vector3(a * 3.3f, 0, side * 1.4f), 0.08f, 0.08f, 2.6f, 4);
                    b[Pick("cloth_a", "cloth_b", "cloth_c", "cloth_d")].Gable(st + Vector3.up * 2.6f, 7.4f, 3.6f, 0.8f, 0f, 0.1f);
                }
                b["lantern"].Blob(new Vector3(x, 4.2f, -30f), new Vector3(0.45f, 0.6f, 0.45f), (int)x);
                if (((int)x / 10) % 3 == 0) s.lamps.Add(new Vector3(x, 4f, -30f));
            }
            b["dark_metal"].Box(new Vector3(0, 4.7f, -30f), new Vector3(300f, 0.04f, 0.04f));
            b["plaza"].Box(new Vector3(0, 0, -30f), new Vector3(320f, 0.04f, 10f));
            // forecourt with a statue
            b["plaza"].Box(new Vector3(0, 0, -5f), new Vector3(60f, 0.05f, 30f));
            b["wall_stone"].Box(new Vector3(0, 0, -5f), new Vector3(5f, 2f, 5f));
            b["gold"].Box(new Vector3(0, 2f, -5f), new Vector3(1.4f, 4f, 1.4f));
            for (int k = 0; k < 10; k++) props.Tree(s.root, TreeType.Cherry, new Vector3(R(-28, 28), 0, Pick(-18f, 8f)), R(0.7f, 0.95f), R(0, 360));

            // road + bus park
            b["asphalt"].Box(new Vector3(0, 0, -80f), new Vector3(1600f, 0.02f, 12f));
            b["mark_yellow"].Box(new Vector3(0, 0.025f, -80f), new Vector3(1600f, 0.01f, 0.2f));
            Lane(s, false, new Vector3(-800f, 0, -77.5f), new Vector3(800f, 0, -77.5f));
            Lane(s, false, new Vector3(800f, 0, -82.5f), new Vector3(-800f, 0, -82.5f));
            var busPark = new Vector3(150f, 0, -55f);
            b["asphalt"].Box(busPark, new Vector3(60f, 0.03f, 28f));
            for (int k = 0; k < 4; k++) { var bus = props.Car(s.root, 12); bus.localPosition = busPark + new Vector3(-21f + k * 14f, 0.05f, 0); }
            for (float x = -300f; x <= 300f; x += 40f) props.Lamp(b, new Vector3(x, 0, -73f), 180f, 6.5f, 1.2f);

            // observation tower on the hill
            var tw = new Vector3(hill.x, Hf(hill.x, hill.y), hill.y);
            b["concrete"].Cylinder(tw, 6f, 3.2f, 120f, 12);
            b["concrete"].Cylinder(tw + Vector3.up * 92f, 9f, 11f, 6f, 16);
            b["window_warm"].Cylinder(tw + Vector3.up * 98f, 11f, 11f, 5f, 16, false);
            b["concrete"].Cylinder(tw + Vector3.up * 103f, 11f, 6f, 4f, 16);
            b["metal"].Cylinder(tw + Vector3.up * 120f, 1.2f, 0.2f, 40f, 6);
            b["beacon"].Box(tw + Vector3.up * 160f, Vector3.one * 1.2f, 0, 1, true, true);

            // surrounding low town with hanok roofs
            for (int k = 0; k < 220; k++)
            {
                float ang = R(0, Mathf.PI * 2), r = R(250f, 700f);
                var p = new Vector3(Mathf.Sin(ang) * r, 0, Mathf.Cos(ang) * r);
                if ((new Vector2(p.x, p.z) - hill).magnitude < 210f || Mathf.Abs(p.z + 80f) < 12f) continue;
                p.y = Hf(p.x, p.z);
                if (p.y > 8f) continue;
                props.House(b, p, R(9f, 14f), R(6f, 9f), R(3f, 4.5f), R(-10f, 10f) + Pick(0f, 90f), Pick("facade_wood", "facade_house", "facade_house"), Pick("roof_dark", "roof_dark", "roof_red"), Random.value < 0.6f, 2.4f);
            }
            for (int k = 0; k < 500; k++)
            {
                float x = R(-900f, 900f), z = R(-600f, 950f);
                float h = Hf(x, z);
                if (h < 6f) continue;
                props.Tree(s.root, Random.value < 0.6f ? TreeType.Conifer : TreeType.Broadleaf, new Vector3(x, h - 0.3f, z), R(1.1f, 1.8f), R(0, 360), null, 0.8f);
            }
            Backdrop(s, 1300f, 3600f, 300f, Vector3.zero);
            b.Flush(s.root, "Sight", s.mats);
            Cars(s, props, 14, 6f, 11f);
            s.volcanoPos = new Vector3(-800f, 0, 2400f);
        }

        /// <summary>Wooden hall with red pillars, a dancheong band and a (double) hip roof with deep eaves.</summary>
        static void Pavilion(MeshBatch b, Vector3 basePos, float w, float d, float h, int tiers)
        {
            b["wall_white"].Box(basePos, new Vector3(w - 2f, h, d - 2f));
            int nx = Mathf.Max(2, Mathf.RoundToInt(w / 4f)), nz = Mathf.Max(2, Mathf.RoundToInt(d / 4f));
            for (int i = 0; i <= nx; i++)
                foreach (int sz in new[] { -1, 1 })
                    b["wall_red"].Cylinder(basePos + new Vector3(-w / 2 + i * w / nx, 0, sz * d / 2), 0.35f, 0.35f, h, 8);
            for (int j = 1; j < nz; j++)
                foreach (int sx in new[] { -1, 1 })
                    b["wall_red"].Cylinder(basePos + new Vector3(sx * w / 2, 0, -d / 2 + j * d / nz), 0.35f, 0.35f, h, 8);
            b["dancheong"].Box(basePos + Vector3.up * (h - 0.8f), new Vector3(w + 0.8f, 0.9f, d + 0.8f));
            if (tiers >= 2)
            {
                b["roof_dark"].Hip(basePos + Vector3.up * h, w, d, h * 0.35f, 0f, 2.6f, 0.0f);
                b["wall_white"].Box(basePos + Vector3.up * (h + h * 0.3f), new Vector3(w * 0.7f, h * 0.5f, d * 0.6f));
                b["dancheong"].Box(basePos + Vector3.up * (h * 1.7f), new Vector3(w * 0.72f, 0.7f, d * 0.62f));
                b["roof_dark"].Hip(basePos + Vector3.up * (h * 1.8f), w * 0.7f, d * 0.6f, h * 0.6f, 0f, 2.4f, 0.45f);
            }
            else b["roof_dark"].Hip(basePos + Vector3.up * h, w, d, h * 0.6f, 0f, 2.2f, 0.45f);
        }

        // ================================================================== 6. Coast / harbour / beach

        /// <summary>Seaside town: open sea to the south, a sandy beach with umbrellas, a fishing harbour with breakwater,
        /// lighthouse, cranes and containers, a promenade road, beachfront hotels and houses climbing the hills; islands offshore.</summary>
        static void Coast(BuiltScene s, Props props)
        {
            var b = new MeshBatch();
            s.focus = new Vector3(-40f, 12f, -20f);
            s.camDistance = 300f; s.camHeight = 55f; s.camYaw = 160f;
            s.extent = 300f;
            s.seaDir = Vector3.back; s.shoreDistance = 10f; s.seaLevel = 0f;
            s.floodLevel = 2.4f; s.groundLevel = 2f;
            float seed = Random.value * 50f;
            float Hf(float x, float z)
            {
                if (x < -70f) // harbour quay (vertical wall into the sea)
                    return z > -12f ? 2f + Mathf.Max(0f, z - 60f) * 0.06f + Hills(x, z) : -7f;
                float beach = Mathf.Lerp(-5f, 1.6f, Mathf.InverseLerp(-110f, 22f, z));
                if (z < 22f) return beach;
                return 2f + (z - 22f) * 0.05f + Hills(x, z);
            }
            float Hills(float x, float z) => Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(230f, 650f, z)) * (70f + 120f * Mathf.PerlinNoise(x / 230f + seed, z / 230f));
            s.height = Hf;
            s.fireCenter = new Vector3(260f, 0, 560f); s.fireCenter.y = Hf(260f, 560f);
            HeightField(s, b, 1200f, 10f, Hf, (h, ny) => h < 1.8f ? (h < 0.3f ? "wetsand" : "sand") : ny < 0.5f ? "concrete" : h > 60f ? (ny < 0.72f ? "mountain_rock" : "mountain") : "grass");

            GroundRect(s, "grass", -4000f, 4000f, 20f, 4000f, -0.5f);
            s.seaMat = s.mats.NewWater(new Color(0.03f, 0.16f, 0.25f), new Color(0.08f, 0.40f, 0.44f), 0.45f, 0.25f);
            Water(s, "Sea", s.seaMat, -4000f, 4000f, -4000f, 30f, 0f, new Vector2(-60f, -40f), 3.5f, 320f);

            // harbour: breakwater + lighthouse
            b["concrete"].Box(new Vector3(-380f, -7f, -120f), new Vector3(10f, 10f, 220f));
            b["concrete"].Box(new Vector3(-310f, -7f, -228f), new Vector3(150f, 10f, 10f));
            for (int k = 0; k < 40; k++) b["concrete"].Box(new Vector3(-386f + R(-3, 3), -2f, R(-230f, -10f)), new Vector3(3f, 2.4f, 3f), R(0, 90));
            var lh = new Vector3(-240f, 3f, -228f);
            b["concrete"].Cylinder(lh, 5f, 5f, 0.6f, 12);
            for (int i = 0; i < 4; i++) b[i % 2 == 0 ? "lh_white" : "lh_red"].Cylinder(lh + Vector3.up * (0.6f + i * 4f), 2.4f - i * 0.25f, 2.4f - (i + 1) * 0.25f, 4f, 12);
            b["glass"].Cylinder(lh + Vector3.up * 16.6f, 1.4f, 1.4f, 2.2f, 12);
            b["lh_light"].Box(lh + Vector3.up * 17.2f, new Vector3(0.9f, 1f, 0.9f));
            b["lh_red"].Cone(lh + Vector3.up * 18.8f, 1.8f, 2f, 12);
            var beam = new GameObject("LighthouseBeam").transform;
            beam.SetParent(s.root, false);
            beam.localPosition = lh + Vector3.up * 17.7f;
            s.lighthouseBeam = beam;
            s.spinning.Add(beam);
            s.lamps.Add(lh + Vector3.up * 17.7f);

            // quay: cranes, containers, boats
            for (int k = 0; k < 2; k++)
            {
                var c = new Vector3(-330f + k * 70f, 2f, -4f);
                for (int a = -1; a <= 1; a += 2) for (int z = -1; z <= 1; z += 2) b["crane"].Box(c + new Vector3(a * 7f, 0, z * 6f), new Vector3(1.2f, 32f, 1.2f));
                b["crane"].Box(c + new Vector3(0, 32f, 0), new Vector3(16f, 2.4f, 14f));
                b["crane"].Box(c + new Vector3(0, 33.5f, -20f), new Vector3(3f, 2f, 64f));
                b["dark_metal"].Box(c + new Vector3(0, 30f, 10f), new Vector3(6f, 4f, 6f));
            }
            for (int i = 0; i < 8; i++) for (int j = 0; j < 3; j++)
                {
                    int stack = RI(1, 4);
                    for (int h = 0; h < stack; h++) b[Pick("cont_a", "cont_b", "cont_c", "cont_d")].Box(new Vector3(-360f + i * 14f, 2f + h * 2.6f, 18f + j * 3f), new Vector3(12.2f, 2.6f, 2.5f));
                }
            for (int k = 0; k < 9; k++) props.Boat(s.root, new Vector3(-360f + k * 13f + R(-2, 2), 0f, -40f - R(0, 30)), R(170f, 190f), R(9f, 14f), k);
            props.Boat(s.root, new Vector3(-150f, 0f, -420f), 100f, 70f, 2);
            props.Boat(s.root, new Vector3(260f, 0f, -900f), 60f, 120f, 1);
            for (float x = -390f; x < -80f; x += 30f) props.Lamp(b, new Vector3(x, 2f, -8f), 180f, 7f, 1f);

            // beach umbrellas + lifeguard tower
            for (int k = 0; k < 26; k++)
            {
                var p = new Vector3(R(-20f, 330f), 0, R(0f, 15f)); p.y = Hf(p.x, p.z);
                b["metal"].Cylinder(p, 0.05f, 0.05f, 2.4f, 4);
                b[Pick("umbrella_a", "umbrella_b")].Cone(p + Vector3.up * 1.9f, 1.4f, 0.7f, 8);
            }
            var lg = new Vector3(150f, Hf(150f, 5f), 5f);
            for (int a = -1; a <= 1; a += 2) for (int c = -1; c <= 1; c += 2) b["wall_wood"].Cylinder(lg + new Vector3(a, 0, c), 0.1f, 0.1f, 3f, 4);
            b["wall_white"].Box(lg + Vector3.up * 3f, new Vector3(2.6f, 2f, 2.6f));
            b["roof_red"].Hip(lg + Vector3.up * 5f, 2.6f, 2.6f, 0.8f, 0f, 0.3f, 0f);
            for (int k = 0; k < 6; k++) { var bu = new Vector3(-60f + k * 90f, 0, -160f); b["buoy"].Blob(bu, new Vector3(0.6f, 0.6f, 0.6f), k); }

            // promenade road
            var road = Curve(t => new Vector3(Mathf.Lerp(-700f, 700f, t), 0, 32f), 40);
            RoadStrip(b, road, 12f, (x, z) => Hf(x, z), "asphalt", 0.05f);
            b["plaza"].Box(new Vector3(130f, 0, 24f), new Vector3(420f, 2.0f, 5f));
            Lane(s, false, new Vector3(-700f, Hf(0, 30f) + 0.05f, 29f), new Vector3(700f, Hf(0, 30f) + 0.05f, 29f));
            Lane(s, false, new Vector3(700f, Hf(0, 35f) + 0.05f, 35f), new Vector3(-700f, Hf(0, 35f) + 0.05f, 35f));
            for (float x = -60f; x <= 340f; x += 25f)
            {
                props.Lamp(b, new Vector3(x, 2f, 24f), 180f, 6f, 1f);
                props.Tree(s.root, TreeType.Palm, new Vector3(x + 12f, 2f, 24f), R(0.9f, 1.2f), R(0, 360), "leaf", 1.2f);
            }

            // beachfront hotels, then houses up the hill
            for (float x = -40f; x < 360f; x += R(28f, 40f))
            {
                var p = new Vector3(x, 0, R(55f, 70f)); p.y = Hf(p.x, p.z) - 0.5f;
                Tower(b, p, R(18f, 26f), R(12f, 16f), R(18f, 60f), R(-4f, 4f), Pick("facade_apart", "facade_office", "facade_glass"), 3.2f, true, false);
            }
            for (int k = 0; k < 260; k++)
            {
                var p = new Vector3(R(-500f, 600f), 0, R(90f, 420f));
                if (p.x < -70f && p.z < 60f) continue;
                p.y = Hf(p.x, p.z);
                if (p.y > 45f) continue;
                if (Random.value < 0.8f) props.House(b, p - Vector3.up * 0.3f, R(8f, 12f), R(7f, 9f), R(3f, 6f), R(-15f, 15f), Pick("facade_house", "facade_house", "facade_brick"), Pick("roof_red", "roof_blue", "roof_orange", "roof_green"));
                else Tower(b, p - Vector3.up * 0.5f, R(12f, 20f), R(10f, 14f), R(9f, 20f), R(-15f, 15f), "facade_apart", 2.9f, false);
            }
            for (int k = 0; k < 500; k++)
            {
                float x = R(-900f, 900f), z = R(200f, 900f);
                float h = Hf(x, z);
                if (h < 25f) continue;
                props.Tree(s.root, Random.value < 0.6f ? TreeType.Conifer : TreeType.Broadleaf, new Vector3(x, h - 0.3f, z), R(1.2f, 2f), R(0, 360), null, 0.8f);
            }
            // islands
            foreach (var (c, r) in new[] { (new Vector3(420f, 0, -820f), 90f), (new Vector3(-620f, 0, -1100f), 140f), (new Vector3(80f, 0, -1500f), 70f) })
            {
                b["mountain"].Blob(c + Vector3.down * 6f, new Vector3(r, r * 0.45f, r * 0.8f), (int)r);
                for (int k = 0; k < 12; k++) { var tp = c + new Vector3(R(-r, r) * 0.5f, r * 0.3f, R(-r, r) * 0.4f); props.Tree(s.root, TreeType.Conifer, tp, R(1.5f, 2.2f), R(0, 360), null, 0.6f); }
            }
            Backdrop(s, 1250f, 3600f, 320f, Vector3.back);
            b.Flush(s.root, "Coast", s.mats);
            Cars(s, props, 12, 6f, 10f);
            s.volcanoPos = new Vector3(1100f, 0, -1900f);
        }
    }
}
