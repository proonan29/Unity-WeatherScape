using System;
using System.Collections.Generic;
using UnityEngine;

namespace WeatherScape
{
    /// <summary>Six kinds of place the scene builder can show.</summary>
    public enum SceneKind { Metropolis, Town, Village, Tourist, Forest, Coast }

    /// <summary>Four times of day.</summary>
    public enum DayPhase { Night, Morning, Day, Evening }

    /// <summary>Six weather looks.</summary>
    public enum WeatherKind { Clear, Cloudy, Rain, Snow, HeavyRain, Typhoon }

    /// <summary>Special conditions shown as messages (and some as effects).</summary>
    public enum HazardKind { Volcano, Earthquake, Flood, Tsunami, Wildfire, TropicalCyclone, Drought, HeatWave, ColdWave, StrongWind, HeavySnow, Fog }

    public enum Severity { Info, Advisory, Warning, Severe }

    public enum DataMode { Live, Virtual }

    [Serializable]
    public class PlaceInfo
    {
        public string query;
        public string name;           // display name in the chosen language
        public string region;         // admin / country line
        public string country;
        public double lat, lon;
        public long population;       // 0 = unknown
        public string category, type; // geocoder classes (e.g. boundary/administrative, natural/beach)
        public float importance;
        public bool coastal;          // sea within reach (marine model has waves here)
        public bool coastalKnown;
        public SceneKind scene;
        public string sceneReason;
        public bool sceneFixed;       // came from the built-in list with a fixed scene
        public float utcOffsetHours = float.NaN; // built-in/virtual places
        public string source;         // Nominatim / Open-Meteo / built-in

        public string Key => $"{lat:0.###},{lon:0.###}";
        public int Seed => Mathf.Abs(((int)(lat * 1000) * 73856093) ^ ((int)(lon * 1000) * 19349663));
    }

    [Serializable]
    public class WeatherInfo
    {
        public float temperature, apparent, humidity;
        public float precipitation, rain, snowfall;   // mm, mm, cm in the last interval (≈ per hour)
        public int weatherCode;                       // WMO
        public float cloudCover;                      // %
        public float windSpeed, windDir, windGust;    // m/s, degrees (from)
        public float visibility = 20000f;             // m
        public bool isDay;
        public int utcOffsetSeconds;
        public string localTimeText;                  // yyyy-MM-dd HH:mm
        public float localMinutes;                    // minutes since local midnight
        public float sunriseMinutes = 6 * 60, sunsetMinutes = 18 * 60;
        public float waveHeight = float.NaN;          // m (marine model), NaN = inland
        public float riverDischarge = float.NaN, riverMedian = float.NaN;
        public string source;
        public DateTime fetchedUtc;
    }

    [Serializable]
    public class Hazard
    {
        public HazardKind kind;
        public Severity severity;
        public string title;      // English key text (localized by UI)
        public string detail;     // free text (place names, numbers)
        public float distanceKm = -1f;
        public float magnitude;
        public string source;     // USGS / GDACS / Open-Meteo / built-in / virtual
        public bool isVirtual;
        public DateTime timeUtc;
    }

    /// <summary>Everything the scene needs: where, what weather, which hazards, and the resolved look.</summary>
    [Serializable]
    public class Conditions
    {
        public PlaceInfo place;
        public WeatherInfo weather;
        public List<Hazard> hazards = new List<Hazard>();
        public DataMode mode;

        // resolved (auto) values
        public SceneKind autoScene;
        public DayPhase autoPhase;
        public WeatherKind autoWeather;
        public bool thunder, fog;

        // what is shown (auto unless overridden)
        public SceneKind scene;
        public DayPhase phase;
        public WeatherKind weatherKind;
        public float hour = 12f;         // local hour used for the sun position
        public float windStrength;       // 0..1 (≈ 0 calm, 1 typhoon)
        public float windDirDeg;         // direction the wind blows TO (degrees, 0 = +Z)
        public float precipIntensity;    // 0..1

        public bool Has(HazardKind k)
        {
            foreach (var h in hazards) if (h.kind == k) return true;
            return false;
        }

        public bool HasEffect(HazardKind k, Severity min = Severity.Advisory)
        {
            foreach (var h in hazards) if (h.kind == k && (h.isVirtual || h.severity >= min)) return true;
            return false;
        }

        public Severity MaxSeverity
        {
            get
            {
                var s = Severity.Info;
                foreach (var h in hazards) if (h.severity > s) s = h.severity;
                return s;
            }
        }
    }

    /// <summary>User choices that override the automatic values (-1 = auto).</summary>
    [Serializable]
    public class Overrides
    {
        public int scene = -1, phase = -1, weather = -1;
        public List<HazardKind> virtualHazards = new List<HazardKind>();
        public bool Any => scene >= 0 || phase >= 0 || weather >= 0 || virtualHazards.Count > 0;
    }
}
