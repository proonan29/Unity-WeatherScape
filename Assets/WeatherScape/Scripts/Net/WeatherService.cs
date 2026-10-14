using System;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;

namespace WeatherScape.Net
{
    /// <summary>Open-Meteo: current weather (no API key), marine wave height, river discharge (GloFAS).</summary>
    public static class WeatherService
    {
        const string Current = "temperature_2m,relative_humidity_2m,apparent_temperature,is_day,precipitation,rain,showers,snowfall,weather_code,cloud_cover,wind_speed_10m,wind_direction_10m,wind_gusts_10m,visibility";

        public static async Task<WeatherInfo> Fetch(double lat, double lon, CancellationToken ct = default)
        {
            string url = $"https://api.open-meteo.com/v1/forecast?latitude={Http.D(lat)}&longitude={Http.D(lon)}&current={Current}" +
                         "&daily=sunrise,sunset&timezone=auto&forecast_days=1&wind_speed_unit=ms";
            var root = await Http.GetJson(url, ct).ConfigureAwait(false);
            var w = new WeatherInfo
            {
                temperature = Json.F(root, "current", "temperature_2m"),
                apparent = Json.F(root, "current", "apparent_temperature"),
                humidity = Json.F(root, "current", "relative_humidity_2m"),
                precipitation = Z(Json.F(root, "current", "precipitation")),
                rain = Z(Json.F(root, "current", "rain")) + Z(Json.F(root, "current", "showers")),
                snowfall = Z(Json.F(root, "current", "snowfall")),
                weatherCode = (int)Z(Json.F(root, "current", "weather_code")),
                cloudCover = Z(Json.F(root, "current", "cloud_cover")),
                windSpeed = Z(Json.F(root, "current", "wind_speed_10m")),
                windDir = Z(Json.F(root, "current", "wind_direction_10m")),
                windGust = Z(Json.F(root, "current", "wind_gusts_10m")),
                visibility = Json.F(root, "current", "visibility"),
                isDay = Json.F(root, "current", "is_day") > 0.5f,
                utcOffsetSeconds = (int)Z(Json.F(root, "utc_offset_seconds")),
                source = "Open-Meteo",
                fetchedUtc = DateTime.UtcNow,
            };
            if (float.IsNaN(w.visibility)) w.visibility = 20000f;
            // exact local "now" (the 'current' block is the last 15-min slot)
            var local = DateTime.UtcNow.AddSeconds(w.utcOffsetSeconds);
            w.localTimeText = local.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
            w.localMinutes = local.Hour * 60 + local.Minute;
            w.sunriseMinutes = Minutes(Json.S(root, "daily", "sunrise", 0), 6 * 60);
            w.sunsetMinutes = Minutes(Json.S(root, "daily", "sunset", 0), 18 * 60);
            return w;
        }

        static float Z(float v) => float.IsNaN(v) ? 0f : v;

        static float Minutes(string iso, float fallback)
        {
            if (string.IsNullOrEmpty(iso)) return fallback;
            if (DateTime.TryParse(iso, CultureInfo.InvariantCulture, DateTimeStyles.None, out var t)) return t.Hour * 60 + t.Minute;
            return fallback;
        }

        /// <summary>Significant wave height (m). NaN when the marine model has no sea cell here (inland).</summary>
        public static async Task<float> WaveHeight(double lat, double lon, CancellationToken ct = default)
        {
            string url = $"https://marine-api.open-meteo.com/v1/marine?latitude={Http.D(lat)}&longitude={Http.D(lon)}&current=wave_height";
            var root = await Http.GetJson(url, ct).ConfigureAwait(false);
            var v = Json.Get(root, "current", "wave_height");
            return v == null ? float.NaN : (float)Json.Num(v, double.NaN);
        }

        /// <summary>Today's river discharge vs. the long-term median (GloFAS). Returns (NaN, NaN) without a river cell.</summary>
        public static async Task<(float today, float median)> River(double lat, double lon, CancellationToken ct = default)
        {
            string url = $"https://flood-api.open-meteo.com/v1/flood?latitude={Http.D(lat)}&longitude={Http.D(lon)}&daily=river_discharge,river_discharge_median&forecast_days=1";
            var root = await Http.GetJson(url, ct).ConfigureAwait(false);
            return (Json.F(root, "daily", "river_discharge", 0), Json.F(root, "daily", "river_discharge_median", 0));
        }
    }
}
