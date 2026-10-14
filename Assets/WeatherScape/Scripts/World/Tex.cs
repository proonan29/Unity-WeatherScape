using UnityEngine;

namespace WeatherScape.World
{
    /// <summary>Procedural textures: facades with lit windows, ground noise and particle sprites.</summary>
    public static class Tex
    {
        public const int Bays = 8;      // facade texture = 8 × 8 window bays (so lit windows don't repeat every bay)
        const int Px = 32;              // pixels per bay

        public enum FacadeStyle { Glass, Office, Apartment, Brick, Shop, House, Wood }

        static Texture2D New(int w, int h, bool linear = false, bool mips = true)
        {
            var t = new Texture2D(w, h, TextureFormat.RGBA32, mips, linear) { wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Bilinear, anisoLevel = 4 };
            t.hideFlags = HideFlags.DontSave;
            return t;
        }

        static float Hash(int x, int y, int s)
        {
            unchecked
            {
                uint h = (uint)(x * 374761393 + y * 668265263 + s * 2147483647);
                h = (h ^ (h >> 13)) * 1274126177u;
                return (h ^ (h >> 16)) / (float)uint.MaxValue;
            }
        }

        public static Texture2D Noise(int seed, float amount)
        {
            const int N = 256;
            var t = New(N, N);
            var px = new Color32[N * N];
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    float v = 0f, a = 0.5f, tot = 0f;
                    for (int o = 0; o < 4; o++)
                    {
                        int cell = 4 << o;
                        float fx = x * cell / (float)N, fy = y * cell / (float)N;
                        int ix = Mathf.FloorToInt(fx), iy = Mathf.FloorToInt(fy);
                        float tx = Mathf.SmoothStep(0, 1, fx - ix), ty = Mathf.SmoothStep(0, 1, fy - iy);
                        float h00 = Hash(ix % cell, iy % cell, seed + o), h10 = Hash((ix + 1) % cell, iy % cell, seed + o);
                        float h01 = Hash(ix % cell, (iy + 1) % cell, seed + o), h11 = Hash((ix + 1) % cell, (iy + 1) % cell, seed + o);
                        v += a * Mathf.Lerp(Mathf.Lerp(h00, h10, tx), Mathf.Lerp(h01, h11, tx), ty);
                        tot += a; a *= 0.5f;
                    }
                    v /= tot;
                    float g = 1f - amount + amount * 2f * v;
                    byte b = (byte)Mathf.Clamp(g * 230f, 0, 255);
                    px[y * N + x] = new Color32(b, b, b, 255);
                }
            t.SetPixels32(px);
            t.Apply(true);
            return t;
        }

