using UnityEngine;

namespace WeatherScape
{
    /// <summary>Rules that turn raw data into the six scenes, four times of day and six weather looks.</summary>
    public static class Classify
    {
        // ------------------------------------------------------------ weather

        /// <summary>WMO weather code + measurements → weather look. Typhoon = tropical-storm-force wind (≥ 17.2 m/s mean or ≥ 25 m/s gusts) with rain.</summary>
        public static WeatherKind Weather(WeatherInfo w, out bool thunder, out bool fog)
        {
            int c = w.weatherCode;
            thunder = c >= 95;
            fog = c == 45 || c == 48 || w.visibility < 1000f;
            bool snow = (c >= 71 && c <= 77) || c == 85 || c == 86 || w.snowfall > 0.05f;
            bool rain = (c >= 51 && c <= 67) || (c >= 80 && c <= 82) || c >= 95 || w.rain > 0.05f;
            bool stormWind = w.windSpeed >= 17.2f || w.windGust >= 25f;
            if (stormWind && (rain || w.precipitation > 0.5f)) return WeatherKind.Typhoon;
            if (snow && !(rain && w.temperature > 1.5f)) return WeatherKind.Snow;
            if (rain)
            {
                bool heavy = c == 65 || c == 67 || c == 82 || c >= 95 || w.precipitation >= 7.6f;
                return heavy ? WeatherKind.HeavyRain : WeatherKind.Rain;
            }
            if (c >= 2 || w.cloudCover >= 60f || fog) return WeatherKind.Cloudy;
            return WeatherKind.Clear;
        }

        public static float WindStrength(WeatherInfo w) => Mathf.Clamp01(Mathf.Max(w.windSpeed / 25f, w.windGust / 38f));

        public static float PrecipIntensity(WeatherKind k, WeatherInfo w)
        {
            float p = w != null ? Mathf.Clamp01(w.precipitation / 15f) : 0f;
            switch (k)
            {
                case WeatherKind.Rain: return Mathf.Clamp(0.25f + p, 0.25f, 0.55f);
                case WeatherKind.HeavyRain: return Mathf.Clamp(0.65f + p, 0.65f, 1f);
                case WeatherKind.Typhoon: return 1f;
                case WeatherKind.Snow: return Mathf.Clamp(0.35f + (w != null ? w.snowfall : 0f) * 0.3f, 0.35f, 1f);
                default: return 0f;
            }
        }

        // ------------------------------------------------------------ time of day

        /// <summary>Morning = from 30 min before sunrise to 3 h after; Evening = 1.5 h before to 45 min after sunset; Night/Day otherwise.</summary>
        public static DayPhase Phase(float localMin, float sunrise, float sunset)
        {
            if (sunset <= sunrise) { sunrise = 6 * 60; sunset = 18 * 60; } // polar day/night fallback
            if (localMin < sunrise - 30f || localMin >= sunset + 45f) return DayPhase.Night;
            if (localMin < sunrise + 180f) return DayPhase.Morning;
            if (localMin < sunset - 90f) return DayPhase.Day;
            return DayPhase.Evening;
        }

        /// <summary>Representative local hour for a phase (used when the phase is overridden).</summary>
        public static float HourFor(DayPhase p, float sunrise, float sunset)
        {
            switch (p)
            {
                case DayPhase.Morning: return (sunrise + 50f) / 60f;
                case DayPhase.Day: return 13f;
                case DayPhase.Evening: return (sunset - 45f) / 60f;
                default: return 23f;
            }
        }

        // ------------------------------------------------------------ place → scene

