using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using WeatherScape.World;

namespace WeatherScape.Effects
{
    /// <summary>Visual effects for special conditions: erupting (or quiet) volcano, earthquake shake + dust,
    /// rising flood water, tsunami wave, wildfire with smoke. Built lazily inside the current scene.</summary>
    public class HazardFX
    {
        readonly BuiltScene s;
        readonly Transform root;
        GameObject volcano, volcanoGlow, lava, fire, flood, tsunami, dust;
        Material floodMat, tsunamiMat;
        ParticleSystem plume, steam, dustPs;
        bool erupting, quake, flooding, tsunamiOn, burning;
        float quakeStart = -100f, floodLevel, tsunamiT;
        public Vector3 shakeOffset; public Vector3 shakeEuler;
        public bool AshFall => erupting;

        public HazardFX(BuiltScene scene)
        {
            s = scene;
            root = new GameObject("Hazards") { hideFlags = HideFlags.DontSave }.transform;
            root.SetParent(s.root, false);
        }

        public void Dispose() { if (root != null) Object.DestroyImmediate(root.gameObject); }

        public void Apply(Conditions c)
        {
            bool volcanoNear = c.Has(HazardKind.Volcano);
            erupting = c.HasEffect(HazardKind.Volcano, Severity.Advisory);
            quake = c.HasEffect(HazardKind.Earthquake, Severity.Advisory);
            tsunamiOn = c.HasEffect(HazardKind.Tsunami, Severity.Warning);
            flooding = c.HasEffect(HazardKind.Flood, Severity.Advisory) || tsunamiOn && s.seaDir == Vector3.zero;
            burning = c.HasEffect(HazardKind.Wildfire, Severity.Advisory);

            if (volcanoNear || erupting) EnsureVolcano();
            if (volcano != null) volcano.SetActive(volcanoNear || erupting);
            if (plume != null) { Play(plume, erupting); var em = plume.emission; em.rateOverTime = erupting ? 45f : 0f; }
            if (steam != null) { Play(steam, volcanoNear && !erupting); var em = steam.emission; em.rateOverTime = volcanoNear && !erupting ? 4f : 0f; }
            if (volcanoGlow != null) volcanoGlow.SetActive(erupting);
            if (lava != null) lava.SetActive(erupting);

            if (quake) EnsureDust();
            if (flooding || tsunamiOn) EnsureFlood();
            if (flood != null) flood.SetActive(flooding || tsunamiOn);
            if (tsunamiOn && s.seaDir != Vector3.zero) EnsureTsunami();
            if (tsunami != null) tsunami.SetActive(tsunamiOn && s.seaDir != Vector3.zero);
            if (burning) EnsureFire();
            if (fire != null) fire.SetActive(burning);
            if (!flooding && !tsunamiOn) floodLevel = 0f;
        }

        static float ConeH(float f, float[] rr, float[] hh)
        {
            for (int i = 0; i < rr.Length - 1; i++)
                if (f <= rr[i] && f >= rr[i + 1]) return Mathf.Lerp(Mathf.Max(hh[i], 0f), hh[i + 1], Mathf.InverseLerp(rr[i], rr[i + 1], f));
            return f > rr[0] ? 0f : 1f;
        }

