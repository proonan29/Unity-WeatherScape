using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace WeatherScape.Net
{
    /// <summary>Place search: built-in list → Nominatim (OpenStreetMap) → Open-Meteo geocoding.
    /// Enrich() fills population (reverse lookup of the surrounding city) and the coastal flag (marine model).</summary>
    public static class Geocoder
    {
        static readonly HashSet<string> JunkCats = new HashSet<string> { "highway", "railway", "shop", "building", "office", "craft", "public_transport", "aeroway", "emergency", "healthcare", "club" };

        static bool IsJunk(PlaceInfo p) => JunkCats.Contains(p.category ?? "") || p.category == "amenity" && p.type != "place_of_worship";

        public static async Task<List<PlaceInfo>> Search(string query, bool korean, CancellationToken ct = default)
        {
            var list = new List<PlaceInfo>();
            var builtIn = Gazetteer.Find(query);
            if (builtIn != null) list.Add(Gazetteer.ToPlace(builtIn, korean, query));

            List<PlaceInfo> found = null;
            try { found = await Nominatim(query, korean, ct).ConfigureAwait(false); }
            catch (Exception e) when (!(e is OperationCanceledException)) { UnityEngine.Debug.LogWarning($"[WeatherScape] Nominatim failed: {e.Message}"); }
            if (found == null || found.Count == 0)
            {
                try { found = await OpenMeteo(query, korean, ct).ConfigureAwait(false); }
                catch (Exception e) when (!(e is OperationCanceledException)) { UnityEngine.Debug.LogWarning($"[WeatherScape] Open-Meteo geocoding failed: {e.Message}"); }
            }
            if (found != null)
                foreach (var p in found)
                    if (builtIn == null || Geo.Km(p.lat, p.lon, builtIn.lat, builtIn.lon) > 15f) list.Add(p);
            return list.Take(6).ToList();
        }

        static async Task<List<PlaceInfo>> Nominatim(string query, bool korean, CancellationToken ct)
        {
            string url = "https://nominatim.openstreetmap.org/search?format=jsonv2&limit=10&extratags=1&addressdetails=1" +
                         $"&accept-language={(korean ? "ko,en" : "en")}&q={Http.Esc(query)}";
            var arr = await Http.GetNominatim(url, ct).ConfigureAwait(false) as List<object>;
            var res = new List<(PlaceInfo p, float score)>();
            if (arr == null) return new List<PlaceInfo>();
            foreach (var o in arr)
            {
                var p = FromNominatim(o, query);
                float score = p.importance;
                if (p.category == "place" || p.category == "boundary") score += 0.30f;
                else if (p.category == "natural" || p.category == "tourism" || p.category == "historic" || p.category == "leisure") score += 0.20f;
                if (IsJunk(p)) score -= 1f;
                if (p.population > 0) score += 0.1f;
                res.Add((p, score));
            }
            var sorted = res.OrderByDescending(r => r.score).Select(r => r.p).ToList();
            // drop junk if something better exists; dedupe places closer than 3 km with the same name
            var outList = new List<PlaceInfo>();
            bool hasGood = sorted.Any(p => !IsJunk(p));
            foreach (var p in sorted)
            {
                if (hasGood && IsJunk(p)) continue;
                if (outList.Any(q => q.name == p.name && Geo.Km(q.lat, q.lon, p.lat, p.lon) < 3f)) continue;
                outList.Add(p);
            }
            return outList;
        }

        static PlaceInfo FromNominatim(object o, string query)
        {
            var p = new PlaceInfo
            {
                query = query,
                name = Json.S(o, "name"),
                category = Json.S(o, "category") ?? Json.S(o, "class"),
                type = Json.S(o, "type"),
                lat = Json.Num(Json.Get(o, "lat"), 0), lon = Json.Num(Json.Get(o, "lon"), 0),
                importance = (float)Json.Num(Json.Get(o, "importance"), 0),
                source = "Nominatim",
            };
            string pop = Json.S(o, "extratags", "population");
            if (!string.IsNullOrEmpty(pop))
            {
                var digits = new string(pop.Where(char.IsDigit).ToArray());
                if (long.TryParse(digits, out var n)) p.population = n;
            }
            string disp = Json.S(o, "display_name") ?? "";
            if (string.IsNullOrEmpty(p.name)) p.name = disp.Split(',')[0].Trim();
            p.country = Json.S(o, "address", "country") ?? "";
            string state = Json.S(o, "address", "state") ?? Json.S(o, "address", "province") ?? Json.S(o, "address", "city") ?? "";
            p.region = string.IsNullOrEmpty(state) || state == p.name ? p.country : $"{state}, {p.country}";
            return p;
        }

        static async Task<List<PlaceInfo>> OpenMeteo(string query, bool korean, CancellationToken ct)
        {
            string url = $"https://geocoding-api.open-meteo.com/v1/search?count=10&format=json&language={(korean ? "ko" : "en")}&name={Http.Esc(query)}";
            var root = await Http.GetJson(url, ct).ConfigureAwait(false);
            var list = new List<PlaceInfo>();
            foreach (var o in Json.A(root, "results"))
            {
                var p = new PlaceInfo
                {
                    query = query,
                    name = Json.S(o, "name"),
                    lat = Json.Num(Json.Get(o, "latitude"), 0), lon = Json.Num(Json.Get(o, "longitude"), 0),
                    population = (long)Json.Num(Json.Get(o, "population"), 0),
                    category = "geonames", type = Json.S(o, "feature_code"),
                    country = Json.S(o, "country") ?? "",
                    source = "Open-Meteo",
                };
                string a1 = Json.S(o, "admin1");
                p.region = string.IsNullOrEmpty(a1) ? p.country : $"{a1}, {p.country}";
                list.Add(p);
            }
            return list.OrderByDescending(p => p.population).ToList();
        }

        /// <summary>Population from the surrounding city (reverse geocode at city zoom) and the coastal flag, then the scene.</summary>
        public static async Task Enrich(PlaceInfo p, bool korean, CancellationToken ct = default)
        {
            if (!p.coastalKnown)
            {
                try
                {
                    float wave = await WeatherService.WaveHeight(p.lat, p.lon, ct).ConfigureAwait(false);
                    p.coastal = !float.IsNaN(wave);
                    p.coastalKnown = true;
                }
                catch (Exception e) when (!(e is OperationCanceledException)) { UnityEngine.Debug.LogWarning($"[WeatherScape] marine check failed: {e.Message}"); }
            }
            if (!p.sceneFixed && p.population == 0 && p.source == "Nominatim" && NeedsPopulation(p))
            {
                try
                {
                    string url = $"https://nominatim.openstreetmap.org/reverse?format=jsonv2&zoom=10&extratags=1&accept-language={(korean ? "ko,en" : "en")}&lat={Http.D(p.lat)}&lon={Http.D(p.lon)}";
                    var o = await Http.GetNominatim(url, ct).ConfigureAwait(false);
                    var city = FromNominatim(o, p.query);
                    if (city.population > 0) p.population = city.population;
                    if (IsJunk(p)) { p.category = city.category; p.type = city.type; if (!string.IsNullOrEmpty(city.region)) p.region = $"{city.name}, {city.region}"; }
                }
                catch (Exception e) when (!(e is OperationCanceledException)) { UnityEngine.Debug.LogWarning($"[WeatherScape] reverse lookup failed: {e.Message}"); }
            }
            p.scene = Classify.Scene(p, out p.sceneReason);
        }

        static bool NeedsPopulation(PlaceInfo p)
        {
            // the class alone already decides coast / nature / sight scenes
            var probe = new PlaceInfo { name = p.name, category = p.category, type = p.type, population = 1, coastal = false };
            var k = Classify.Scene(probe, out _);
            return !(k == SceneKind.Coast || k == SceneKind.Forest || k == SceneKind.Tourist) || IsJunk(p);
        }
    }
}
