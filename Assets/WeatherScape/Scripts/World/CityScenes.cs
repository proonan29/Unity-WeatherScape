using System.Collections.Generic;
using UnityEngine;

namespace WeatherScape.World
{
    public static partial class SceneBuilder
    {
        // ================================================================== 1. Metropolis

        /// <summary>Downtown grid (6 × 6 blocks of 64 m, 16 m avenues), skyscrapers rising toward the centre, a park block,
        /// lower suburbs out to the horizon; a waterfront to the south when the city is on the coast.</summary>
        static void Metropolis(BuiltScene s, Props props, bool coastal)
        {
            var b = new MeshBatch();
            const float Block = 80f, Road = 16f, Half = 240f;
            float seaZ = -268f;
            s.focus = new Vector3(0, 55f, 0);
            s.camDistance = 640f; s.camHeight = 150f; s.camYaw = 30f;
            s.extent = 300f;
            s.floodLevel = 1.2f;
            s.fireCenter = new Vector3(-700f, 0, 900f);

            float landZ0 = coastal ? seaZ : -3500f;
            GroundRect(s, "concrete", -300f, 300f, Mathf.Max(landZ0, -300f), 300f, -0.03f);
            GroundRect(s, "urban", -1100f, 1100f, landZ0, 1100f, -0.2f);
            GroundRect(s, "meadow", -3500f, 3500f, landZ0, 3500f, -0.4f);

            if (coastal)
            {
                s.seaDir = Vector3.back; s.shoreDistance = -seaZ; s.seaLevel = -1.5f;
                s.seaMat = s.mats.NewWater(new Color(0.05f, 0.16f, 0.24f), new Color(0.12f, 0.33f, 0.38f), 0.4f, 0.15f);
                Water(s, "Sea", s.seaMat, -4000f, 4000f, -4000f, seaZ - 2f, s.seaLevel, new Vector2(0, seaZ - 2f));
                b["concrete"].Box(new Vector3(0, -3f, seaZ + 2f), new Vector3(560f, 3.2f, 6f));     // sea wall
                b["plaza"].Box(new Vector3(0, -0.02f, seaZ + 12f), new Vector3(520f, 0.05f, 14f)); // promenade
                for (float x = -240; x <= 240; x += 30f) props.Lamp(b, new Vector3(x, 0, seaZ + 6f), 180f, 5.5f, 0.6f);
                // pier with a ferry
                b["concrete"].Box(new Vector3(130f, -1f, seaZ - 60f), new Vector3(14f, 1.3f, 120f));
                props.Boat(s.root, new Vector3(150f, s.seaLevel, seaZ - 90f), 0f, 34f, 1);
                props.Boat(s.root, new Vector3(-320f, s.seaLevel, seaZ - 380f), 70f, 60f, 2);
            }

            // avenues (x and z), lane markings, sidewalks, crosswalks
            var lines = new List<float>();
            for (float c = -Half; c <= Half + 0.1f; c += Block) lines.Add(c);
            float zMin = coastal ? seaZ + 22f : -Half - 40f, zMax = Half + 40f, xMin = -Half - 40f, xMax = Half + 40f;
            foreach (var c in lines)
            {
                // north-south avenue at x = c
                b["asphalt"].Box(new Vector3(c, 0, (zMin + zMax) / 2), new Vector3(Road, 0.02f, zMax - zMin));
                for (float z = zMin; z < zMax; z += 9f) { b["mark_white"].Box(new Vector3(c - 4f, 0.025f, z), new Vector3(0.18f, 0.01f, 4f)); b["mark_white"].Box(new Vector3(c + 4f, 0.025f, z), new Vector3(0.18f, 0.01f, 4f)); }
                b["mark_yellow"].Box(new Vector3(c - 0.2f, 0.025f, (zMin + zMax) / 2), new Vector3(0.15f, 0.01f, zMax - zMin));
                b["mark_yellow"].Box(new Vector3(c + 0.2f, 0.025f, (zMin + zMax) / 2), new Vector3(0.15f, 0.01f, zMax - zMin));
                // east-west street at z = c
                if (!coastal || c > seaZ + 20f)
                {
                    b["asphalt"].Box(new Vector3((xMin + xMax) / 2, 0.005f, c), new Vector3(xMax - xMin, 0.02f, Road));
                    for (float x = xMin; x < xMax; x += 9f) { b["mark_white"].Box(new Vector3(x, 0.03f, c - 4f), new Vector3(4f, 0.01f, 0.18f)); b["mark_white"].Box(new Vector3(x, 0.03f, c + 4f), new Vector3(4f, 0.01f, 0.18f)); }
                }
                // lanes for traffic (two per direction)
                Lane(s, false, new Vector3(c + 2f, 0, zMin), new Vector3(c + 2f, 0, zMax));
                Lane(s, false, new Vector3(c - 2f, 0, zMax), new Vector3(c - 2f, 0, zMin));
                Lane(s, false, new Vector3(c + 6f, 0, zMin), new Vector3(c + 6f, 0, zMax));
                Lane(s, false, new Vector3(xMax, 0, c + 2f), new Vector3(xMin, 0, c + 2f));
                Lane(s, false, new Vector3(xMin, 0, c - 2f), new Vector3(xMax, 0, c - 2f));
            }
            foreach (var cx in lines) foreach (var cz in lines)
                for (int k = -3; k <= 3; k++)
                {
                    b["mark_white"].Box(new Vector3(cx + k * 1.6f, 0.03f, cz - Road * 0.5f - 2f), new Vector3(0.8f, 0.01f, 3f));
                    b["mark_white"].Box(new Vector3(cx - Road * 0.5f - 2f, 0.03f, cz + k * 1.6f), new Vector3(3f, 0.01f, 0.8f));
                }

            // blocks
            float inner = Block - Road;      // 64 m incl. sidewalks
            int parkI = RI(1, 4), parkJ = RI(2, 5);
            for (int i = 0; i < lines.Count - 1; i++)
                for (int j = 0; j < lines.Count - 1; j++)
                {
                    float cx = (lines[i] + lines[i + 1]) / 2, cz = (lines[j] + lines[j + 1]) / 2;
                    if (coastal && cz - inner / 2 < seaZ + 20f) continue;
                    b["sidewalk"].Box(new Vector3(cx, 0, cz), new Vector3(inner, 0.15f, inner));
                    // lamps on the four sidewalks
                    for (int k = -1; k <= 1; k += 2)
                    {
                        props.Lamp(b, new Vector3(cx + k * 16f, 0.15f, cz - inner / 2 + 1f), 180f);
                        props.Lamp(b, new Vector3(cx + k * 16f, 0.15f, cz + inner / 2 - 1f), 0f);
                    }
                    for (int k = -1; k <= 1; k += 2)
                    {
                        props.Tree(s.root, TreeType.Street, new Vector3(cx - inner / 2 + 1.6f, 0.15f, cz + k * 22f), R(0.85f, 1.05f), R(0, 360));
                        props.Tree(s.root, TreeType.Street, new Vector3(cx + inner / 2 - 1.6f, 0.15f, cz + k * 22f), R(0.85f, 1.05f), R(0, 360));
                    }
                    float dist = new Vector2(cx, cz).magnitude;
                    float tall = Mathf.Lerp(1f, 0.25f, Mathf.InverseLerp(40f, 300f, dist));
                    if (i == parkI && j == parkJ) { Park(s, props, b, new Vector3(cx, 0.15f, cz), inner - 6f); continue; }
                    if (i == 2 && j == 3 && !(parkI == 2 && parkJ == 3)) { PlazaTower(s, b, new Vector3(cx, 0.15f, cz), inner, tall); continue; }
                    // 1, 2 or 4 lots
                    int split = Pick(1, 2, 2, 4, 4);
                    float lot = inner - 8f;
                    var lots = new List<(Vector3 c, float w, float d)>();
                    if (split == 1) lots.Add((new Vector3(cx, 0.15f, cz), lot, lot));
                    else if (split == 2) { float w = lot / 2 - 2; lots.Add((new Vector3(cx - lot / 4, 0.15f, cz), w, lot)); lots.Add((new Vector3(cx + lot / 4, 0.15f, cz), w, lot)); }
                    else { float w = lot / 2 - 2; for (int a = -1; a <= 1; a += 2) for (int c = -1; c <= 1; c += 2) lots.Add((new Vector3(cx + a * lot / 4, 0.15f, cz + c * lot / 4), w, w)); }
                    foreach (var L in lots)
                    {
                        float h = R(25f, 70f) + tall * tall * R(60f, 190f) * (split == 4 ? 0.6f : 1f);
                        string fac = h > 90f ? Pick("facade_glass", "facade_glass", "facade_office") : Pick("facade_office", "facade_apart", "facade_glass", "facade_brick");
                        float fw = L.w * R(0.75f, 0.95f), fd = L.d * R(0.75f, 0.95f);
                        float built = Tower(b, L.c, fw, fd, h, 0f, fac, 3.6f, true, h > 120f);
                        if (h > 100f && Random.value < 0.6f)
                        {
                            float h2 = R(15f, 50f);
                            float top = Tower(b, L.c + Vector3.up * (built + 0.4f), fw * 0.65f, fd * 0.65f, h2, 0f, fac, 3.6f, false, Random.value < 0.5f);
                            if (Random.value < 0.3f) b["dark_metal"].Cylinder(L.c + Vector3.up * (built + top + 0.8f), 0.6f, 0.1f, R(15f, 35f), 6);
                        }
                        if (h < 60f && Random.value < 0.4f) b[Pick("neon_a", "neon_b")].Box(L.c + new Vector3(0, R(6f, 12f), -fd / 2 - 0.15f), new Vector3(fw * 0.5f, 1.4f, 0.2f));
                    }
                }

            // suburbs ring (low-rise, simple) out to 900 m for a continuous skyline
            for (int k = 0; k < 420; k++)
            {
                float ang = R(0, Mathf.PI * 2), r = R(320f, 1000f);
                var p = new Vector3(Mathf.Sin(ang) * r, 0, Mathf.Cos(ang) * r);
                if (coastal && p.z < seaZ + 10f) continue;
                float h = R(9f, 45f) * Mathf.Lerp(1.2f, 0.5f, Mathf.InverseLerp(320f, 1000f, r));
                Tower(b, p, R(14f, 36f), R(14f, 36f), h, R(0, 90f), Pick("facade_apart", "facade_office", "facade_apart", "facade_brick"), 3f, false);
            }
            Backdrop(s, 1300f, 3600f, 340f, s.seaDir);
            b.Flush(s.root, "City", s.mats);
            Cars(s, props, 110, 7f, 14f);
            s.volcanoPos = new Vector3(-900f, 0, 2300f);
        }

