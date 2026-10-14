using UnityEngine;
using UnityEngine.Rendering;
using WeatherScape.Effects;
using WeatherScape.World;

namespace WeatherScape
{
    /// <summary>Owns the generated scene and all effects. Show() rebuilds the scene only when the place/scene kind changes;
    /// lighting and weather blend smoothly in play mode. Everything generated is DontSave (not written into the .unity file).</summary>
    [ExecuteAlways]
    public class WeatherWorld : MonoBehaviour
    {
        public Light sun, flashLight;
        public Material skyMaterial;
        public Volume volume;
        public CameraRig rig;
        [Range(0.1f, 5f)] public float blendSpeed = 0.7f;

        public BuiltScene Scene { get; private set; }
        public Conditions Current { get; private set; }
        public Look CurrentLook => current;
        public string BuiltKey => builtKey;
        public float LastBuildMs { get; private set; }

        Transform generated;
        Mats mats;
        Atmosphere atmo;
        Precipitation precip;
        SceneAnimator anim;
        HazardFX hazards;
        AudioFX audio;
        Look current, target;
        bool hasLook;
        Season season;
        string builtKey;

        public bool Muted { get => audio != null && audio.muted; set { if (audio != null) audio.muted = value; } }

        void OnEnable()
        {
            // a domain reload loses the C# references but not the DontSave objects: clean them up
            for (int i = transform.childCount - 1; i >= 0; i--)
                if (transform.GetChild(i).name == "Generated" && transform.GetChild(i) != generated) DestroyImmediate(transform.GetChild(i).gameObject);
        }

        void OnDisable() => Teardown();

        void Teardown()
        {
            anim?.Dispose(); hazards?.Dispose(); atmo?.Dispose();
            if (generated != null) DestroyImmediate(generated.gameObject);
            mats?.Dispose();
            anim = null; hazards = null; atmo = null; precip = null; audio = null; mats = null; generated = null; Scene = null; builtKey = null; hasLook = false;
        }

        static string Key(Conditions c) => $"{c.scene}|{c.place?.Seed ?? 0}|{(c.place != null && c.place.coastal ? 1 : 0)}";

        public void Show(Conditions c, bool instant)
        {
            if (c == null) return;
            Current = c;
            string key = Key(c);
            if (key != builtKey || Scene == null)
            {
                Teardown();
                var sw = System.Diagnostics.Stopwatch.StartNew();
                generated = new GameObject("Generated") { hideFlags = HideFlags.DontSave }.transform;
                generated.SetParent(transform, false);
                int seed = c.place?.Seed ?? 1;
                mats = new Mats(seed);
                Scene = SceneBuilder.Build(c.scene, seed, c.place != null && c.place.coastal, generated, mats);
                atmo = new Atmosphere(sun, flashLight, skyMaterial, volume);
                precip = new Precipitation(generated, mats);
                anim = new SceneAnimator(Scene);
                hazards = new HazardFX(Scene);
                audio = new AudioFX(generated);
                atmo.Thunder = d => audio?.Thunder(d);
                builtKey = key;
                instant = true;
                if (rig != null) rig.SetScene(Scene, true);
                LastBuildMs = (float)sw.Elapsed.TotalMilliseconds;
            }
            season = SceneAnimator.SeasonFor(c);
            hazards.Apply(c);
            target = Look.From(c);
            if (instant || !hasLook || !Application.isPlaying) { current = target; hasLook = true; }
            ApplyLook(current);
            precip.Apply(c.weatherKind, target, hazards.AshFall);
            anim.trafficFactor = c.weatherKind == WeatherKind.Typhoon ? 0.35f : c.weatherKind == WeatherKind.HeavyRain || c.weatherKind == WeatherKind.Snow ? 0.6f : 1f;
            if (c.HasEffect(HazardKind.Flood) || c.HasEffect(HazardKind.Tsunami, Severity.Warning)) anim.trafficFactor = 0f;
        }

