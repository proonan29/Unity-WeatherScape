using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using UnityEngine;

namespace WeatherScape.Net
{
    /// <summary>Special conditions near a place: USGS earthquakes (+ tsunami flag), GDACS alerts (cyclone, flood, volcano, wildfire, drought),
    /// GloFAS river level, weather-derived warnings (heat, cold, wind, heavy rain/snow, fog) and the built-in active-volcano list.</summary>
    public static class HazardService
    {
        static string gdacsXml;
        static DateTime gdacsAt;

        public static async Task<List<Hazard>> Fetch(PlaceInfo p, WeatherInfo w, CancellationToken ct = default)
        {
            var list = new List<Hazard>();
            var tasks = new List<Task>
            {
                Guard("USGS", () => Usgs(p, list, ct)),
                Guard("GDACS", () => Gdacs(p, list, ct)),
                Guard("GloFAS", () => River(p, w, list, ct)),
            };
            await Task.WhenAll(tasks).ConfigureAwait(false);
            lock (list) { FromWeather(p, w, list); NearVolcano(p, list); }
            list.Sort((a, b) => b.severity != a.severity ? b.severity.CompareTo(a.severity) : a.distanceKm.CompareTo(b.distanceKm));
            return list;
        }

        static async Task Guard(string name, Func<Task> f)
        {
            try { await f().ConfigureAwait(false); }
            catch (Exception e) when (!(e is OperationCanceledException)) { Debug.LogWarning($"[WeatherScape] {name} failed: {e.Message}"); }
        }

        static void Add(List<Hazard> list, Hazard h) { lock (list) list.Add(h); }

        // ---------------------------------------------------------------- USGS

        static async Task Usgs(PlaceInfo p, List<Hazard> list, CancellationToken ct)
        {
            string start = DateTime.UtcNow.AddHours(-48).ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture);
            string url = $"https://earthquake.usgs.gov/fdsnws/event/1/query?format=geojson&latitude={Http.D(p.lat)}&longitude={Http.D(p.lon)}" +
                         $"&maxradiuskm=1500&starttime={start}&minmagnitude=4&orderby=magnitude&limit=40";
            var root = await Http.GetJson(url, ct).ConfigureAwait(false);
            int shown = 0;
            foreach (var f in Json.A(root, "features"))
            {
                float mag = Json.F(f, "properties", "mag");
                double lon = Json.Num(Json.Get(f, "geometry", "coordinates", 0), 0), lat = Json.Num(Json.Get(f, "geometry", "coordinates", 1), 0);
                float km = Geo.Km(p.lat, p.lon, lat, lon);
                var t = DateTimeOffset.FromUnixTimeMilliseconds((long)Json.Num(Json.Get(f, "properties", "time"), 0)).UtcDateTime;
                bool tsunami = Json.Num(Json.Get(f, "properties", "tsunami"), 0) > 0.5;
                string where = Json.S(f, "properties", "place") ?? "";
                if (tsunami && km <= 1500f && (DateTime.UtcNow - t).TotalHours < 24 && (p.coastal || km < 300f))
                    Add(list, new Hazard { kind = HazardKind.Tsunami, severity = Severity.Warning, title = "Tsunami possible – check official warnings", detail = $"M{mag:0.0} {where}", distanceKm = km, magnitude = mag, source = "USGS", timeUtc = t });
                bool near = mag >= 4f && km <= 300f || mag >= 6f && km <= 1000f;
                if (!near || shown >= 3) continue;
                shown++;
                Severity s = mag >= 6f && km <= 300f ? Severity.Severe : mag >= 5f && km <= 300f ? Severity.Warning : km <= 150f || mag >= 5.5f ? Severity.Advisory : Severity.Info;
                Add(list, new Hazard { kind = HazardKind.Earthquake, severity = s, title = "Earthquake", detail = where, distanceKm = km, magnitude = mag, source = "USGS", timeUtc = t });
            }
        }

        // ---------------------------------------------------------------- GDACS (RSS feed, cached 15 min)

        static async Task Gdacs(PlaceInfo p, List<Hazard> list, CancellationToken ct)
        {
            if (gdacsXml == null || (DateTime.UtcNow - gdacsAt).TotalMinutes > 15)
            {
                gdacsXml = await Http.Get("https://www.gdacs.org/xml/rss.xml", ct).ConfigureAwait(false);
                gdacsAt = DateTime.UtcNow;
            }
            var doc = new XmlDocument();
            doc.LoadXml(gdacsXml.TrimStart('﻿'));
            var ns = new XmlNamespaceManager(doc.NameTable);
            ns.AddNamespace("gdacs", "http://www.gdacs.org");
            ns.AddNamespace("geo", "http://www.w3.org/2003/01/geo/wgs84_pos#");
            foreach (XmlNode item in doc.SelectNodes("//item"))
            {
                string type = item.SelectSingleNode("gdacs:eventtype", ns)?.InnerText;
                if (item.SelectSingleNode("gdacs:iscurrent", ns)?.InnerText != "true") continue;
                double lat = Num(item.SelectSingleNode("geo:Point/geo:lat", ns)?.InnerText), lon = Num(item.SelectSingleNode("geo:Point/geo:long", ns)?.InnerText);
                if (double.IsNaN(lat)) continue;
                float km = Geo.Km(p.lat, p.lon, lat, lon);
                string level = item.SelectSingleNode("gdacs:alertlevel", ns)?.InnerText ?? "Green";
                Severity s = level == "Red" ? Severity.Severe : level == "Orange" ? Severity.Warning : Severity.Advisory;
                string name = item.SelectSingleNode("gdacs:eventname", ns)?.InnerText;
                string country = item.SelectSingleNode("gdacs:country", ns)?.InnerText;
                string sev = item.SelectSingleNode("gdacs:severity", ns)?.InnerText;
                string detail = string.Join(" · ", new[] { name, country, sev }).Replace(" ·  · ", " · ").Trim(' ', '·');
                var h = new Hazard { severity = s, detail = detail, distanceKm = km, source = "GDACS", timeUtc = DateTime.UtcNow };
                switch (type)
                {
                    case "TC": if (km > 1000f) continue; h.kind = HazardKind.TropicalCyclone; h.title = "Tropical cyclone"; if (km > 500f && s == Severity.Advisory) h.severity = Severity.Info; break;
                    case "FL": if (km > 300f) continue; h.kind = HazardKind.Flood; h.title = "Flood alert"; break;
                    case "VO": if (km > 300f) continue; h.kind = HazardKind.Volcano; h.title = "Volcanic activity"; break;
                    case "WF": if (km > 150f) continue; h.kind = HazardKind.Wildfire; h.title = "Wildfire"; break;
                    case "DR": if (km > 600f) continue; h.kind = HazardKind.Drought; h.title = "Drought"; h.severity = Severity.Info; break;
                    case "EQ": if (km > 500f || level == "Green") continue; h.kind = HazardKind.Earthquake; h.title = "Earthquake (GDACS alert)"; break;
                    default: continue;
                }
                Add(list, h);
            }
        }