        static void Play(ParticleSystem ps, bool on)
        {
            if (on && !ps.isPlaying) ps.Play(true);
            if (!on && ps.isPlaying) ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        // ------------------------------------------------------------------ builders

        ParticleSystem Particles(string name, Transform parent, Vector3 pos, Material mat, int max)
        {
            var go = new GameObject(name) { hideFlags = HideFlags.DontSave };
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main; main.maxParticles = max; main.playOnAwake = false; main.simulationSpace = ParticleSystemSimulationSpace.World;
            var r = go.GetComponent<ParticleSystemRenderer>(); r.sharedMaterial = mat; r.shadowCastingMode = ShadowCastingMode.Off; r.receiveShadows = false;
            return ps;
        }

        void EnsureVolcano()
        {
            if (volcano != null) return;
            volcano = new GameObject("Volcano") { hideFlags = HideFlags.DontSave };
            volcano.transform.SetParent(root, false);
            // in the default view: ahead of the camera, a little to the right
            var fwd = Quaternion.Euler(0, s.camYaw, 0) * Vector3.forward;
            var p = s.focus + Quaternion.Euler(0, 16f, 0) * fwd * 1750f; p.y = 0f;
            s.volcanoPos = p;
            var b = new MeshBatch();
            // jagged cone: rings with noise, crater at the top
            int sides = 28; float R0 = 560f, H = 330f, crater = 45f;
            float[] rr = { 1f, 0.75f, 0.52f, 0.32f, 0.16f, 0.09f };
            float[] hh = { -20f, 0.18f, 0.42f, 0.68f, 0.9f, 1f };
            var rng = new System.Random(3);
            var ring = new Vector3[rr.Length, sides];
            for (int k = 0; k < rr.Length; k++)
                for (int i = 0; i < sides; i++)
                {
                    float a = i * Mathf.PI * 2 / sides;
                    float j = 1f + (float)(rng.NextDouble() - 0.5) * 0.18f;
                    float rad = k == rr.Length - 1 ? crater : R0 * rr[k] * j;
                    ring[k, i] = p + new Vector3(Mathf.Sin(a) * rad, k == 0 ? hh[0] : H * hh[k] * (1f + (float)(rng.NextDouble() - 0.5) * 0.06f), Mathf.Cos(a) * rad);
                }
            for (int k = 0; k < rr.Length - 1; k++)
                for (int i = 0; i < sides; i++)
                {
                    int n = (i + 1) % sides;
                    b["volcano_rock"].Tri(ring[k, i], ring[k + 1, n], ring[k + 1, i]);
                    b["volcano_rock"].Tri(ring[k, i], ring[k, n], ring[k + 1, n]);
                }
            var top = p + Vector3.up * (H * 0.93f);
            var lb = new MeshBatch();
            for (int i = 0; i < sides; i++) lb["lava"].Tri(ring[rr.Length - 1, i], ring[rr.Length - 1, (i + 1) % sides], top);
            // lava streaks down the slope
            for (int k = 0; k < 6; k++)
            {
                float a = k * 1.1f + 0.4f;
                var d = new Vector3(Mathf.Sin(a), 0, Mathf.Cos(a));
                for (int seg = 0; seg < 8; seg++)
                {
                    float t0 = seg / 8f, t1 = (seg + 1) / 8f;
                    Vector3 P(float t) { float rad = Mathf.Lerp(crater * 1.1f, R0 * 0.55f, t); return p + d * rad + Vector3.up * (ConeH(rad / R0, rr, hh) * H + 3f) + Vector3.Cross(Vector3.up, d) * Mathf.Sin(t * 9f + k) * 12f; }
                    var side = Vector3.Cross(Vector3.up, d) * (6f - seg * 0.4f);
                    Vector3 a0 = P(t0) - side, a1 = P(t0) + side, b0 = P(t1) - side, b1 = P(t1) + side;
                    lb["lava"].Quad(a0 + d * 2f, b0 + d * 2f, b1 + d * 2f, a1 + d * 2f, Vector2.zero, Vector2.up, Vector2.one, Vector2.right);
                }
            }
            b.Flush(volcano.transform, "VolcanoMesh", s.mats, false);
            lava = new GameObject("Lava") { hideFlags = HideFlags.DontSave };
            lava.transform.SetParent(volcano.transform, false);
            lb.Flush(lava.transform, "LavaMesh", s.mats, false);
            foreach (var t in volcano.GetComponentsInChildren<Transform>()) t.gameObject.hideFlags = HideFlags.DontSave;

            var puff = Tex.Puff(5); s.mats.Own(puff);
            var smokeMat = s.mats.NewParticle(puff, false);
            plume = Particles("EruptionPlume", volcano.transform, top, smokeMat, 1500);
            var m = plume.main;
            m.startLifetime = new ParticleSystem.MinMaxCurve(22f, 32f); m.startSpeed = new ParticleSystem.MinMaxCurve(14f, 22f);
            m.startSize = new ParticleSystem.MinMaxCurve(70f, 140f); m.startColor = new ParticleSystem.MinMaxGradient(new Color(0.22f, 0.2f, 0.2f, 0.85f), new Color(0.38f, 0.34f, 0.32f, 0.8f));
            m.startRotation = new ParticleSystem.MinMaxCurve(0, 6.28f);
            var sh = plume.shape; sh.shapeType = ParticleSystemShapeType.Cone; sh.angle = 14f; sh.radius = 60f; sh.rotation = new Vector3(-90f, 0, 0);
            var em = plume.emission; em.rateOverTime = 45f;
            var sz = plume.sizeOverLifetime; sz.enabled = true; sz.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0, 0.6f, 1, 2.6f));
            var col = plume.colorOverLifetime; col.enabled = true;
            var g = new Gradient(); g.SetKeys(new[] { new GradientColorKey(new Color(1f, 0.55f, 0.3f), 0f), new GradientColorKey(Color.white, 0.12f) }, new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.08f), new GradientAlphaKey(0.8f, 0.6f), new GradientAlphaKey(0f, 1f) });
            col.color = g;
            var vel = plume.velocityOverLifetime; vel.enabled = true; vel.space = ParticleSystemSimulationSpace.World;
            vel.x = new ParticleSystem.MinMaxCurve(8f, 16f); vel.y = new ParticleSystem.MinMaxCurve(-6f, -9f); vel.z = new ParticleSystem.MinMaxCurve(2f, 6f);

            steam = Particles("Steam", volcano.transform, top, smokeMat, 200);
            m = steam.main;
            m.startLifetime = 14f; m.startSpeed = 10f; m.startSize = new ParticleSystem.MinMaxCurve(50f, 90f);
            m.startColor = new Color(0.92f, 0.92f, 0.94f, 0.45f);
            sh = steam.shape; sh.shapeType = ParticleSystemShapeType.Cone; sh.angle = 8f; sh.radius = 30f; sh.rotation = new Vector3(-90f, 0, 0);
            em = steam.emission; em.rateOverTime = 4f;
            sz = steam.sizeOverLifetime; sz.enabled = true; sz.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0, 0.5f, 1, 2f));

            volcanoGlow = new GameObject("CraterGlow") { hideFlags = HideFlags.DontSave };
            volcanoGlow.transform.SetParent(volcano.transform, false);
            volcanoGlow.transform.localPosition = top + Vector3.up * 60f;
            var l = volcanoGlow.AddComponent<Light>();
            l.type = LightType.Point; l.range = 1400f; l.intensity = 40f; l.color = new Color(1f, 0.4f, 0.12f); l.shadows = LightShadows.None;
        }

        void EnsureDust()
        {
            if (dust != null) return;
            dust = new GameObject("QuakeDust") { hideFlags = HideFlags.DontSave };
            dust.transform.SetParent(root, false);
            var puff = Tex.Puff(9); s.mats.Own(puff);
            dustPs = Particles("Dust", dust.transform, s.focus - Vector3.up * s.focus.y, s.mats.NewParticle(puff, false), 800);
            var m = dustPs.main;
            m.startLifetime = new ParticleSystem.MinMaxCurve(4f, 8f); m.startSpeed = new ParticleSystem.MinMaxCurve(1f, 4f);
            m.startSize = new ParticleSystem.MinMaxCurve(8f, 22f); m.startColor = new Color(0.62f, 0.56f, 0.48f, 0.45f);
            var sh = dustPs.shape; sh.shapeType = ParticleSystemShapeType.Box; sh.scale = new Vector3(s.extent * 1.4f, 2f, s.extent * 1.4f);
            var em = dustPs.emission; em.rateOverTime = 0f;
            var sz = dustPs.sizeOverLifetime; sz.enabled = true; sz.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0, 0.5f, 1, 1.8f));
            var col = dustPs.colorOverLifetime; col.enabled = true;
            var g = new Gradient(); g.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) }, new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(1, 0.2f), new GradientAlphaKey(0, 1) });
            col.color = g;
            dustPs.Play();
        }

        void EnsureFlood()
        {
            if (flood != null) return;
            floodMat = s.mats.NewWater(new Color(0.26f, 0.22f, 0.15f), new Color(0.42f, 0.36f, 0.24f), 0.12f, 0.15f);
            floodMat.SetFloat("_WaveLength", 7f);
            floodMat.SetColor("_SkyColor", new Color(0.45f, 0.43f, 0.38f));
            var go = new GameObject("FloodWater") { hideFlags = HideFlags.DontSave };
            go.transform.SetParent(root, false);
            float e = s.floodExtent;
            var b = new MeshBuilder();
            int n = 120; float step = e * 2 / n;
            for (int j = 0; j < n; j++) for (int i = 0; i < n; i++)
                {
                    float x0 = -e + i * step, z0 = -e + j * step;
                    b.Quad(new Vector3(x0, 0, z0), new Vector3(x0, 0, z0 + step), new Vector3(x0 + step, 0, z0 + step), new Vector3(x0 + step, 0, z0), Vector2.zero, Vector2.up, Vector2.one, Vector2.right);
                }
            var mesh = b.ToMesh("Flood"); s.mats.Own(mesh);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>(); r.sharedMaterial = floodMat; r.shadowCastingMode = ShadowCastingMode.Off;
            flood = go;
        }

        void EnsureTsunami()
        {
            if (tsunami != null) return;
            tsunamiMat = s.mats.NewWater(new Color(0.05f, 0.2f, 0.27f), new Color(0.2f, 0.45f, 0.48f), 0.6f, 0.8f);
            var go = new GameObject("TsunamiWave") { hideFlags = HideFlags.DontSave };
            go.transform.SetParent(root, false);
            var b = new MeshBuilder();
            // wave profile across its travel direction (local z), extruded along local x
            float[] pz = { -60f, -30f, -12f, -4f, 0f, 4f, 10f, 40f };
            float[] py = { 0f, 4f, 10f, 15f, 16f, 13f, 5f, 0f };
            int nx = 80; float w = 3200f;
            for (int i = 0; i < nx; i++)
                for (int k = 0; k < pz.Length - 1; k++)
                {
                    float x0 = -w / 2 + i * w / nx, x1 = x0 + w / nx;
                    b.Quad(new Vector3(x0, py[k], pz[k]), new Vector3(x0, py[k + 1], pz[k + 1]), new Vector3(x1, py[k + 1], pz[k + 1]), new Vector3(x1, py[k], pz[k]), Vector2.zero, Vector2.up, Vector2.one, Vector2.right);
                }
            var mesh = b.ToMesh("Tsunami"); s.mats.Own(mesh);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>(); r.sharedMaterial = tsunamiMat; r.shadowCastingMode = ShadowCastingMode.Off;
            // spray along the crest
            var puff = Tex.Puff(13); s.mats.Own(puff);
            var spray = Particles("Spray", go.transform, new Vector3(0, 15f, 0), s.mats.NewParticle(puff, false), 1500);
            var m = spray.main; m.simulationSpace = ParticleSystemSimulationSpace.Local;
            m.startLifetime = 2.5f; m.startSpeed = new ParticleSystem.MinMaxCurve(6f, 14f); m.startSize = new ParticleSystem.MinMaxCurve(12f, 26f);
            m.startColor = new Color(0.95f, 0.97f, 1f, 0.7f);
            var sh = spray.shape; sh.shapeType = ParticleSystemShapeType.Box; sh.scale = new Vector3(w, 1f, 6f);
            var em = spray.emission; em.rateOverTime = 400f;
            spray.Play();
            tsunami = go;
        }

        void EnsureFire()
        {
            if (fire != null) return;
            fire = new GameObject("Wildfire") { hideFlags = HideFlags.DontSave };
            fire.transform.SetParent(root, false);
            var c = FirePoint();
            s.fireCenter = c;
            var flameTex = Tex.Puff(21); s.mats.Own(flameTex);
            var flames = Particles("Flames", fire.transform, c, s.mats.NewParticle(flameTex, true), 3000);
            var m = flames.main;
            m.startLifetime = new ParticleSystem.MinMaxCurve(1.2f, 2.6f); m.startSpeed = new ParticleSystem.MinMaxCurve(3f, 9f);
            m.startSize = new ParticleSystem.MinMaxCurve(9f, 22f);
            m.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.55f, 0.12f, 0.9f), new Color(1f, 0.3f, 0.05f, 0.8f));
            var sh = flames.shape; sh.shapeType = ParticleSystemShapeType.Box; sh.scale = new Vector3(160f, 1f, 90f);
            var em = flames.emission; em.rateOverTime = 900f;
            var vel = flames.velocityOverLifetime; vel.enabled = true; vel.space = ParticleSystemSimulationSpace.World; vel.y = new ParticleSystem.MinMaxCurve(4f, 8f); vel.x = new ParticleSystem.MinMaxCurve(0f, 0f); vel.z = new ParticleSystem.MinMaxCurve(0f, 0f);
            var col = flames.colorOverLifetime; col.enabled = true;
            var g = new Gradient(); g.SetKeys(new[] { new GradientColorKey(new Color(1f, 0.9f, 0.5f), 0), new GradientColorKey(new Color(1f, 0.25f, 0.05f), 1) }, new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(1, 0.15f), new GradientAlphaKey(0, 1) });
            col.color = g;
            flames.Play();
            var smokeTex = Tex.Puff(23); s.mats.Own(smokeTex);
            var smoke = Particles("Smoke", fire.transform, c + Vector3.up * 20f, s.mats.NewParticle(smokeTex, false), 1200);
            m = smoke.main;
            m.startLifetime = new ParticleSystem.MinMaxCurve(18f, 26f); m.startSpeed = new ParticleSystem.MinMaxCurve(6f, 12f);
            m.startSize = new ParticleSystem.MinMaxCurve(50f, 110f); m.startColor = new Color(0.25f, 0.23f, 0.22f, 0.55f);
            sh = smoke.shape; sh.shapeType = ParticleSystemShapeType.Box; sh.scale = new Vector3(200f, 1f, 120f);
            em = smoke.emission; em.rateOverTime = 40f;
            var sz = smoke.sizeOverLifetime; sz.enabled = true; sz.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0, 0.5f, 1, 2.5f));
            vel = smoke.velocityOverLifetime; vel.enabled = true; vel.space = ParticleSystemSimulationSpace.World; vel.x = new ParticleSystem.MinMaxCurve(6f, 10f); vel.y = new ParticleSystem.MinMaxCurve(4f, 7f); vel.z = new ParticleSystem.MinMaxCurve(2f, 4f);
            smoke.Play();
            for (int i = 0; i < 3; i++)
            {
                var lg = new GameObject("FireLight") { hideFlags = HideFlags.DontSave };
                lg.transform.SetParent(fire.transform, false);
                lg.transform.localPosition = c + new Vector3((i - 1) * 70f, 15f, 0);
                var l = lg.AddComponent<Light>(); l.type = LightType.Point; l.range = 160f; l.intensity = 12f; l.color = new Color(1f, 0.45f, 0.15f); l.shadows = LightShadows.None;
            }
        }

        /// <summary>A visible slope ahead of the default camera: highest land point in a fan in front of the focus.</summary>
        Vector3 FirePoint()
        {
            var fwd = Quaternion.Euler(0, s.camYaw, 0) * Vector3.forward;
            Vector3 best = s.fireCenter; float bestH = float.MinValue;
            for (float d = 120f; d <= 700f; d += 40f)
                for (float a = -24f; a <= 24f; a += 6f)
                {
                    var p = s.focus + Quaternion.Euler(0, a, 0) * fwd * d; p.y = 0f;
                    float h = s.H(p.x, p.z);
                    if (s.seaDir != Vector3.zero && Vector3.Dot(new Vector3(p.x - s.focus.x, 0, p.z - s.focus.z), s.seaDir) > s.shoreDistance - 30f) continue;
                    if (h < s.seaLevel + 0.8f) continue;
                    float score = h + d * 0.05f;
                    if (score > bestH) { bestH = score; best = new Vector3(p.x, h, p.z); }
                }
            return best;
        }

        // ------------------------------------------------------------------ per frame

        public void Update(float t, float dt, Vector2 wind, float windStrength, bool instant)
        {
            // earthquake: a 3.5 s shake every ~9 s
            shakeOffset = Vector3.zero; shakeEuler = Vector3.zero;
            if (quake)
            {
                if (t - quakeStart > 9f || t < quakeStart) { quakeStart = t; if (dustPs != null) { var em = dustPs.emission; em.rateOverTime = 120f; } }
                float k = t - quakeStart;
                float amp = k < 3.5f ? Mathf.Sin(k / 3.5f * Mathf.PI) : 0f;
                if (k > 3.5f && dustPs != null) { var em = dustPs.emission; em.rateOverTime = 0f; }
                shakeOffset = new Vector3(Mathf.PerlinNoise(t * 9f, 1.3f) - 0.5f, (Mathf.PerlinNoise(t * 11f, 7.1f) - 0.5f) * 0.6f, Mathf.PerlinNoise(t * 8f, 4.7f) - 0.5f) * amp * 2.4f;
                shakeEuler = new Vector3(Mathf.PerlinNoise(t * 7f, 2.2f) - 0.5f, Mathf.PerlinNoise(t * 6f, 9.9f) - 0.5f, Mathf.PerlinNoise(t * 9f, 5.5f) - 0.5f) * amp * 1.6f;
            }
            // flood water rises to its level
            if (flood != null && flood.activeSelf)
            {
                float target = s.groundLevel + s.floodLevel + (tsunamiOn ? 1.5f : 0f);
                floodLevel = instant ? target : Mathf.MoveTowards(floodLevel, target, dt * 0.4f);
                if (floodLevel < s.groundLevel - 1.5f) floodLevel = s.groundLevel - 1.5f;
                flood.transform.localPosition = new Vector3(s.focus.x, floodLevel, s.focus.z);
            }
            // tsunami: approaches from the open sea every 60 s
            if (tsunami != null && tsunami.activeSelf)
            {
                tsunamiT = instant ? 34f : (tsunamiT + dt) % 60f;
                float dist = Mathf.Lerp(1600f, -60f, tsunamiT / 42f);
                var dir = s.seaDir;
                var pos = s.focus + dir * (s.shoreDistance + dist); pos.y = s.seaLevel;
                tsunami.transform.localPosition = pos;
                tsunami.transform.localRotation = Quaternion.LookRotation(dir);   // steep face (local -z) towards the land
                float rise = tsunamiT > 42f ? Mathf.Clamp01((tsunamiT - 42f) / 4f) * Mathf.Clamp01((60f - tsunamiT) / 6f) : 0f;
                tsunami.SetActive(true);
                if (flood != null) flood.SetActive(rise > 0.02f && !flooding);
                if (flood != null && !flooding) flood.transform.localPosition = new Vector3(s.focus.x, s.groundLevel + Mathf.Lerp(-1f, 2.6f, rise), s.focus.z);
            }
            if (floodMat != null) floodMat.SetVector("_WindDir", new Vector4(wind.x, wind.y, 0, 0));
        }

        public void Prewarm()
        {
            foreach (var ps in root.GetComponentsInChildren<ParticleSystem>())
                if (ps.gameObject.activeInHierarchy && ps.emission.rateOverTime.constant > 0f) ps.Simulate(ps.main.startLifetime.constantMax * 0.8f, true, true);
        }
    }
}