        static readonly string[] CoastTypes = { "beach", "coastline", "bay", "sea", "harbour", "marina", "pier", "lighthouse", "cape", "island", "islet", "port", "strait", "ocean", "beach_resort", "breakwater", "ferry_terminal" };
        static readonly string[] ForestTypes = { "national_park", "protected_area", "nature_reserve", "peak", "volcano", "wood", "forest", "mountain_range", "ridge", "valley", "waterfall", "glacier", "massif", "mountain", "MT", "MTS", "PRK", "FRST", "RESN", "RESF", "PK", "VAL", "GLCR", "CNYN" };
        static readonly string[] TouristCats = { "tourism", "historic" };
        static readonly string[] TouristTypes = { "attraction", "museum", "theme_park", "viewpoint", "zoo", "castle", "palace", "monument", "memorial", "ruins", "archaeological_site", "temple", "shrine", "tower", "gallery", "aquarium", "RSRT", "CSTL", "MUS", "TMPL", "HSTS", "MNMT", "PAL", "AMUS", "ZOO", "RUIN", "TOWR", "SHRN", "PGDA" };
        static readonly string[] VillageTypes = { "village", "hamlet", "isolated_dwelling", "farm", "farmland", "allotments", "orchard", "residential", "locality", "PPLL", "PPLF" };
        static readonly string[] CoastNames = { "해변", "해수욕장", "항구", "포구", "beach", "harbor", "harbour", "port ", "bay", "만 ", "섬", "island" };
        static readonly string[] ForestNames = { "국립공원", "national park", "숲", "forest", "산", "mount", "mt.", "봉" };
        static readonly string[] TouristNames = { "궁", "사찰", "타워", "tower", "castle", "palace", "temple", "museum", "박물관", "유적", "랜드", "world", "park" };

        static bool In(string[] list, string v)
        {
            if (string.IsNullOrEmpty(v)) return false;
            foreach (var x in list) if (string.Equals(x, v, System.StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        static bool NameHas(string[] keys, string name)
        {
            if (string.IsNullOrEmpty(name)) return false;
            string n = name.ToLowerInvariant();
            foreach (var k in keys) if (n.Contains(k.ToLowerInvariant())) return true;
            return false;
        }

        /// <summary>Picks one of the six scenes from the geocoder class, population and the coastal flag.
        /// Big cities stay Metropolis even on the coast (the city scene then gets a waterfront).</summary>
        public static SceneKind Scene(PlaceInfo p, out string reason)
        {
            if (p.sceneFixed) { reason = "built-in list"; return p.scene; }
            string cat = p.category ?? "", type = p.type ?? "";

            if (In(CoastTypes, type) || cat == "natural" && type == "water" && NameHas(CoastNames, p.name)) { reason = $"{cat}/{type}"; return SceneKind.Coast; }
            if (cat == "boundary" && (type == "national_park" || type == "protected_area") || In(ForestTypes, type) || cat == "leisure" && type == "nature_reserve")
            { reason = $"{cat}/{type}"; return SceneKind.Forest; }
            if (In(TouristCats, cat) || In(TouristTypes, type) || cat == "leisure" && (type == "theme_park" || type == "water_park") || cat == "amenity" && type == "place_of_worship" || cat == "man_made" && type == "tower")
            { reason = $"{cat}/{type}"; return SceneKind.Tourist; }

            long pop = p.population;
            if (pop >= 1_000_000) { reason = $"population {pop:N0} ≥ 1,000,000"; return SceneKind.Metropolis; }
            if (pop >= 50_000)
            {
                if (p.coastal) { reason = $"coastal town (population {pop:N0})"; return SceneKind.Coast; }
                reason = $"population {pop:N0} ≥ 50,000"; return SceneKind.Town;
            }
            if (pop > 0)
            {
                if (p.coastal) { reason = $"coastal village (population {pop:N0})"; return SceneKind.Coast; }
                reason = $"population {pop:N0} < 50,000"; return SceneKind.Village;
            }

            // population unknown: fall back on names and place classes
            if (NameHas(CoastNames, p.name)) { reason = "name (coast)"; return SceneKind.Coast; }
            if (NameHas(ForestNames, p.name) && !(cat == "boundary" || cat == "place")) { reason = "name (nature)"; return SceneKind.Forest; }
            if (NameHas(TouristNames, p.name) && !(cat == "boundary" || cat == "place")) { reason = "name (sight)"; return SceneKind.Tourist; }
            if (In(VillageTypes, type) || type == "PPL") { reason = $"{cat}/{type}" + (p.coastal ? " + coast" : ""); return p.coastal ? SceneKind.Coast : SceneKind.Village; }
            if (type == "city" || type == "PPLC" || type == "PPLA") { reason = $"{cat}/{type}"; return p.importance > 0.6f ? SceneKind.Metropolis : (p.coastal ? SceneKind.Coast : SceneKind.Town); }
            if (p.coastal) { reason = "coastal (marine model)"; return SceneKind.Coast; }
            reason = $"{cat}/{type} (default)";
            return SceneKind.Town;
        }
    }
}