        void ApplyLook(Look L)
        {
            atmo.Apply(L);
            anim.ApplyMaterials(L, season);
            float amp = SeaAmp(L);
            if (Scene.seaMat != null) { Water(Scene.seaMat, L, amp); Scene.seaMat.SetFloat("_WaveLength", Mathf.Lerp(18f, 48f, L.windStrength)); }
            if (Scene.lakeMat != null) Water(Scene.lakeMat, L, 0.03f + L.windStrength * 0.16f);
        }

        float SeaAmp(Look L)
        {
            float a = 0.25f + 2.6f * Mathf.Pow(L.windStrength, 1.5f);
            var w = Current?.weather;
            if (w != null && !float.IsNaN(w.waveHeight) && Current.mode == DataMode.Live && Current.weatherKind == Current.autoWeather) a = Mathf.Max(a * 0.5f, w.waveHeight * 0.6f);
            return Mathf.Clamp(a, 0.15f, 3.2f);
        }

        static void Water(Material m, Look L, float amp)
        {
            m.SetFloat("_WaveAmp", amp);
            m.SetVector("_WindDir", new Vector4(L.wind.x, L.wind.y, 0, 0));
            m.SetFloat("_Foam", Mathf.Clamp01(0.1f + L.windStrength * 0.8f));
            m.SetFloat("_Rain", L.wet > 0.3f ? L.precip : 0f);
            m.SetColor("_SkyColor", Color.Lerp(L.horizon, L.zenith, 0.35f) * 0.9f);
        }

        void Update()
        {
            if (Scene == null || Current == null) return;
            float dt = Application.isPlaying ? Time.deltaTime : 0.016f;
            float t = Application.isPlaying ? Time.time : Time.realtimeSinceStartup;
            if (Application.isPlaying && hasLook)
            {
                current = Look.Lerp(current, target, Mathf.Clamp01(dt * blendSpeed));
                ApplyLook(current);
            }
            var camPos = rig != null ? rig.transform.position : Scene.focus;
            anim.Update(t, dt, camPos, SeaAmp(current));
            hazards.Update(t, dt, current.wind, current.windStrength, !Application.isPlaying);
            if (rig != null) { rig.shakeOffset = hazards.shakeOffset; rig.shakeEuler = hazards.shakeEuler; }
            bool storm = Current.lightning && (Current.weatherKind == WeatherKind.HeavyRain || Current.weatherKind == WeatherKind.Typhoon || Current.weatherKind == WeatherKind.Rain);
            atmo.UpdateLightning(storm && Application.isPlaying, dt, t, rig != null ? rig.Cam : null, generated);
            precip.Follow(camPos);
            audio.Update(dt, Current.weatherKind, current, Scene.seaMat != null);
        }

        /// <summary>Screenshot helper: snap to the target look and fill particle volumes.</summary>
        public void PrepareCapture()
        {
            if (Scene == null) return;
            current = target; ApplyLook(current);
            var camPos = rig != null ? rig.transform.position : Scene.focus;
            float t = Time.realtimeSinceStartup;
            anim.Update(t, 0f, camPos, SeaAmp(current));
            hazards.Update(t, 0f, current.wind, current.windStrength, true);
            precip.Follow(camPos);
            precip.Prewarm();
            hazards.Prewarm();
        }

        public string Describe()
        {
            if (Scene == null) return "no scene";
            int renderers = generated.GetComponentsInChildren<Renderer>().Length;
            return $"scene={Scene.kind} key={builtKey} build_ms={LastBuildMs:0} renderers={renderers} trees={Scene.sway.Count} cars={Scene.cars.Count} lamps={Scene.lamps.Count} " +
                   $"boats={Scene.boats.Count} materials={mats.all.Count} sun_elev={current.sunElev:0.0} night={current.night:0.00} cloud={current.cloudCover:0.00} fog={current.fogDensity:0.0000} " +
                   $"wind={current.windStrength:0.00} wet={current.wet:0.00} snow={current.snow:0.00} season={season} particles={precip.Count}";
        }
    }
}
