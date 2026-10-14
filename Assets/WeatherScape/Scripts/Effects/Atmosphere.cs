using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace WeatherScape.Effects
{
    /// <summary>Applies a <see cref="Look"/>: procedural sky, sun/moon light, ambient, fog, post-processing; plus lightning.</summary>
    public class Atmosphere
    {
        readonly Light sun, flash;
        readonly Material sky;
        readonly VolumeProfile profile;
        readonly ColorAdjustments color;
        readonly Bloom bloom;
        LineRenderer bolt;
        float nextStrike = 3f, flashT = -10f, flashLevel;
        Vector3 boltAt;
        public System.Action<float> Thunder;   // delay seconds → audio

        public Atmosphere(Light sun, Light flash, Material sky, Volume volume)
        {
            this.sun = sun; this.flash = flash; this.sky = sky;
            if (volume != null)
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                profile.hideFlags = HideFlags.DontSave;
                var tone = profile.Add<Tonemapping>(true); tone.mode.Override(TonemappingMode.ACES);
                bloom = profile.Add<Bloom>(true); bloom.threshold.Override(1.0f); bloom.intensity.Override(0.4f); bloom.scatter.Override(0.65f);
                color = profile.Add<ColorAdjustments>(true); color.saturation.Override(0f); color.postExposure.Override(0f); color.colorFilter.Override(Color.white);
                var vig = profile.Add<Vignette>(true); vig.intensity.Override(0.18f);
                volume.profile = profile;
            }
            RenderSettings.skybox = sky;
            RenderSettings.sun = sun;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
        }

        public void Dispose()
        {
            if (profile != null) Object.DestroyImmediate(profile);
            if (bolt != null) Object.DestroyImmediate(bolt.gameObject);
        }

        public void Apply(Look L)
        {
            bool useSun = L.sunElev > -3f;
            if (sun != null)
            {
                Vector3 d = useSun ? L.sunDir : L.moonDir;
                // keep the light a bit above the horizon so shadows don't stretch to infinity
                d.y = Mathf.Max(d.y, 0.06f);
                sun.transform.rotation = Quaternion.LookRotation(-d.normalized);
                sun.color = useSun ? L.sunColor : L.moonColor;
                sun.intensity = useSun ? Mathf.Max(L.sunIntensity, L.moonIntensity) : L.moonIntensity;
                sun.shadowStrength = useSun ? L.shadowStrength : 0.5f * L.shadowStrength;
                sun.shadows = sun.intensity > 0.03f ? LightShadows.Soft : LightShadows.None;
            }
            if (sky != null)
            {
                sky.SetColor("_ZenithColor", L.zenith);
                sky.SetColor("_HorizonColor", L.horizon);
                sky.SetColor("_GroundColor", L.ground);
                sky.SetVector("_SunDir", L.sunDir);
                sky.SetColor("_SunColor", L.sunColor * Mathf.Clamp01((L.sunElev + 4f) / 6f));
                sky.SetVector("_MoonDir", L.moonDir);
                sky.SetFloat("_Stars", L.stars);
                sky.SetFloat("_CloudCover", L.cloudCover);
                sky.SetFloat("_CloudSpeed", L.cloudSpeed);
                sky.SetColor("_CloudColor", L.cloudLit);
                sky.SetColor("_CloudShade", L.cloudShade);
                sky.SetVector("_WindDir", new Vector4(L.wind.x, L.wind.y, 0, 0));
            }
            RenderSettings.ambientSkyColor = L.ambSky;
            RenderSettings.ambientEquatorColor = L.ambEquator;
            RenderSettings.ambientGroundColor = L.ambGround;
            RenderSettings.fogColor = L.fogColor;
            RenderSettings.fogDensity = L.fogDensity;
            // the reflection probe is baked from a daytime sky: dim it at night and under heavy cloud
            RenderSettings.reflectionIntensity = Mathf.Lerp(0.08f, 1f, 1f - L.night) * Mathf.Lerp(1f, 0.55f, L.cloudCover);
            if (color != null)
            {
                color.saturation.Override(L.saturation);
                color.postExposure.Override(L.exposure);
                color.colorFilter.Override(L.filter);
            }
            if (bloom != null) bloom.intensity.Override(L.bloom);
        }

        /// <summary>Random lightning: sky flash, a fill light and a short-lived bolt somewhere in front of the camera.</summary>
        public void UpdateLightning(bool active, float dt, float t, Camera cam, Transform parent)
        {
            if (!active) { SetFlash(0f); nextStrike = t + 2f; if (bolt != null) bolt.enabled = false; return; }
            if (t >= nextStrike)
            {
                flashT = t;
                nextStrike = t + Random.Range(2.5f, 9f);
                if (cam != null)
                {
                    var fwd = cam.transform.forward; fwd.y = 0; fwd.Normalize();
                    boltAt = cam.transform.position + Quaternion.Euler(0, Random.Range(-35f, 35f), 0) * fwd * Random.Range(500f, 1400f);
                    MakeBolt(parent, boltAt);
                    Thunder?.Invoke(Vector3.Distance(cam.transform.position, boltAt) / 343f);
                }
            }
            float k = t - flashT;
            // two quick pulses then fade
            float f = k < 0f ? 0f : k < 0.08f ? 1f : k < 0.14f ? 0.25f : k < 0.22f ? 0.85f : Mathf.Max(0f, 0.85f - (k - 0.22f) * 4f);
            SetFlash(f);
            if (bolt != null) bolt.enabled = f > 0.3f;
        }

        void SetFlash(float f)
        {
            flashLevel = f;
            if (sky != null) sky.SetFloat("_Flash", f * 0.9f);
            if (flash != null) { flash.intensity = f * 2.2f; flash.enabled = f > 0.01f; }
        }

        void MakeBolt(Transform parent, Vector3 ground)
        {
            if (bolt == null)
            {
                var go = new GameObject("LightningBolt") { hideFlags = HideFlags.DontSave };
                go.transform.SetParent(parent, false);
                bolt = go.AddComponent<LineRenderer>();
                bolt.widthMultiplier = 3.5f;
                bolt.positionCount = 14;
                var m = new Material(Shader.Find("Universal Render Pipeline/Unlit")) { hideFlags = HideFlags.DontSave };
                m.SetColor("_BaseColor", new Color(3.5f, 3.6f, 4.5f));
                bolt.sharedMaterial = m;
                bolt.shadowCastingMode = ShadowCastingMode.Off;
            }
            Vector3 top = ground + new Vector3(Random.Range(-80f, 80f), 650f, Random.Range(-80f, 80f));
            for (int i = 0; i < bolt.positionCount; i++)
            {
                float t = i / (float)(bolt.positionCount - 1);
                var p = Vector3.Lerp(top, ground, t);
                if (i > 0 && i < bolt.positionCount - 1) p += new Vector3(Random.Range(-25f, 25f), 0, Random.Range(-25f, 25f));
                bolt.SetPosition(i, p);
            }
        }
    }
}