        static void Park(BuiltScene s, Props props, MeshBatch b, Vector3 c, float size)
        {
            b["grass"].Box(c, new Vector3(size, 0.08f, size));
            b["plaza"].Box(c + Vector3.up * 0.08f, new Vector3(size, 0.02f, 5f));
            b["plaza"].Box(c + Vector3.up * 0.08f, new Vector3(5f, 0.02f, size));
            for (int i = 0; i < 26; i++)
            {
                var p = c + new Vector3(R(-size / 2 + 3, size / 2 - 3), 0.08f, R(-size / 2 + 3, size / 2 - 3));
                if (Mathf.Abs(p.x - c.x) < 4f || Mathf.Abs(p.z - c.z) < 4f) continue;
                props.Tree(s.root, Random.value < 0.3f ? TreeType.Conifer : TreeType.Broadleaf, p, R(0.7f, 1.1f), R(0, 360));
            }
            b["wall_stone"].Cylinder(c + Vector3.up * 0.1f, 6f, 6f, 0.6f, 16);
            b["concrete"].Cylinder(c + Vector3.up * 0.7f, 1.2f, 0.6f, 2.2f, 8);
            for (int k = 0; k < 4; k++) props.Bench(b, c + Quaternion.Euler(0, k * 90 + 45, 0) * new Vector3(0, 0.08f, 9f), k * 90f + 45f);
            for (int k = 0; k < 4; k++) props.Lamp(b, c + Quaternion.Euler(0, k * 90 + 20, 0) * new Vector3(0, 0.08f, 14f), k * 90f, 4.5f, 0.3f);
        }

