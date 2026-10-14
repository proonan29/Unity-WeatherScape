using UnityEngine;
using UnityEngine.Rendering;
using WeatherScape.World;

namespace WeatherScape.Effects
{
    /// <summary>Camera-following particle volumes: rain streaks, snowflakes, wind-blown debris (typhoon) and volcanic ash.</summary>
    public class Precipitation
    {
        public readonly Transform root;
        readonly ParticleSystem rain, snow, debris, ash;
        float rainRate, snowRate, debrisRate, ashRate;

        public Precipitation(Transform parent, Mats mats)
        {
            root = new GameObject("Precipitation") { hideFlags = HideFlags.DontSave }.transform;
            root.SetParent(parent, false);
            var streak = Tex.Streak(); mats.Own(streak);
            var dot = Tex.Dot(32, 0.35f); mats.Own(dot);
            var leaf = Tex.Dot(16, 0.8f); mats.Own(leaf);

            // particles entering a small sphere around the camera are removed (no screen-filling streaks)
            var near = root.gameObject.AddComponent<SphereCollider>(); near.isTrigger = true; near.radius = 7f;
            rain = Make("Rain", mats.NewParticle(streak, false), 30000, new Vector3(160f, 1f, 160f), 45f);
            var trig = rain.trigger; trig.enabled = true; trig.AddCollider(near); trig.inside = ParticleSystemOverlapAction.Kill; trig.enter = ParticleSystemOverlapAction.Kill;
            var main = rain.main;
            main.startLifetime = 3f; main.startSize3D = true;
            main.startSizeX = new ParticleSystem.MinMaxCurve(0.012f, 0.022f); main.startSizeY = new ParticleSystem.MinMaxCurve(0.8f, 1.4f); main.startSizeZ = 1f;
            main.startColor = new Color(0.75f, 0.8f, 0.88f, 0.42f);
            var rr = rain.GetComponent<ParticleSystemRenderer>();
            rr.renderMode = ParticleSystemRenderMode.Stretch; rr.velocityScale = 0.045f; rr.lengthScale = 1f;

            snow = Make("Snow", mats.NewParticle(dot, false), 30000, new Vector3(140f, 1f, 140f), 35f);
            main = snow.main;
            main.startLifetime = 18f; main.startSize = new ParticleSystem.MinMaxCurve(0.06f, 0.16f);
            main.startColor = new Color(1f, 1f, 1f, 0.9f);
            var noise = snow.noise; noise.enabled = true; noise.strength = 0.9f; noise.frequency = 0.35f; noise.scrollSpeed = 0.3f;

            debris = Make("Debris", mats.NewParticle(leaf, false), 3000, new Vector3(160f, 30f, 160f), 8f);
            main = debris.main;
            main.startLifetime = 6f; main.startSize = new ParticleSystem.MinMaxCurve(0.06f, 0.18f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.16f, 0.2f, 0.1f, 0.9f), new Color(0.24f, 0.18f, 0.1f, 0.9f));
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, 6.28f);
            var rot = debris.rotationOverLifetime; rot.enabled = true; rot.z = new ParticleSystem.MinMaxCurve(-6f, 6f);
            noise = debris.noise; noise.enabled = true; noise.strength = 3f; noise.frequency = 0.2f;

