using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using WeatherScape.World;

namespace WeatherScape.Effects
{
    public enum Season { Spring, Summer, Autumn, Winter }

    /// <summary>Per-frame life of a built scene: wind sway, traffic, bobbing boats, lighthouse, night lights,
    /// and material changes for wetness, snow cover, season colours and lit windows.</summary>
    public class SceneAnimator
    {
        readonly BuiltScene s;
        readonly List<Light> pool = new List<Light>();
        readonly Transform lightRoot;
        Material beamMat;
        float nextPoolUpdate;
        Look look;
        Season season;
        public float trafficFactor = 1f;

        public SceneAnimator(BuiltScene scene, int lightCount = 28)
        {
            s = scene;
            lightRoot = new GameObject("NightLights") { hideFlags = HideFlags.DontSave }.transform;
            lightRoot.SetParent(s.root, false);
            for (int i = 0; i < lightCount; i++)
            {
                var go = new GameObject("Lamp" + i) { hideFlags = HideFlags.DontSave };
                go.transform.SetParent(lightRoot, false);
                var l = go.AddComponent<Light>();
                l.type = LightType.Point; l.range = 22f; l.intensity = 0f; l.color = new Color(1f, 0.78f, 0.5f);
                l.shadows = LightShadows.None; l.enabled = false;
                pool.Add(l);
            }
            if (s.lighthouseBeam != null)
            {
                var b = new MeshBuilder();
                b.Cylinder(Vector3.zero, 0.6f, 14f, 260f, 10, false);
                var m = b.ToMesh("Beam"); s.mats.Own(m);
                var go = new GameObject("BeamCone") { hideFlags = HideFlags.DontSave };
                go.transform.SetParent(s.lighthouseBeam, false);
                go.transform.localRotation = Quaternion.Euler(90f, 0, 0);
                go.AddComponent<MeshFilter>().sharedMesh = m;
                var r = go.AddComponent<MeshRenderer>();
                beamMat = s.mats.NewParticle(Tex.Dot(8, 0f), true);
                beamMat.SetColor("_BaseColor", new Color(1f, 0.9f, 0.65f, 0.0f));
                beamMat.SetInt("_Cull", (int)CullMode.Off);
                r.sharedMaterial = beamMat;
                r.shadowCastingMode = ShadowCastingMode.Off;
            }
        }

        public static Season SeasonFor(Conditions c)
        {
            double lat = c.place?.lat ?? 37.5;
            if (System.Math.Abs(lat) < 23.0) return Season.Summer;
            var local = System.DateTime.UtcNow.AddSeconds(c.weather?.utcOffsetSeconds ?? 0);
            int m = local.Month;
            if (lat < 0) m = (m + 5) % 12 + 1;
            return m >= 3 && m <= 5 ? Season.Spring : m >= 6 && m <= 8 ? Season.Summer : m >= 9 && m <= 11 ? Season.Autumn : Season.Winter;
        }

        static Color SeasonTint(Mats.Info i, string key, Season se)
        {
            if (!(i.foliage || i.crop)) return i.baseColor;
            switch (se)
            {
                case Season.Spring:
                    if (key == "leaf2") return new Color(0.96f, 0.72f, 0.82f);
                    if (i.crop) return key == "field_b" ? new Color(0.45f, 0.55f, 0.28f) : new Color(0.40f, 0.58f, 0.25f);
                    return Color.Lerp(i.baseColor, new Color(0.42f, 0.62f, 0.25f), 0.5f);
                case Season.Autumn:
                    if (key == "leaf") return new Color(0.78f, 0.45f, 0.13f);
                    if (key == "leaf2") return new Color(0.86f, 0.25f, 0.10f);
                    if (key == "hedge") return new Color(0.5f, 0.42f, 0.18f);
                    if (i.crop) return key == "field_c" ? new Color(0.55f, 0.45f, 0.25f) : new Color(0.84f, 0.70f, 0.30f);
                    if (key == "mountain") return new Color(0.38f, 0.36f, 0.20f);
                    return Color.Lerp(i.baseColor, new Color(0.55f, 0.50f, 0.26f), 0.55f);
                case Season.Winter:
                    if (key == "leaf" || key == "leaf2" || key == "hedge") return new Color(0.40f, 0.34f, 0.28f);
                    if (i.crop) return new Color(0.46f, 0.39f, 0.29f);
                    if (key == "mountain") return new Color(0.30f, 0.30f, 0.24f);
                    return Color.Lerp(i.baseColor, new Color(0.50f, 0.48f, 0.36f), 0.6f);
                default: return i.baseColor;
            }
        }