        static void PlazaTower(BuiltScene s, MeshBatch b, Vector3 c, float size, float tall)
        {
            b["plaza"].Box(c, new Vector3(size - 4f, 0.05f, size - 4f));
            // landmark: tapered tower made of stacked shrinking boxes + spire
            float y = 0f, w = 34f;
            for (int i = 0; i < 6; i++)
            {
                float h = 38f - i * 3f;
                b["facade_glass"].Box(c + Vector3.up * y, new Vector3(w, h, w), 45f, 1f, false);
                b["roof_flat"].Box(c + Vector3.up * (y + h), new Vector3(w * 1.02f, 0.6f, w * 1.02f), 45f);
                y += h + 0.6f; w *= 0.84f;
            }
            b["metal"].Cylinder(c + Vector3.up * y, 1.6f, 0.15f, 55f, 8);
            b["beacon"].Box(c + Vector3.up * (y + 55f), new Vector3(0.8f, 0.8f, 0.8f), 0, 1, true, true);
        }

        // ================================================================== 2. Town

        /// <summary>Mid-sized town: irregular low-rise blocks (shops, brick, small apartments), an apartment complex of
        /// 15–20 storey slabs, a river with two bridges, a church tower and wooded hills around.</summary>
        static void Town(BuiltScene s, Props props, bool coastal)
        {
            var b = new MeshBatch();
            s.focus = new Vector3(0, 25f, 20f);
            s.camDistance = 360f; s.camHeight = 75f; s.camYaw = 35f;
            s.extent = 260f;
            s.floodLevel = 1.0f;
            s.fireCenter = new Vector3(-420f, 0, 520f);
            // river along z = 150 (west → east), 30 m wide; the ground leaves a gap for it
            const float riverZ = 150f, riverW = 30f;
            float seaEdge = coastal ? -420f : -3500f;
            GroundRect(s, "grass", -3500f, 3500f, seaEdge, riverZ - riverW / 2 - 2f, -0.05f);
            GroundRect(s, "grass", -3500f, 3500f, riverZ + riverW / 2 + 2f, 3500f, -0.05f);
            if (coastal)
            {
                s.seaMat = s.mats.NewWater(new Color(0.05f, 0.17f, 0.25f), new Color(0.12f, 0.34f, 0.38f), 0.45f, 0.18f);
                s.seaLevel = -1.2f; s.shoreDistance = 420f;
                Water(s, "Sea", s.seaMat, -4000f, 4000f, -4000f, seaEdge + 4f, s.seaLevel, new Vector2(0, seaEdge));
                b["sand"].Box(new Vector3(0, -2.5f, seaEdge + 10f), new Vector3(4000f, 2.45f, 30f));
            }
            s.lakeMat = s.mats.NewWater(new Color(0.08f, 0.2f, 0.22f), new Color(0.16f, 0.32f, 0.3f), 0.08f, 0.0f);
            s.lakeMat.SetFloat("_WaveLength", 6f);
            Water(s, "River", s.lakeMat, -2000f, 2000f, riverZ - riverW / 2, riverZ + riverW / 2, -1.2f, new Vector2(0, riverZ), 3f, 200f);
            b["dirt"].Box(new Vector3(0, -2.2f, riverZ), new Vector3(4000f, 1f, riverW + 2f));                   // river bed
            b["concrete"].Box(new Vector3(0, -1.6f, riverZ - riverW / 2 - 1f), new Vector3(4000f, 1.65f, 2f));    // embankments
            b["concrete"].Box(new Vector3(0, -1.6f, riverZ + riverW / 2 + 1f), new Vector3(4000f, 1.65f, 2f));
            foreach (var bx in new[] { -60f, 120f })
            {
                b["concrete"].Box(new Vector3(bx, -0.7f, riverZ), new Vector3(14f, 0.8f, riverW + 8f));
                b["metal"].Box(new Vector3(bx - 6.8f, 0.1f, riverZ), new Vector3(0.2f, 1f, riverW + 8f));
                b["metal"].Box(new Vector3(bx + 6.8f, 0.1f, riverZ), new Vector3(0.2f, 1f, riverW + 8f));
                b["asphalt"].Box(new Vector3(bx, 0.1f, riverZ), new Vector3(10f, 0.02f, riverW + 8f));
                b["asphalt"].Box(new Vector3(bx, 0f, 245f), new Vector3(10f, 0.02f, 150f));
                Lane(s, false, new Vector3(bx + 2f, 0f, -220f), new Vector3(bx + 2f, 0f, 320f));
                Lane(s, false, new Vector3(bx - 2f, 0f, 320f), new Vector3(bx - 2f, 0f, -220f));
            }

            // street grid (irregular spacing)
            float[] xs = { -230f, -150f, -60f, 30f, 120f, 210f };
            float[] zs = { -200f, -130f, -55f, 20f, 95f };
            foreach (var x in xs)
            {
                b["asphalt"].Box(new Vector3(x, 0, -37.5f), new Vector3(10f, 0.02f, 345f));
                b["mark_white"].Box(new Vector3(x, 0.025f, -37.5f), new Vector3(0.15f, 0.01f, 345f));
            }
            foreach (var z in zs)
            {
                b["asphalt"].Box(new Vector3(0, 0.005f, z), new Vector3(500f, 0.02f, 10f));
                b["mark_white"].Box(new Vector3(0, 0.03f, z), new Vector3(500f, 0.01f, 0.15f));
                Lane(s, false, new Vector3(-260f, 0, z + 2f), new Vector3(260f, 0, z + 2f));
                Lane(s, false, new Vector3(260f, 0, z - 2f), new Vector3(-260f, 0, z - 2f));
            }
            // riverside road
            b["asphalt"].Box(new Vector3(0, 0, riverZ - riverW / 2 - 9f), new Vector3(4000f, 0.02f, 10f));
            Lane(s, true, new Vector3(-600f, 0, riverZ - riverW / 2 - 7f), new Vector3(600f, 0, riverZ - riverW / 2 - 7f), new Vector3(600f, 0, riverZ - riverW / 2 - 11f), new Vector3(-600f, 0, riverZ - riverW / 2 - 11f));

            for (int i = 0; i < xs.Length - 1; i++)
                for (int j = 0; j < zs.Length - 1; j++)
                {
                    float x0 = xs[i] + 6f, x1 = xs[i + 1] - 6f, z0 = zs[j] + 6f, z1 = zs[j + 1] - 6f;
                    var c = new Vector3((x0 + x1) / 2, 0, (z0 + z1) / 2);
                    b["sidewalk"].Box(c, new Vector3(x1 - x0 + 4f, 0.12f, z1 - z0 + 4f));
                    props.Lamp(b, new Vector3(x0, 0.12f, z0 - 1f), 180f, 6f, 1f);
                    props.Lamp(b, new Vector3(x1, 0.12f, z1 + 1f), 0f, 6f, 1f);
                    // apartment complex in the north-east corner blocks
                    if (i >= 3 && j >= 2)
                    {
                        b["grass"].Box(c + Vector3.up * 0.12f, new Vector3(x1 - x0, 0.03f, z1 - z0));
                        for (int k = 0; k < 2; k++)
                        {
                            var p = new Vector3(c.x, 0.15f, Mathf.Lerp(z0 + 12f, z1 - 12f, k));
                            float h = Tower(b, p, Mathf.Min(48f, x1 - x0 - 10f), 13f, R(42f, 58f), 0f, "facade_apart", 2.9f, false);
                            b["roof_flat"].Box(p + Vector3.up * h, new Vector3(6f, 3f, 6f));
                        }
                        for (int k = 0; k < 6; k++) props.Tree(s.root, TreeType.Street, c + new Vector3(R(-30, 30), 0.15f, R(-4, 4)), R(0.8f, 1.1f), R(0, 360));
                        continue;
                    }
                    if (i == 1 && j == 1)
                    {
                        // church with tower + small square
                        b["plaza"].Box(c + Vector3.up * 0.12f, new Vector3(x1 - x0, 0.03f, z1 - z0));
                        b["wall_stone"].Box(c + new Vector3(0, 0.15f, 8f), new Vector3(14f, 10f, 26f));
                        b["roof_dark"].Gable(c + new Vector3(0, 10.15f, 8f), 26f, 14f, 6f, 90f, 0.4f);
                        b["wall_stone"].Box(c + new Vector3(0, 0.15f, -8f), new Vector3(7f, 26f, 7f));
                        b["roof_dark"].Cone(c + new Vector3(0, 26.15f, -8f), 5f, 10f, 4);
                        b["window_warm"].Box(c + new Vector3(0, 19f, -11.55f), new Vector3(2f, 3f, 0.1f));
                        continue;
                    }
                    // street-front buildings around the block perimeter
                    float wTot = x1 - x0;
                    for (float x = x0 + 2f; x < x1 - 8f; x += R(10f, 16f))
                    {
                        float w = R(8f, 13f); if (x + w > x1 - 2f) w = x1 - 2f - x;
                        foreach (int side in new[] { -1, 1 })
                        {
                            float z = side < 0 ? z0 + 6f : z1 - 6f;
                            float floors = RI(2, 7);
                            string fac = Pick("facade_brick", "facade_apart", "facade_office", "facade_brick");
                            var p = new Vector3(x + w / 2, 0.12f, z);
                            b["facade_shop"].Box(p, new Vector3(w, 3.4f, 10f), 0, 1, false);
                            Tower(b, p + Vector3.up * 3.4f, w, 10f, floors * 3.2f, 0f, fac, 3.2f, Random.value < 0.4f);
                            if (Random.value < 0.35f) b[Pick("cloth_a", "cloth_b", "cloth_c", "cloth_d")].Box(p + new Vector3(0, 3f, side * 5.6f), new Vector3(w * 0.9f, 0.15f, 1.6f));
                            if (Random.value < 0.15f) b[Pick("neon_a", "neon_b")].Box(p + new Vector3(0, 4.6f, side * 5.1f), new Vector3(w * 0.5f, 0.9f, 0.15f));
                        }
                    }
                    for (int k = 0; k < 3; k++) props.Tree(s.root, TreeType.Street, c + new Vector3(R(-wTot * 0.3f, wTot * 0.3f), 0.12f, R(-6f, 6f)), R(0.8f, 1f), R(0, 360));
                }

            // houses spreading out of the centre
            for (int k = 0; k < 240; k++)
            {
                float ang = R(0, Mathf.PI * 2), r = R(280f, 750f);
                var p = new Vector3(Mathf.Sin(ang) * r, 0, Mathf.Cos(ang) * r);
                if (Mathf.Abs(p.z - riverZ) < 30f) continue;
                if (Random.value < 0.75f) props.House(b, p, R(8f, 13f), R(7f, 10f), R(3f, 6.5f), R(0, 90), Pick("facade_house", "facade_brick", "facade_house"), Pick("roof_red", "roof_blue", "roof_green", "roof_dark", "roof_orange"));
                else Tower(b, p, R(14f, 30f), R(12f, 20f), R(12f, 30f), R(0, 90), "facade_apart", 2.9f, false);
                if (Random.value < 0.5f) props.Tree(s.root, TreeType.Broadleaf, p + new Vector3(R(8, 12), 0, R(-6, 6)), R(0.7f, 1.1f), R(0, 360));
            }
            if (coastal) { s.seaDir = Vector3.back; }
            Backdrop(s, 800f, 3200f, 230f, coastal ? Vector3.back : Vector3.zero);
            // forested hills nearer (trees on the backdrop foot)
            for (int k = 0; k < 160; k++)
            {
                float ang = R(0, Mathf.PI * 2), r = R(820f, 1050f);
                if (coastal && Mathf.Cos(ang) < -0.2f) continue;
                props.Tree(s.root, Random.value < 0.5f ? TreeType.Conifer : TreeType.Broadleaf, new Vector3(Mathf.Sin(ang) * r, 0, Mathf.Cos(ang) * r), R(1.5f, 2.6f), R(0, 360), null, 0.6f);
            }
            b.Flush(s.root, "Town", s.mats);
            Cars(s, props, 40, 6f, 11f);
            s.volcanoPos = new Vector3(600f, 0, 2100f);
        }
    }
}