        public static (Texture2D albedo, Texture2D emission) Facade(FacadeStyle style, int seed)
        {
            int N = Bays * Px;
            var a = New(N, N); var e = New(N, N);
            var pa = new Color[N * N]; var pe = new Color[N * N];
            Color wall, frame, glassC; float litChance; Color litTint;
            switch (style)
            {
                case FacadeStyle.Glass: wall = new Color(0.30f, 0.38f, 0.46f); frame = new Color(0.62f, 0.66f, 0.70f); glassC = new Color(0.22f, 0.30f, 0.38f); litChance = 0.38f; litTint = new Color(0.95f, 0.9f, 0.78f); break;
                case FacadeStyle.Office: wall = new Color(0.70f, 0.68f, 0.64f); frame = new Color(0.55f, 0.55f, 0.55f); glassC = new Color(0.20f, 0.25f, 0.30f); litChance = 0.45f; litTint = new Color(1f, 0.95f, 0.8f); break;
                case FacadeStyle.Apartment: wall = new Color(0.88f, 0.86f, 0.80f); frame = new Color(0.70f, 0.70f, 0.70f); glassC = new Color(0.24f, 0.30f, 0.34f); litChance = 0.55f; litTint = new Color(1f, 0.82f, 0.55f); break;
                case FacadeStyle.Brick: wall = new Color(0.56f, 0.28f, 0.21f); frame = new Color(0.85f, 0.82f, 0.75f); glassC = new Color(0.18f, 0.22f, 0.26f); litChance = 0.5f; litTint = new Color(1f, 0.8f, 0.5f); break;
                case FacadeStyle.Shop: wall = new Color(0.82f, 0.80f, 0.76f); frame = new Color(0.25f, 0.25f, 0.27f); glassC = new Color(0.25f, 0.32f, 0.36f); litChance = 0.8f; litTint = new Color(1f, 0.92f, 0.75f); break;
                case FacadeStyle.Wood: wall = new Color(0.50f, 0.35f, 0.22f); frame = new Color(0.30f, 0.20f, 0.12f); glassC = new Color(0.85f, 0.80f, 0.68f); litChance = 0.5f; litTint = new Color(1f, 0.75f, 0.45f); break;
                default: wall = new Color(0.89f, 0.86f, 0.79f); frame = new Color(0.75f, 0.72f, 0.66f); glassC = new Color(0.22f, 0.27f, 0.30f); litChance = 0.5f; litTint = new Color(1f, 0.78f, 0.5f); break;
            }
            for (int by = 0; by < Bays; by++)
                for (int bx = 0; bx < Bays; bx++)
                {
                    bool lit = Hash(bx, by, seed) < litChance;
                    bool hasWin = style != FacadeStyle.House || Hash(bx, by, seed + 7) < 0.55f;
                    float shade = 0.9f + Hash(bx, by, seed + 3) * 0.2f;
                    for (int y = 0; y < Px; y++)
                        for (int x = 0; x < Px; x++)
                        {
                            int gx = bx * Px + x, gy = by * Px + y;
                            Color c = wall; Color em = Color.black;
                            bool win = false, fr = false;
                            switch (style)
                            {
                                case FacadeStyle.Glass:
                                    win = y >= 6; fr = x < 1 || x >= Px - 1 || y == 6 || y == 5;
                                    if (y < 5) c = wall * 0.75f;
                                    break;
                                case FacadeStyle.Office: win = x >= 4 && x < 28 && y >= 9 && y < 27; fr = win && (x == 4 || x == 27 || y == 9 || y == 26 || x == 16); break;
                                case FacadeStyle.Apartment:
                                    win = x >= 3 && x < 29 && y >= 10 && y < 25; fr = win && (x == 3 || x == 28 || x == 16 || y == 24);
                                    if (y >= 8 && y < 11) { c = new Color(0.62f, 0.62f, 0.64f); win = false; } // balcony rail
                                    break;
                                case FacadeStyle.Brick:
                                    if ((y % 4 == 0) || ((x + (y / 4 % 2) * 4) % 8 == 0)) c = new Color(0.72f, 0.68f, 0.62f);
                                    win = x >= 10 && x < 22 && y >= 8 && y < 26; fr = win && (x == 10 || x == 21 || y == 8 || y == 25 || y == 17);
                                    break;
                                case FacadeStyle.Shop: win = x >= 2 && x < 30 && y >= 4 && y < 26; fr = win && (x == 2 || x == 29 || y == 25 || x == 16); if (y >= 27 && y < 31) c = new Color(0.6f, 0.15f + 0.4f * Hash(bx, 0, seed), 0.2f); break;
                                case FacadeStyle.Wood:
                                    if (y % 5 == 0) c = wall * 0.8f;
                                    win = x >= 11 && x < 21 && y >= 11 && y < 23; fr = win && (x == 11 || x == 20 || y == 11 || y == 22 || x == 16);
                                    break;
                                default: win = x >= 11 && x < 21 && y >= 10 && y < 24; fr = win && (x == 11 || x == 20 || y == 10 || y == 23 || y == 17); break;
                            }
                            if (!hasWin) { win = false; fr = false; }
                            if (win)
                            {
                                c = fr ? frame : glassC * shade;
                                if (style == FacadeStyle.Glass) c = fr ? frame : (lit ? glassC * 1.15f : glassC) * shade;
                                if (lit && !fr)
                                {
                                    // soft glow, brighter towards the middle of the pane
                                    float k = 0.75f + 0.25f * Hash(gx / 3, gy / 3, seed + 11);
                                    em = litTint * k;
                                    if (style != FacadeStyle.Glass) c = Color.Lerp(c, litTint * 0.6f, 0.35f);
                                }
                            }
                            else c *= shade;
                            c.a = 1f;
                            pa[gy * N + gx] = c; pe[gy * N + gx] = em;
                        }
                }
            a.SetPixels(pa); a.Apply(true);
            e.SetPixels(pe); e.Apply(true);
            a.name = $"Facade_{style}"; e.name = $"FacadeEmit_{style}";
            return (a, e);
        }

        // ---------------------------------------------------------------- particle sprites

        public static Texture2D Dot(int size = 32, float hardness = 0.3f)
        {
            var t = New(size, size, false, true); t.wrapMode = TextureWrapMode.Clamp;
            var px = new Color[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = (x + 0.5f) / size * 2 - 1, dy = (y + 0.5f) / size * 2 - 1;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    float aa = Mathf.Clamp01((1f - d) / (1f - hardness));
                    px[y * size + x] = new Color(1, 1, 1, aa * aa);
                }
            t.SetPixels(px); t.Apply(true);
            return t;
        }

        public static Texture2D Streak()
        {
            const int W = 8, H = 64;
            var t = New(W, H, false, true); t.wrapMode = TextureWrapMode.Clamp;
            var px = new Color[W * H];
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    float dx = Mathf.Abs((x + 0.5f) / W * 2 - 1);
                    float vy = (y + 0.5f) / H;
                    float a = Mathf.Clamp01(1f - dx * 1.4f) * Mathf.Sin(vy * Mathf.PI);
                    px[y * W + x] = new Color(1, 1, 1, a);
                }
            t.SetPixels(px); t.Apply(true);
            return t;
        }

        public static Texture2D Puff(int seed)
        {
            const int N = 64;
            var t = New(N, N, false, true); t.wrapMode = TextureWrapMode.Clamp;
            var px = new Color[N * N];
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    float dx = (x + 0.5f) / N * 2 - 1, dy = (y + 0.5f) / N * 2 - 1;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    float n = 0.65f + 0.35f * Mathf.PerlinNoise(x * 0.11f + seed, y * 0.11f + seed * 0.37f);
                    float a = Mathf.Clamp01(1f - d) * n;
                    px[y * N + x] = new Color(1, 1, 1, a * a * 1.4f);
                }
            t.SetPixels(px); t.Apply(true);
            return t;
        }
    }
}
