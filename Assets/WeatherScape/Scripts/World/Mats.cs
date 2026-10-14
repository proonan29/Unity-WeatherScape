using System.Collections.Generic;
using UnityEngine;

namespace WeatherScape.World
{
    /// <summary>Runtime material library (URP Lit clones). Each material remembers its base look so weather can
    /// add snow cover, wetness and night lighting without touching asset files.</summary>
    public class Mats
    {
        public class Info
        {
            public Material mat;
            public Color baseColor; public float smooth;
            public float snow, wet;          // how much snow sticks / how shiny it gets in rain (0..1)
            public Color emission;           // night emission (black = none)
            public bool foliage, crop;       // recoloured by season
            public bool alwaysEmissive;      // e.g. lava
        }

        public readonly Dictionary<string, Info> all = new Dictionary<string, Info>();
        readonly List<Object> owned = new List<Object>();
        Material litTemplate, particleTemplate;
        Texture2D noiseTex;
        public Shader waterShader;
        public int seed;

        public Mats(int seed)
        {
            this.seed = seed;
            litTemplate = Resources.Load<Material>("WS_Lit");
            particleTemplate = Resources.Load<Material>("WS_Particle");
            waterShader = Shader.Find("WeatherScape/Water");
        }

        public IEnumerable<Info> Infos => all.Values;

        Material NewLit()
        {
            Material m = litTemplate != null ? new Material(litTemplate) : new Material(Shader.Find("Universal Render Pipeline/Lit"));
            m.hideFlags = HideFlags.DontSave;
            m.enableInstancing = true;
            owned.Add(m);
            return m;
        }