        static double Num(string s) => double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var d) ? d : double.NaN;

        // ---------------------------------------------------------------- river + weather + volcano list

        static async Task River(PlaceInfo p, WeatherInfo w, List<Hazard> list, CancellationToken ct)
        {
            var (today, median) = await WeatherService.River(p.lat, p.lon, ct).ConfigureAwait(false);
            if (w != null) { w.riverDischarge = today; w.riverMedian = median; }
            if (float.IsNaN(today) || float.IsNaN(median) || median < 20f) return;
            float ratio = today / median;
            if (ratio >= 2f)
                Add(list, new Hazard { kind = HazardKind.Flood, severity = ratio >= 4f ? Severity.Warning : Severity.Advisory, title = "River level high", detail = $"{today:0} m³/s (×{ratio:0.0} median)", distanceKm = 0, source = "GloFAS", timeUtc = DateTime.UtcNow });
        }

        public static void FromWeather(PlaceInfo p, WeatherInfo w, List<Hazard> list)
        {
            if (w == null) return;
            string src = w.source ?? "Open-Meteo";
            void A(HazardKind k, Severity s, string title, string detail) => list.Add(new Hazard { kind = k, severity = s, title = title, detail = detail, distanceKm = 0, source = src, timeUtc = DateTime.UtcNow });
            float feel = float.IsNaN(w.apparent) ? w.temperature : w.apparent;
            if (feel >= 33f) A(HazardKind.HeatWave, feel >= 35f ? Severity.Warning : Severity.Advisory, "Heat wave", $"{feel:0.#} °C");
            if (w.temperature <= -12f) A(HazardKind.ColdWave, w.temperature <= -15f ? Severity.Warning : Severity.Advisory, "Cold wave", $"{w.temperature:0.#} °C");
            float gust = Mathf.Max(w.windGust, w.windSpeed);
            if (gust >= 14f) A(HazardKind.StrongWind, gust >= 25f ? Severity.Severe : gust >= 21f ? Severity.Warning : Severity.Advisory, "Strong wind", $"{w.windSpeed:0.#} m/s, gust {w.windGust:0.#} m/s");
            if (w.precipitation >= 10f) A(HazardKind.Flood, w.precipitation >= 20f ? Severity.Warning : Severity.Advisory, "Heavy rain – flood risk", $"{w.precipitation:0.#} mm/h");
            if (w.snowfall >= 1f) A(HazardKind.HeavySnow, w.snowfall >= 3f ? Severity.Warning : Severity.Advisory, "Heavy snow", $"{w.snowfall:0.#} cm/h");
            if (w.visibility < 1000f) A(HazardKind.Fog, w.visibility < 200f ? Severity.Warning : Severity.Advisory, "Dense fog", $"{w.visibility:0} m");
        }

        public static void NearVolcano(PlaceInfo p, List<Hazard> list)
        {
            if (!Volcanoes.Nearest(p.lat, p.lon, out var v, out float km) || km > 60f) return;
            foreach (var h in list) if (h.kind == HazardKind.Volcano) return; // a live alert already covers it
            list.Add(new Hazard { kind = HazardKind.Volcano, severity = Severity.Info, title = "Active volcano nearby", detail = $"{v.ko}|{v.en}", distanceKm = km, source = "built-in", timeUtc = DateTime.UtcNow });
        }

        // ---------------------------------------------------------------- virtual scenarios

        public static Hazard Virtual(HazardKind k)
        {
            var h = new Hazard { kind = k, severity = Severity.Warning, isVirtual = true, source = "virtual", distanceKm = 0, timeUtc = DateTime.UtcNow };
            switch (k)
            {
                case HazardKind.Volcano: h.title = "Volcanic eruption"; h.severity = Severity.Severe; break;
                case HazardKind.Earthquake: h.title = "Earthquake"; h.magnitude = 6.4f; h.distanceKm = 18f; h.severity = Severity.Severe; break;
                case HazardKind.Flood: h.title = "Flood"; break;
                case HazardKind.Tsunami: h.title = "Tsunami"; h.severity = Severity.Severe; break;
                case HazardKind.Wildfire: h.title = "Wildfire"; break;
                default: h.title = k.ToString(); break;
            }
            return h;
        }

        public static readonly HazardKind[] VirtualKinds = { HazardKind.Volcano, HazardKind.Earthquake, HazardKind.Flood, HazardKind.Tsunami, HazardKind.Wildfire };
    }
}
