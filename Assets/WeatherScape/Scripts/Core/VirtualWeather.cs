using System;
using System.Globalization;
using UnityEngine;

namespace WeatherScape
{
    /// <summary>Plausible made-up weather for offline / virtual mode: climate from latitude and month, a seeded daily weather,
    /// sunrise/sunset from the solar declination, and the local clock from the place's UTC offset (or longitude).</summary>
    public static class VirtualWeather
    {
        public static WeatherInfo Make(PlaceInfo p, DateTime utcNow, WeatherKind? forced = null)
        {
            float offH = float.IsNaN(p.utcOffsetHours) ? Mathf.Round((float)p.lon / 15f) : p.utcOffsetHours;
            var local = utcNow.AddHours(offH);
            var rng = new System.Random(p.Seed ^ (local.DayOfYear * 7919) ^ (local.Hour / 3 * 104729));

            double lat = p.lat;
            int doy = local.DayOfYear;
            // seasonal factor: +1 mid-summer, -1 mid-winter (flipped in the southern hemisphere)
            float season = Mathf.Cos((doy - 196) / 365f * 2f * Mathf.PI) * Mathf.Sign((float)lat == 0 ? 1f : (float)lat);
            float absLat = Mathf.Abs((float)lat);
            float meanT = 28f - 0.42f * absLat;                // annual mean
            float amp = Mathf.Lerp(2f, 16f, Mathf.Clamp01(absLat / 55f));
            float hourAmp = 4f * Mathf.Sin((local.Hour - 9) / 24f * 2f * Mathf.PI);
            float temp = meanT + amp * season + hourAmp + (float)(rng.NextDouble() * 4 - 2);

            // daily weather draw
            WeatherKind k;
            if (forced.HasValue) k = forced.Value;
            else
            {
                double r = rng.NextDouble();
                bool tropical = absLat < 23f;
                if (r < 0.40) k = WeatherKind.Clear;
                else if (r < 0.70) k = WeatherKind.Cloudy;
                else if (r < 0.86) k = temp < 1f ? WeatherKind.Snow : WeatherKind.Rain;
                else if (r < 0.96) k = temp < 1f ? WeatherKind.Snow : WeatherKind.HeavyRain;
                else k = tropical || absLat < 35f && season > 0f ? WeatherKind.Typhoon : WeatherKind.HeavyRain;
            }
            if (k == WeatherKind.Snow && temp > 1f) temp = (float)(rng.NextDouble() * 2 - 3);

            var w = new WeatherInfo { source = "virtual", fetchedUtc = utcNow, temperature = temp };
            switch (k)
            {
                case WeatherKind.Clear: w.weatherCode = 0; w.cloudCover = 8; w.windSpeed = 2.5f; break;
                case WeatherKind.Cloudy: w.weatherCode = 3; w.cloudCover = 85; w.windSpeed = 4f; break;
                case WeatherKind.Rain: w.weatherCode = 63; w.cloudCover = 100; w.precipitation = w.rain = 2.5f; w.windSpeed = 5f; break;
                case WeatherKind.HeavyRain: w.weatherCode = 95; w.cloudCover = 100; w.precipitation = w.rain = 14f; w.windSpeed = 9f; break;
                case WeatherKind.Snow: w.weatherCode = 73; w.cloudCover = 100; w.precipitation = 1.5f; w.snowfall = 1.2f; w.windSpeed = 4f; break;
                case WeatherKind.Typhoon: w.weatherCode = 82; w.cloudCover = 100; w.precipitation = w.rain = 28f; w.windSpeed = 26f; break;
            }
            w.windSpeed *= 0.8f + (float)rng.NextDouble() * 0.4f;
            w.windGust = w.windSpeed * 1.5f;
            w.windDir = (float)(rng.NextDouble() * 360);
            w.humidity = k == WeatherKind.Clear ? 45 : k == WeatherKind.Cloudy ? 65 : 90;
            w.apparent = temp - w.windSpeed * 0.3f;
            w.visibility = k == WeatherKind.Clear ? 30000 : k == WeatherKind.Cloudy ? 15000 : k == WeatherKind.Rain ? 6000 : 1500;

            // sunrise / sunset from declination (standard approximation)
            float decl = 23.44f * Mathf.Sin(2f * Mathf.PI * (284 + doy) / 365f) * Mathf.Deg2Rad;
            float latR = (float)lat * Mathf.Deg2Rad;
            float cosH = Mathf.Clamp(-Mathf.Tan(latR) * Mathf.Tan(decl), -1f, 1f);
            float halfDayH = Mathf.Acos(cosH) * Mathf.Rad2Deg / 15f;
            float solarNoon = 12f + (offH * 15f - (float)p.lon) / 15f; // local clock of solar noon
            w.sunriseMinutes = Mathf.Clamp((solarNoon - halfDayH) * 60f, 0f, 1439f);
            w.sunsetMinutes = Mathf.Clamp((solarNoon + halfDayH) * 60f, 1f, 1440f);

            w.utcOffsetSeconds = Mathf.RoundToInt(offH * 3600f);
            w.localTimeText = local.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
            w.localMinutes = local.Hour * 60 + local.Minute;
            w.isDay = w.localMinutes >= w.sunriseMinutes && w.localMinutes < w.sunsetMinutes;
            if (p.coastal) w.waveHeight = 0.4f + w.windSpeed * 0.12f;
            return w;
        }

        /// <summary>Unknown names in virtual mode still get a stable fake location (so the same name always gives the same scene).</summary>
        public static PlaceInfo FakePlace(string name, bool korean)
        {
            int h = 17;
            foreach (char c in name ?? "") h = h * 31 + c;
            var rng = new System.Random(h);
            var p = new PlaceInfo
            {
                query = name, name = name,
                region = korean ? "가상 장소" : "Virtual place",
                lat = 20 + rng.NextDouble() * 35, lon = 100 + rng.NextDouble() * 40,
                source = "virtual",
                coastalKnown = true,
            };
            p.scene = (SceneKind)rng.Next(6);
            p.coastal = p.scene == SceneKind.Coast;
            p.sceneFixed = true;
            p.sceneReason = "virtual";
            p.utcOffsetHours = 9;
            return p;
        }
    }
}