            ash = Make("Ash", mats.NewParticle(dot, false), 12000, new Vector3(140f, 1f, 140f), 35f);
            main = ash.main;
            main.startLifetime = 20f; main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.12f);
            main.startColor = new Color(0.32f, 0.3f, 0.29f, 0.85f);
            noise = ash.noise; noise.enabled = true; noise.strength = 0.6f; noise.frequency = 0.3f;
        }

        ParticleSystem Make(string name, Material mat, int max, Vector3 box, float height)
        {
            var go = new GameObject(name) { hideFlags = HideFlags.DontSave };
            go.transform.SetParent(root, false);
            go.transform.localPosition = Vector3.up * height;
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.maxParticles = max;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.playOnAwake = false;
            main.scalingMode = ParticleSystemScalingMode.Shape;
            main.startSpeed = 0f;
            var shape = ps.shape; shape.shapeType = ParticleSystemShapeType.Box; shape.scale = box;
            var em = ps.emission; em.rateOverTime = 0f;
            var vel = ps.velocityOverLifetime; vel.enabled = true; vel.space = ParticleSystemSimulationSpace.World;
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            r.sortingFudge = 10f;
            return ps;
        }

        static void SetVel(ParticleSystem ps, Vector3 v, float jitter)
        {
            var vel = ps.velocityOverLifetime;
            vel.x = new ParticleSystem.MinMaxCurve(v.x - jitter, v.x + jitter);
            vel.y = new ParticleSystem.MinMaxCurve(v.y - jitter * 0.5f, v.y + jitter * 0.5f);
            vel.z = new ParticleSystem.MinMaxCurve(v.z - jitter, v.z + jitter);
        }

        static void SetRate(ParticleSystem ps, float rate)
        {
            var em = ps.emission; em.rateOverTime = rate;
            if (rate > 0f && !ps.isPlaying) ps.Play(true);
            if (rate <= 0f && ps.isPlaying) ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }

        public void Apply(WeatherKind kind, Look L, bool ashFall)
        {
            Vector3 w = new Vector3(L.wind.x, 0, L.wind.y) * (L.windStrength * 26f);
            bool rainy = kind == WeatherKind.Rain || kind == WeatherKind.HeavyRain || kind == WeatherKind.Typhoon;
            rainRate = rainy ? Mathf.Lerp(3500f, 22000f, L.precip) : 0f;
            snowRate = kind == WeatherKind.Snow ? Mathf.Lerp(2500f, 9000f, L.precip) : 0f;
            debrisRate = kind == WeatherKind.Typhoon ? 380f : kind == WeatherKind.HeavyRain && L.windStrength > 0.5f ? 60f : 0f;
            ashRate = ashFall ? 2600f : 0f;
            SetVel(rain, new Vector3(w.x, -17f - L.precip * 6f, w.z), 1.2f);
            SetVel(snow, new Vector3(w.x * 0.35f, -1.4f, w.z * 0.35f), 0.5f);
            SetVel(debris, new Vector3(w.x * 1.1f, 1f, w.z * 1.1f), 4f);
            SetVel(ash, new Vector3(w.x * 0.3f, -1.0f, w.z * 0.3f), 0.4f);
            // tilt the emitter upwind so the rain still falls through the view
            rain.transform.localPosition = Vector3.up * 45f - w * 2.2f;
            snow.transform.localPosition = Vector3.up * 30f - w * 0.35f * 18f;
            ash.transform.localPosition = Vector3.up * 30f - w * 0.3f * 20f;
            debris.transform.localPosition = Vector3.up * 8f - w * 3f;
            float k = Mathf.Clamp01(0.18f + L.ambSky.grayscale * 1.1f + L.sunIntensity * 0.25f + L.night * 0.12f);
            var rm = rain.main; rm.startColor = new Color(0.78f * k, 0.82f * k, 0.9f * k, 0.32f);
            var sm = snow.main; sm.startColor = new Color(Mathf.Min(1f, k * 1.25f), Mathf.Min(1f, k * 1.25f), Mathf.Min(1f, k * 1.3f), 0.9f);
            SetRate(rain, rainRate); SetRate(snow, snowRate); SetRate(debris, debrisRate); SetRate(ash, ashRate);
        }

        public void Follow(Vector3 camPos)
        {
            root.position = camPos;
        }

        /// <summary>Edit mode / screenshots: fill the volumes immediately.</summary>
        public void Prewarm()
        {
            foreach (var ps in new[] { rain, snow, debris, ash })
            {
                var em = ps.emission;
                if (em.rateOverTime.constant > 0f) { ps.Simulate(ps == rain ? 3f : ps == debris ? 4f : 12f, true, true); }
                else ps.Clear(true);
            }
        }

        public int Count => rain.particleCount + snow.particleCount + debris.particleCount + ash.particleCount;
    }
}
