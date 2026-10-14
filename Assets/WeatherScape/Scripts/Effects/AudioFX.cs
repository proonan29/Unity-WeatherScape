using UnityEngine;

namespace WeatherScape.Effects
{
    /// <summary>Procedural ambience (no audio assets): rain hiss, wind roar, surf and thunder rumbles.</summary>
    public class AudioFX
    {
        readonly AudioSource rain, wind, surf, thunder;
        readonly AudioClip thunderClip;
        float pendingThunder = -1f;
        public bool muted;

        public AudioFX(Transform parent)
        {
            var go = new GameObject("Ambience") { hideFlags = HideFlags.DontSave };
            go.transform.SetParent(parent, false);
            rain = Loop(go, Noise("rain", 4f, 0.55f, 0.0f), 0f);
            wind = Loop(go, Noise("wind", 6f, 0.04f, 0.6f), 0f);
            surf = Loop(go, Noise("surf", 8f, 0.12f, 0.9f), 0f);
            thunder = go.AddComponent<AudioSource>();
            thunder.playOnAwake = false; thunder.spatialBlend = 0f;
            thunderClip = Rumble();
        }

        static AudioSource Loop(GameObject go, AudioClip clip, float vol)
        {
            var a = go.AddComponent<AudioSource>();
            a.clip = clip; a.loop = true; a.volume = vol; a.playOnAwake = false; a.spatialBlend = 0f;
            return a;
        }

        /// <summary>Filtered noise loop. smooth = one-pole low-pass factor (small = darker), swell = slow amplitude modulation.</summary>
        static AudioClip Noise(string name, float seconds, float smooth, float swell)
        {
            int rate = 22050, n = (int)(rate * seconds);
            var data = new float[n];
            var rng = new System.Random(name.GetHashCode());
            float y = 0f, y2 = 0f;
            for (int i = 0; i < n; i++)
            {
                float x = (float)(rng.NextDouble() * 2 - 1);
                y += smooth * (x - y);
                y2 += smooth * (y - y2);
                float env = 1f - swell * 0.5f * (1f + Mathf.Sin(i / (float)n * Mathf.PI * 2f * 2f));
                data[i] = (smooth > 0.3f ? y : y2 * 6f) * env;
            }
            // crossfade the ends so the loop is seamless
            int fade = rate / 4;
            for (int i = 0; i < fade; i++) { float t = i / (float)fade; data[i] = data[i] * t + data[n - fade + i] * (1 - t); }
            var clip = AudioClip.Create(name, n - fade, 1, rate, false);
            var trimmed = new float[n - fade]; System.Array.Copy(data, trimmed, n - fade);
            clip.SetData(trimmed, 0);
            clip.hideFlags = HideFlags.DontSave;
            return clip;
        }

        static AudioClip Rumble()
        {
            int rate = 22050, n = rate * 5;
            var data = new float[n];
            var rng = new System.Random(7);
            float y = 0, y2 = 0;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)rate;
                float env = t < 0.08f ? t / 0.08f : Mathf.Exp(-(t - 0.08f) * 0.9f) * (0.75f + 0.25f * Mathf.Sin(t * 13f));
                float x = (float)(rng.NextDouble() * 2 - 1);
                y += 0.05f * (x - y); y2 += 0.05f * (y - y2);
                data[i] = Mathf.Clamp(y2 * 14f * env, -1f, 1f);
            }
            var clip = AudioClip.Create("thunder", n, 1, rate, false);
            clip.SetData(data, 0);
            clip.hideFlags = HideFlags.DontSave;
            return clip;
        }

        public void Thunder(float delay) { if (pendingThunder <= 0f) pendingThunder = Mathf.Clamp(delay, 0.3f, 6f); }

        public void Update(float dt, WeatherKind kind, Look L, bool sea)
        {
            if (!Application.isPlaying) return;
            float master = muted ? 0f : 1f;
            bool rainy = kind == WeatherKind.Rain || kind == WeatherKind.HeavyRain || kind == WeatherKind.Typhoon;
            Set(rain, master * (rainy ? 0.15f + 0.6f * L.precip : 0f), 0.9f + L.precip * 0.2f, dt);
            Set(wind, master * Mathf.Clamp01(L.windStrength * 1.1f - 0.05f) * 0.8f, 0.7f + L.windStrength * 0.6f, dt);
            Set(surf, master * (sea ? 0.15f + 0.45f * L.windStrength : 0f), 0.8f, dt);
            if (pendingThunder > 0f)
            {
                pendingThunder -= dt;
                if (pendingThunder <= 0f && !muted) { thunder.pitch = Random.Range(0.8f, 1.15f); thunder.PlayOneShot(thunderClip, Random.Range(0.6f, 1f)); }
            }
        }

        static void Set(AudioSource a, float vol, float pitch, float dt)
        {
            a.volume = Mathf.MoveTowards(a.volume, vol, dt * 0.5f);
            a.pitch = pitch;
            if (a.volume > 0.001f && !a.isPlaying) a.Play();
            if (a.volume <= 0.001f && a.isPlaying) a.Stop();
        }
    }
}