        public Material NewParticle(Texture2D tex, bool additive)
        {
            Material m = particleTemplate != null ? new Material(particleTemplate) : new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
            m.hideFlags = HideFlags.DontSave;
            m.SetTexture("_BaseMap", tex);
            // URP particle surface: transparent, blend alpha (0) or additive (2)
            m.SetFloat("_Surface", 1f);
            m.SetFloat("_Blend", additive ? 2f : 0f);
            m.SetOverrideTag("RenderType", "Transparent");
            m.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            m.SetInt("_DstBlend", additive ? (int)UnityEngine.Rendering.BlendMode.One : (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            m.SetInt("_ZWrite", 0);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.renderQueue = 3000;
            owned.Add(m);
            return m;
        }

        public Material NewWater(Color deep, Color shallow, float amp, float foam)
        {
            var m = new Material(waterShader != null ? waterShader : Shader.Find("Universal Render Pipeline/Lit")) { hideFlags = HideFlags.DontSave };
            m.SetColor("_DeepColor", deep);
            m.SetColor("_ShallowColor", shallow);
            m.SetFloat("_WaveAmp", amp);
            m.SetFloat("_Foam", foam);
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", deep);
            owned.Add(m);
            return m;
        }

        public void Own(Object o) { if (o != null) { o.hideFlags = HideFlags.DontSave; owned.Add(o); } }

        public void Dispose()
        {
            foreach (var o in owned) if (o != null) { if (Application.isPlaying) Object.Destroy(o); else Object.DestroyImmediate(o); }
            owned.Clear();
            all.Clear();
        }

        // ------------------------------------------------------------------ definitions

        static Color C(float r, float g, float b) => new Color(r, g, b);

        public Material Get(string key)
        {
            if (all.TryGetValue(key, out var info)) return info.mat;
            info = Define(key);
            all[key] = info;
            return info.mat;
        }

        Info Lit(Color c, float smooth = 0.15f, float metal = 0f, float snow = 0f, float wet = 0.5f)
        {
            var m = NewLit();
            m.SetColor("_BaseColor", c);
            m.SetFloat("_Smoothness", smooth);
            m.SetFloat("_Metallic", metal);
            return new Info { mat = m, baseColor = c, smooth = smooth, snow = snow, wet = wet, emission = Color.black };
        }

        Info Emissive(Color c, Color emit, bool always = false)
        {
            var i = Lit(c, 0.4f);
            i.emission = emit; i.alwaysEmissive = always;
            i.mat.EnableKeyword("_EMISSION");
            i.mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            i.mat.SetColor("_EmissionColor", always ? emit : Color.black);
            return i;
        }

        Info Textured(Texture2D tex, Texture2D emitTex, Vector2 tiling, Color emit, float smooth = 0.3f, float metal = 0f, float snow = 0f)
        {
            var i = Lit(Color.white, smooth, metal, snow, 0.4f);
            i.mat.SetTexture("_BaseMap", tex);
            i.mat.SetTextureScale("_BaseMap", tiling);
            if (emitTex != null)
            {
                i.mat.EnableKeyword("_EMISSION");
                i.mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                i.mat.SetTexture("_EmissionMap", emitTex);
                i.mat.SetColor("_EmissionColor", Color.black);
                i.emission = emit;
            }
            return i;
        }

        Info Natural(Color c, float snow, float wet, float tile = 10f, float smooth = 0.08f)
        {
            var i = Lit(c, smooth, 0f, snow, wet);
            if (noiseTex == null) { noiseTex = Tex.Noise(seed, 0.12f); Own(noiseTex); }
            i.mat.SetTexture("_BaseMap", noiseTex);
            i.mat.SetTextureScale("_BaseMap", new Vector2(1f / tile, 1f / tile));
            return i;
        }

        Info Define(string key)
        {
            switch (key)
            {
                // ground & nature
                case "grass": { var i = Natural(C(0.33f, 0.47f, 0.22f), 1f, 0.5f, 32f); i.crop = false; i.foliage = true; return i; }
                case "meadow": { var i = Natural(C(0.40f, 0.52f, 0.25f), 1f, 0.5f, 60f); i.foliage = true; return i; }
                case "urban": return Natural(C(0.42f, 0.43f, 0.40f), 0.95f, 0.7f, 50f);
                case "dirt": return Natural(C(0.46f, 0.37f, 0.27f), 1f, 0.8f);
                case "sand": return Natural(C(0.86f, 0.79f, 0.62f), 0.9f, 0.7f, 6f);
                case "wetsand": return Natural(C(0.66f, 0.58f, 0.44f), 0.5f, 0.9f, 6f, 0.35f);
                case "rock": return Natural(C(0.43f, 0.42f, 0.40f), 0.8f, 0.7f, 6f);
                case "mountain": { var i = Natural(C(0.27f, 0.36f, 0.24f), 0.95f, 0.4f, 40f); i.foliage = true; return i; }
                case "mountain_rock": return Natural(C(0.40f, 0.39f, 0.38f), 0.9f, 0.4f, 30f);
                case "volcano_rock": return Natural(C(0.22f, 0.20f, 0.19f), 0.6f, 0.4f, 30f);
                case "trunk": return Lit(C(0.33f, 0.23f, 0.15f), 0.1f, 0f, 0.1f, 0.5f);
                case "leaf": { var i = Lit(C(0.22f, 0.44f, 0.17f), 0.12f, 0f, 0.65f, 0.5f); i.foliage = true; return i; }
                case "leaf2": { var i = Lit(C(0.30f, 0.50f, 0.20f), 0.12f, 0f, 0.65f, 0.5f); i.foliage = true; return i; }
                case "conifer": return Lit(C(0.12f, 0.30f, 0.17f), 0.12f, 0f, 0.75f, 0.5f);
                case "hedge": { var i = Lit(C(0.18f, 0.38f, 0.16f), 0.1f, 0f, 0.8f, 0.5f); i.foliage = true; return i; }
                case "field_a": { var i = Natural(C(0.42f, 0.60f, 0.22f), 1f, 0.6f, 4f); i.crop = true; return i; }
                case "field_b": { var i = Natural(C(0.70f, 0.64f, 0.30f), 1f, 0.6f, 4f); i.crop = true; return i; }
                case "field_c": { var i = Natural(C(0.30f, 0.46f, 0.20f), 1f, 0.6f, 4f); i.crop = true; return i; }
                case "paddy_water": return Lit(C(0.30f, 0.38f, 0.36f), 0.85f, 0f, 0.9f, 0.2f);

                // city
                case "asphalt": return Lit(C(0.16f, 0.16f, 0.17f), 0.2f, 0f, 0.85f, 1f);
                case "sidewalk": return Lit(C(0.56f, 0.55f, 0.52f), 0.15f, 0f, 0.95f, 0.8f);
                case "plaza": return Natural(C(0.70f, 0.66f, 0.58f), 0.95f, 0.8f, 3f, 0.2f);
                case "mark_white": return Lit(C(0.88f, 0.88f, 0.86f), 0.3f, 0f, 0.8f, 0.5f);
                case "mark_yellow": return Lit(C(0.95f, 0.72f, 0.15f), 0.3f, 0f, 0.8f, 0.5f);
                case "concrete": return Lit(C(0.62f, 0.61f, 0.58f), 0.15f, 0f, 0.2f, 0.6f);
                case "roof_flat": return Lit(C(0.36f, 0.36f, 0.37f), 0.15f, 0f, 1f, 0.8f);
                case "roof_red": return Lit(C(0.60f, 0.21f, 0.15f), 0.25f, 0f, 1f, 0.8f);
                case "roof_blue": return Lit(C(0.22f, 0.34f, 0.52f), 0.3f, 0f, 1f, 0.8f);
                case "roof_green": return Lit(C(0.24f, 0.40f, 0.32f), 0.3f, 0f, 1f, 0.8f);
                case "roof_dark": return Lit(C(0.17f, 0.18f, 0.21f), 0.3f, 0f, 1f, 0.8f);
                case "roof_orange": return Lit(C(0.76f, 0.40f, 0.19f), 0.25f, 0f, 1f, 0.8f);
                case "wall_white": return Lit(C(0.88f, 0.86f, 0.80f), 0.1f, 0f, 0.05f, 0.3f);
                case "wall_cream": return Lit(C(0.85f, 0.77f, 0.60f), 0.1f, 0f, 0.05f, 0.3f);
                case "wall_wood": return Lit(C(0.45f, 0.30f, 0.18f), 0.15f, 0f, 0.05f, 0.4f);
                case "wall_red": return Lit(C(0.58f, 0.15f, 0.11f), 0.2f, 0f, 0.05f, 0.4f);
                case "wall_stone": return Natural(C(0.66f, 0.62f, 0.55f), 0.2f, 0.5f, 3f);
                case "metal": return Lit(C(0.60f, 0.62f, 0.65f), 0.6f, 0.8f, 0.4f, 0.4f);
                case "dark_metal": return Lit(C(0.20f, 0.21f, 0.23f), 0.5f, 0.7f, 0.4f, 0.4f);
                case "glass": return Lit(C(0.18f, 0.24f, 0.30f), 0.92f, 0.3f, 0.1f, 0.1f);
                case "lamp": return Emissive(C(0.95f, 0.92f, 0.8f), new Color(5f, 3.8f, 2.2f));
                case "window_warm": return Emissive(C(0.25f, 0.28f, 0.30f), new Color(2.4f, 1.7f, 0.9f));
                case "beacon": return Emissive(C(0.6f, 0.05f, 0.05f), new Color(6f, 0.3f, 0.2f));
                case "neon_a": return Emissive(C(0.7f, 0.2f, 0.5f), new Color(3f, 0.4f, 2.2f));
                case "neon_b": return Emissive(C(0.2f, 0.6f, 0.8f), new Color(0.4f, 2.4f, 3.4f));
                case "lantern": return Emissive(C(0.85f, 0.25f, 0.15f), new Color(4f, 1.2f, 0.4f));
                case "lava": return Emissive(C(0.3f, 0.05f, 0.0f), new Color(8f, 2.2f, 0.3f), true);
                case "fire": return Emissive(C(0.4f, 0.1f, 0.0f), new Color(6f, 2.5f, 0.5f), true);

                // vehicles, boats, port
                case "car_red": return Lit(C(0.65f, 0.08f, 0.07f), 0.75f, 0.3f, 0.6f, 0.2f);
                case "car_white": return Lit(C(0.88f, 0.88f, 0.88f), 0.75f, 0.3f, 0.6f, 0.2f);
                case "car_black": return Lit(C(0.06f, 0.06f, 0.07f), 0.8f, 0.3f, 0.6f, 0.2f);
                case "car_blue": return Lit(C(0.10f, 0.22f, 0.55f), 0.75f, 0.3f, 0.6f, 0.2f);
                case "car_silver": return Lit(C(0.55f, 0.57f, 0.60f), 0.8f, 0.6f, 0.6f, 0.2f);
                case "car_yellow": return Lit(C(0.92f, 0.70f, 0.08f), 0.7f, 0.2f, 0.6f, 0.2f);
                case "headlight": return Emissive(C(0.9f, 0.9f, 0.85f), new Color(6f, 5.6f, 4.8f));
                case "taillight": return Emissive(C(0.5f, 0.05f, 0.05f), new Color(4f, 0.2f, 0.15f));
                case "hull_white": return Lit(C(0.90f, 0.90f, 0.88f), 0.5f, 0f, 0.6f, 0.2f);
                case "hull_red": return Lit(C(0.62f, 0.12f, 0.10f), 0.5f, 0f, 0.6f, 0.2f);
                case "hull_blue": return Lit(C(0.12f, 0.24f, 0.45f), 0.5f, 0f, 0.6f, 0.2f);
                case "deck_wood": return Lit(C(0.55f, 0.40f, 0.26f), 0.2f, 0f, 0.9f, 0.7f);
                case "crane": return Lit(C(0.85f, 0.55f, 0.10f), 0.4f, 0.3f, 0.6f, 0.3f);
                case "cont_a": return Lit(C(0.70f, 0.18f, 0.12f), 0.3f, 0.3f, 1f, 0.4f);
                case "cont_b": return Lit(C(0.14f, 0.36f, 0.62f), 0.3f, 0.3f, 1f, 0.4f);
                case "cont_c": return Lit(C(0.20f, 0.52f, 0.30f), 0.3f, 0.3f, 1f, 0.4f);
                case "cont_d": return Lit(C(0.82f, 0.62f, 0.18f), 0.3f, 0.3f, 1f, 0.4f);
                case "lh_white": return Lit(C(0.92f, 0.92f, 0.90f), 0.3f, 0f, 0.3f, 0.3f);
                case "lh_red": return Lit(C(0.70f, 0.10f, 0.08f), 0.3f, 0f, 0.3f, 0.3f);
                case "lh_light": return Emissive(C(1f, 0.95f, 0.8f), new Color(12f, 11f, 8f));
                case "buoy": return Lit(C(0.95f, 0.45f, 0.05f), 0.4f, 0f, 0.3f, 0.3f);
                case "umbrella_a": return Lit(C(0.90f, 0.30f, 0.20f), 0.2f, 0f, 1f, 0.3f);
                case "umbrella_b": return Lit(C(0.20f, 0.55f, 0.85f), 0.2f, 0f, 1f, 0.3f);

                // tourist
                case "cloth_a": return Lit(C(0.85f, 0.20f, 0.18f), 0.1f, 0f, 1f, 0.3f);
                case "cloth_b": return Lit(C(0.95f, 0.75f, 0.20f), 0.1f, 0f, 1f, 0.3f);
                case "cloth_c": return Lit(C(0.20f, 0.45f, 0.75f), 0.1f, 0f, 1f, 0.3f);
                case "cloth_d": return Lit(C(0.25f, 0.62f, 0.38f), 0.1f, 0f, 1f, 0.3f);
                case "gold": return Lit(C(0.85f, 0.65f, 0.25f), 0.7f, 0.9f, 0.3f, 0.2f);
                case "dancheong": return Lit(C(0.15f, 0.48f, 0.42f), 0.25f, 0f, 0.1f, 0.3f);
                case "bus": return Lit(C(0.15f, 0.45f, 0.75f), 0.7f, 0.2f, 0.6f, 0.2f);

                // facades (procedural window textures; emissive windows at night)
                case "facade_glass": return Facade(Tex.FacadeStyle.Glass, 3f, 3.6f, new Color(1.3f, 1.25f, 1.1f), 0.85f, 0.4f);
                case "facade_office": return Facade(Tex.FacadeStyle.Office, 3f, 3.6f, new Color(1.8f, 1.6f, 1.2f), 0.35f, 0f);
                case "facade_apart": return Facade(Tex.FacadeStyle.Apartment, 3.2f, 2.9f, new Color(2.2f, 1.6f, 0.9f), 0.2f, 0f);
                case "facade_brick": return Facade(Tex.FacadeStyle.Brick, 3f, 3.2f, new Color(2.2f, 1.5f, 0.8f), 0.15f, 0f);
                case "facade_shop": return Facade(Tex.FacadeStyle.Shop, 3.5f, 3.4f, new Color(2.6f, 2.1f, 1.4f), 0.3f, 0f);
                case "facade_house": return Facade(Tex.FacadeStyle.House, 3f, 2.8f, new Color(2.2f, 1.5f, 0.8f), 0.12f, 0f);
                case "facade_wood": return Facade(Tex.FacadeStyle.Wood, 2.6f, 2.6f, new Color(2.2f, 1.4f, 0.7f), 0.12f, 0f);
            }
            Debug.LogWarning($"[WeatherScape] unknown material key '{key}'");
            return Lit(Color.magenta);
        }

        Info Facade(Tex.FacadeStyle style, float bayW, float floorH, Color emit, float smooth, float metal)
        {
            var (albedo, emission) = Tex.Facade(style, seed + (int)style * 31);
            Own(albedo); Own(emission);
            return Textured(albedo, emission, new Vector2(1f / (bayW * Tex.Bays), 1f / (floorH * Tex.Bays)), emit, smooth, metal, 0f);
        }
    }
}
