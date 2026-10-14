using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using WeatherScape.Net;

namespace WeatherScape
{
    /// <summary>Builds a <see cref="Conditions"/> from live or virtual data and applies the user's overrides.</summary>
    public static class Resolver
    {
        public static async Task<Conditions> Live(PlaceInfo p, bool korean, CancellationToken ct = default)
        {
            await Geocoder.Enrich(p, korean, ct).ConfigureAwait(false);
            var w = await WeatherService.Fetch(p.lat, p.lon, ct).ConfigureAwait(false);
            if (p.coastal)
            {
                try { w.waveHeight = await WeatherService.WaveHeight(p.lat, p.lon, ct).ConfigureAwait(false); } catch (Exception) { }
            }
            var hz = await HazardService.Fetch(p, w, ct).ConfigureAwait(false);
            return new Conditions { place = p, weather = w, hazards = hz, mode = DataMode.Live };
        }

        public static Conditions Virtual(PlaceInfo p, DateTime utcNow, WeatherKind? forced = null)
        {
            if (!p.coastalKnown) { p.coastal = p.scene == SceneKind.Coast; p.coastalKnown = true; }
            if (!p.sceneFixed) p.scene = Classify.Scene(p, out p.sceneReason);
            var w = VirtualWeather.Make(p, utcNow, forced);
            var hz = new List<Hazard>();
            HazardService.FromWeather(p, w, hz);
            HazardService.NearVolcano(p, hz);
            return new Conditions { place = p, weather = w, hazards = hz, mode = DataMode.Virtual };
        }

        static readonly float[] KindWind = { 0.10f, 0.22f, 0.30f, 0.20f, 0.50f, 1.00f };

        /// <summary>Fills the automatic values, then the shown values (override or auto), wind and precipitation, and virtual hazards.</summary>
        public static void Resolve(Conditions c, Overrides o)
        {
            var w = c.weather;
            c.autoScene = c.place != null ? c.place.scene : SceneKind.Town;
            c.autoWeather = w != null ? Classify.Weather(w, out c.thunder, out c.fog) : WeatherKind.Clear;
            c.autoPhase = w != null ? Classify.Phase(w.localMinutes, w.sunriseMinutes, w.sunsetMinutes) : DayPhase.Day;

            c.scene = o != null && o.scene >= 0 ? (SceneKind)o.scene : c.autoScene;
            c.phase = o != null && o.phase >= 0 ? (DayPhase)o.phase : c.autoPhase;
            c.weatherKind = o != null && o.weather >= 0 ? (WeatherKind)o.weather : c.autoWeather;

            float rise = w?.sunriseMinutes ?? 360f, set = w?.sunsetMinutes ?? 1080f;
            c.hour = o != null && o.phase >= 0 || w == null ? Classify.HourFor(c.phase, rise, set) : w.localMinutes / 60f;

            bool weatherOverridden = o != null && o.weather >= 0;
            if (weatherOverridden)
            {
                c.lightning = c.weatherKind == WeatherKind.HeavyRain || c.weatherKind == WeatherKind.Typhoon;
                c.windStrength = KindWind[(int)c.weatherKind];
                c.precipIntensity = Classify.PrecipIntensity(c.weatherKind, null);
            }
            else
            {
                c.lightning = c.thunder;
                c.windStrength = Mathf.Max(w != null ? Classify.WindStrength(w) : 0.1f, c.weatherKind == WeatherKind.Typhoon ? 0.85f : 0f);
                c.precipIntensity = Classify.PrecipIntensity(c.weatherKind, w);
            }
            c.windDirDeg = w != null ? (w.windDir + 180f) % 360f : 60f;
            if (c.weatherKind == WeatherKind.Typhoon) c.lightning = true;

            c.hazards.RemoveAll(h => h.isVirtual);
            if (o != null) foreach (var k in o.virtualHazards) c.hazards.Insert(0, HazardService.Virtual(k));
        }
    }
}