        /// <summary>Material state for the current look. Cheap enough to call every frame while blending.</summary>
        public void ApplyMaterials(Look L, Season se)
        {
            look = L; season = se;
            Color snowC = new Color(0.93f, 0.95f, 0.98f);
            float lampsOn = Mathf.Clamp01((L.night - 0.35f) * 3f);
            foreach (var kv in s.mats.all)
            {
                var i = kv.Value; var m = i.mat;
                Color baseC = SeasonTint(i, kv.Key, se);
                float wet = L.wet * i.wet;
                Color c = baseC * (1f - 0.32f * wet);
                c = Color.Lerp(c, snowC, L.snow * i.snow);
                c.a = 1f;
                if (m.HasProperty("_BaseColor") && kv.Key.StartsWith("facade") == false) m.SetColor("_BaseColor", c);
                float sm = Mathf.Lerp(i.smooth, 0.88f, wet);
                sm = Mathf.Lerp(sm, 0.25f, L.snow * i.snow);
                m.SetFloat("_Smoothness", sm);
                if (i.emission.maxColorComponent > 0f)
                {
                    float k = i.alwaysEmissive ? 1f : kv.Key.StartsWith("facade") || kv.Key == "window_warm" ? Mathf.Clamp01((L.night - 0.2f) * 1.6f) : lampsOn;
                    if (kv.Key == "beacon") k = Mathf.Max(k, 0.35f);
                    m.SetColor("_EmissionColor", i.emission * k);
                }
            }
            for (int k = 0; k < pool.Count; k++) pool[k].intensity = lampsOn * 5f;
            if (beamMat != null) beamMat.SetColor("_BaseColor", new Color(1f, 0.9f, 0.65f, 0.09f * Mathf.Clamp01(L.night * 1.5f + L.fogDensity * 60f)));
        }

        public void Update(float t, float dt, Vector3 camPos, float waveAmp)
        {
            // wind sway (trees bend downwind, with gusts)
            var wdir = new Vector3(look.wind.x, 0, look.wind.y);
            Vector3 axis = wdir.sqrMagnitude > 0.001f ? Vector3.Cross(Vector3.up, wdir).normalized : Vector3.right;
            float ws = look.windStrength;
            for (int i = 0; i < s.sway.Count; i++)
            {
                var sw = s.sway[i];
                if (sw.t == null) continue;
                float gust = 0.6f + 0.4f * Mathf.Sin(t * 0.7f + sw.phase * 0.3f) * Mathf.Sin(t * 1.9f + sw.phase);
                float bend = ws * ws * 16f * sw.flex * gust + ws * 2f;
                float shake = Mathf.Sin(t * (1.6f + ws * 2.5f) + sw.phase) * (0.6f + ws * 5f) * sw.flex;
                sw.t.localRotation = Quaternion.AngleAxis(bend + shake, axis) * sw.baseRot;
            }
            // traffic
            for (int i = 0; i < s.cars.Count; i++)
            {
                var c = s.cars[i];
                if (c.t == null) continue;
                c.s += c.speed * dt * trafficFactor;
                if (c.s > c.lane.length) c.s -= c.lane.length;
                Eval(c.lane, c.s, out var pos, out var dir);
                c.t.localPosition = pos;
                if (dir.sqrMagnitude > 0.0001f) c.t.localRotation = Quaternion.LookRotation(dir);
                s.cars[i] = c;
            }
            // boats
            float a = Mathf.Clamp(waveAmp, 0.05f, 3f);
            for (int i = 0; i < s.boats.Count; i++)
            {
                var b = s.boats[i];
                if (b.t == null) continue;
                float ph = b.phase;
                float sc = Mathf.Clamp(b.t.localScale.x / 12f, 0.3f, 5f);
                b.t.localPosition = b.basePos + Vector3.up * (Mathf.Sin(t * 0.9f + ph) * a * 0.35f);
                b.t.localRotation = b.baseRot * Quaternion.Euler(Mathf.Sin(t * 0.8f + ph) * a * 3f / sc, 0, Mathf.Sin(t * 1.1f + ph * 1.3f) * a * 4f / sc);
            }
            foreach (var sp in s.spinning) if (sp != null) sp.localRotation = Quaternion.Euler(0, t * 36f, 0);

            // night point lights: the lamps nearest to the camera's view centre
            if (t >= nextPoolUpdate || t < nextPoolUpdate - 5f)
            {
                nextPoolUpdate = t + 0.5f;
                bool on = look.night > 0.4f && s.lamps.Count > 0;
                if (!on) { foreach (var l in pool) l.enabled = false; }
                else
                {
                    var near = new List<(float d, Vector3 p)>(s.lamps.Count);
                    Vector3 target = Vector3.Lerp(camPos, s.focus, 0.6f);
                    foreach (var p in s.lamps) near.Add(((p - target).sqrMagnitude, p));
                    near.Sort((x, y) => x.d.CompareTo(y.d));
                    for (int k = 0; k < pool.Count; k++)
                    {
                        bool en = k < near.Count;
                        pool[k].enabled = en;
                        if (en) pool[k].transform.localPosition = near[k].p;
                    }
                }
            }
        }

        static void Eval(BuiltScene.Lane l, float s, out Vector3 pos, out Vector3 dir)
        {
            int n = l.pts.Count;
            int segs = l.loop ? n : n - 1;
            for (int i = 0; i < segs; i++)
            {
                Vector3 a = l.pts[i], b = l.pts[(i + 1) % n];
                float len = Vector3.Distance(a, b);
                if (s <= len || i == segs - 1)
                {
                    float t = len > 0 ? Mathf.Clamp01(s / len) : 0f;
                    pos = Vector3.Lerp(a, b, t);
                    dir = b - a;
                    return;
                }
                s -= len;
            }
            pos = l.pts[0]; dir = Vector3.forward;
        }

        public void Dispose()
        {
            if (lightRoot != null) Object.DestroyImmediate(lightRoot.gameObject);
        }
    }
}
