using UnityEngine;

namespace WeatherScape.Effects
{
    /// <summary>All continuous lighting/atmosphere values. Computed from the conditions and blended over time.</summary>
    public struct Look
    {
        public Vector3 sunDir, moonDir;          // towards the sun / moon
        public float sunElev;                    // degrees
        public Color zenith, horizon, ground;
        public Color sunColor; public float sunIntensity, shadowStrength;
        public Color moonColor; public float moonIntensity;
        public Color ambSky, ambEquator, ambGround;
        public Color fogColor; public float fogDensity;
        public float cloudCover, cloudSpeed, stars;
        public Color cloudLit, cloudShade;
        public float night;                      // 0 day … 1 full night (drives window/lamp emission)
        public float saturation, exposure, bloom;
        public Color filter;
        public float wet, snow;                  // surface wetness / snow cover
        public float windStrength; public Vector2 wind;   // wind blows towards (x,z)
        public float precip;

        public static Look Lerp(Look a, Look b, float t)
        {
            Look o;
            o.sunDir = Vector3.Slerp(a.sunDir, b.sunDir, t).normalized;
            o.moonDir = Vector3.Slerp(a.moonDir, b.moonDir, t).normalized;
            o.sunElev = Mathf.Lerp(a.sunElev, b.sunElev, t);
            o.zenith = Color.Lerp(a.zenith, b.zenith, t); o.horizon = Color.Lerp(a.horizon, b.horizon, t); o.ground = Color.Lerp(a.ground, b.ground, t);
            o.sunColor = Color.Lerp(a.sunColor, b.sunColor, t); o.sunIntensity = Mathf.Lerp(a.sunIntensity, b.sunIntensity, t); o.shadowStrength = Mathf.Lerp(a.shadowStrength, b.shadowStrength, t);
            o.moonColor = Color.Lerp(a.moonColor, b.moonColor, t); o.moonIntensity = Mathf.Lerp(a.moonIntensity, b.moonIntensity, t);
            o.ambSky = Color.Lerp(a.ambSky, b.ambSky, t); o.ambEquator = Color.Lerp(a.ambEquator, b.ambEquator, t); o.ambGround = Color.Lerp(a.ambGround, b.ambGround, t);
            o.fogColor = Color.Lerp(a.fogColor, b.fogColor, t); o.fogDensity = Mathf.Lerp(a.fogDensity, b.fogDensity, t);
            o.cloudCover = Mathf.Lerp(a.cloudCover, b.cloudCover, t); o.cloudSpeed = Mathf.Lerp(a.cloudSpeed, b.cloudSpeed, t); o.stars = Mathf.Lerp(a.stars, b.stars, t);
            o.cloudLit = Color.Lerp(a.cloudLit, b.cloudLit, t); o.cloudShade = Color.Lerp(a.cloudShade, b.cloudShade, t);
            o.night = Mathf.Lerp(a.night, b.night, t);
            o.saturation = Mathf.Lerp(a.saturation, b.saturation, t); o.exposure = Mathf.Lerp(a.exposure, b.exposure, t); o.bloom = Mathf.Lerp(a.bloom, b.bloom, t);
            o.filter = Color.Lerp(a.filter, b.filter, t);
            o.wet = Mathf.Lerp(a.wet, b.wet, t); o.snow = Mathf.Lerp(a.snow, b.snow, t);
            o.windStrength = Mathf.Lerp(a.windStrength, b.windStrength, t); o.wind = Vector2.Lerp(a.wind, b.wind, t);
            o.precip = Mathf.Lerp(a.precip, b.precip, t);
            return o;
        }

        static Color Gray(float v) => new Color(v, v, v);

        /// <summary>Sun path from the local hour, sunrise/sunset and latitude; sky palette from the sun height; then weather on top.</summary>
        public static Look From(Conditions c)
        {
            var w = c.weather;
            float rise = w?.sunriseMinutes ?? 360f, set = w?.sunsetMinutes ?? 1080f;
            if (set <= rise) { rise = 360f; set = 1080f; }
            float minutes = c.hour * 60f;
            double lat = c.place?.lat ?? 37.5;
            int doy = System.DateTime.UtcNow.DayOfYear;
            float decl = 23.44f * Mathf.Sin(2f * Mathf.PI * (284 + doy) / 365f);
            float maxElev = Mathf.Clamp(90f - Mathf.Abs((float)lat - decl), 12f, 82f);
            bool south = lat < 0;

            Look L = default;
            // sun: above the horizon between sunrise and sunset (sin arc), continuing below at night
            float dayLen = set - rise;
            float frac = (minutes - rise) / dayLen;                 // 0 sunrise … 1 sunset
            float elev;
            if (frac >= 0f && frac <= 1f) elev = Mathf.Sin(frac * Mathf.PI) * maxElev;
            else
            {
                float nightLen = 1440f - dayLen;
                float nf = ((minutes - set + 1440f) % 1440f) / nightLen;  // 0 sunset … 1 sunrise
                elev = -Mathf.Sin(Mathf.Clamp01(nf) * Mathf.PI) * (90f - maxElev * 0.5f) * 0.6f;
                frac = 1f + nf;                                      // keeps moving westwards → north
            }
            float az = south ? 90f - frac * 180f : 90f + frac * 180f; // east → south → west (north hemisphere)
            L.sunElev = elev;
            L.sunDir = Dir(az, elev);
            L.moonDir = Dir(az + 180f, 35f);

            float e = elev;
            float day = Mathf.Clamp01((e + 2f) / 14f);             // 0 below -2°, 1 above 12°
            float twilight = Mathf.Clamp01(1f - Mathf.Abs(e - 2f) / 12f) * Mathf.Clamp01((e + 12f) / 8f);
            float nightK = Mathf.Clamp01((-e - 1f) / 10f);
            L.night = Mathf.Clamp01((6f - e) / 14f);

            Color zDay = new Color(0.16f, 0.36f, 0.74f), hDay = new Color(0.62f, 0.76f, 0.92f);
            Color zNight = new Color(0.02f, 0.035f, 0.085f), hNight = new Color(0.06f, 0.08f, 0.14f);
            Color hGold = new Color(1.0f, 0.55f, 0.28f), zGold = new Color(0.22f, 0.25f, 0.45f);
            L.zenith = Color.Lerp(Color.Lerp(zNight, zDay, day), zGold, twilight * 0.6f);
            L.horizon = Color.Lerp(Color.Lerp(hNight, hDay, day), hGold, twilight * 0.85f);
            L.ground = L.horizon * 0.45f;
            L.sunColor = Color.Lerp(new Color(1f, 0.5f, 0.25f), new Color(1f, 0.96f, 0.9f), Mathf.Clamp01((e - 2f) / 25f));
            L.sunIntensity = Mathf.Clamp01((e + 1f) / 10f) * Mathf.Lerp(0.8f, 1.45f, Mathf.Clamp01(e / 40f));
            L.shadowStrength = 0.85f;
            L.moonColor = new Color(0.62f, 0.72f, 1f);
            L.moonIntensity = nightK * 0.55f;
            L.stars = nightK;
            L.ambSky = Color.Lerp(new Color(0.15f, 0.19f, 0.31f), new Color(0.58f, 0.68f, 0.85f), day) + hGold * twilight * 0.15f;
            L.ambEquator = Color.Lerp(new Color(0.10f, 0.12f, 0.18f), new Color(0.50f, 0.54f, 0.56f), day) + hGold * twilight * 0.2f;
            L.ambGround = Color.Lerp(new Color(0.04f, 0.045f, 0.06f), new Color(0.32f, 0.31f, 0.27f), day);
            L.cloudLit = Color.Lerp(new Color(0.10f, 0.11f, 0.15f), Color.white, day) + hGold * twilight * 0.6f;
            L.cloudShade = Color.Lerp(new Color(0.05f, 0.06f, 0.09f), new Color(0.6f, 0.65f, 0.72f), day);
            L.saturation = 8f; L.exposure = nightK * 0.6f; L.bloom = Mathf.Lerp(0.35f, 1.1f, L.night);
            L.filter = Color.white;

            // ---------------- weather on top
            float oc, fog, sat, cloudSpeed;
            switch (c.weatherKind)
            {
                case WeatherKind.Clear: oc = 0.12f; fog = 0.00042f; sat = 0f; cloudSpeed = 0.008f; break;
                case WeatherKind.Cloudy: oc = 0.78f; fog = 0.0006f; sat = -12f; cloudSpeed = 0.012f; break;
                case WeatherKind.Rain: oc = 0.95f; fog = 0.00095f; sat = -22f; cloudSpeed = 0.018f; break;
                case WeatherKind.Snow: oc = 0.95f; fog = 0.0012f; sat = -18f; cloudSpeed = 0.012f; break;
                case WeatherKind.HeavyRain: oc = 1f; fog = 0.0015f; sat = -30f; cloudSpeed = 0.03f; break;
                default: oc = 1f; fog = 0.0019f; sat = -35f; cloudSpeed = 0.07f; break; // typhoon
            }
            if (c.weatherKind == WeatherKind.Clear && w != null && c.mode == DataMode.Live) oc = Mathf.Clamp(w.cloudCover / 100f, 0.05f, 0.45f);
            if (c.fog && c.weatherKind == c.autoWeather) fog = Mathf.Max(fog, 0.0028f);
            float dark = c.weatherKind == WeatherKind.HeavyRain || c.weatherKind == WeatherKind.Typhoon ? 0.42f : c.weatherKind == WeatherKind.Cloudy ? 0.78f : c.weatherKind == WeatherKind.Clear ? 1f : 0.6f;
            if (c.weatherKind == WeatherKind.Snow) dark = 0.8f;
            float gray = Mathf.Lerp(0.06f, 0.62f, day) * Mathf.Lerp(1f, dark, day);
            float ock = Mathf.Clamp01((oc - 0.3f) / 0.6f);
            L.zenith = Color.Lerp(L.zenith, Gray(gray * 0.85f) + new Color(0, 0.01f, 0.03f) * day, ock);
            L.horizon = Color.Lerp(L.horizon, Gray(gray * 1.1f) + new Color(0, 0.01f, 0.02f) * day + hGold * twilight * 0.15f, ock);
            L.ground = L.horizon * 0.5f;
            L.sunIntensity *= 1f - 0.85f * ock;
            L.shadowStrength *= 1f - 0.9f * ock;
            L.moonIntensity *= 1f - 0.8f * ock;
            float ambBoost = Mathf.Lerp(1f, 0.9f * dark + 0.2f, ock);
            L.ambSky = Color.Lerp(L.ambSky, Gray(gray * 1.15f), ock * 0.7f) * ambBoost;
            L.ambEquator = Color.Lerp(L.ambEquator, Gray(gray), ock * 0.7f) * ambBoost;
            L.ambGround *= Mathf.Lerp(1f, 0.85f, ock);
            L.cloudCover = oc;
            L.cloudSpeed = cloudSpeed;
            L.cloudLit = Color.Lerp(L.cloudLit, Gray(gray * 1.25f), ock * 0.6f);
            L.cloudShade = Color.Lerp(L.cloudShade, Gray(gray * 0.65f), ock * 0.8f);
            L.fogDensity = fog;
            L.fogColor = Color.Lerp(L.horizon, Gray(gray), 0.35f);
            L.saturation += sat;
            L.night = Mathf.Clamp01(L.night + ock * 0.25f * day);
            // city light pollution on the horizon at night
            float glow = c.scene == SceneKind.Metropolis ? 0.16f : c.scene == SceneKind.Town || c.scene == SceneKind.Coast ? 0.07f : 0.02f;
            L.horizon += new Color(1f, 0.62f, 0.35f) * glow * nightK * (0.5f + 0.5f * ock);
            L.fogColor += new Color(1f, 0.62f, 0.35f) * glow * 0.5f * nightK;
            if (c.weatherKind == WeatherKind.Snow) { L.ambGround += Gray(0.18f) * day; L.exposure += 0.1f; }

            // hazard grading
            if (c.HasEffect(HazardKind.HeatWave)) L.filter = new Color(1f, 0.94f, 0.85f);
            if (c.HasEffect(HazardKind.ColdWave)) L.filter = new Color(0.9f, 0.96f, 1.05f);
            if (c.HasEffect(HazardKind.Volcano, Severity.Advisory) || c.HasEffect(HazardKind.Wildfire, Severity.Advisory))
            {
                L.horizon = Color.Lerp(L.horizon, new Color(0.55f, 0.32f, 0.22f) * Mathf.Lerp(0.35f, 1f, day), 0.45f);
                L.fogColor = Color.Lerp(L.fogColor, new Color(0.42f, 0.3f, 0.25f) * Mathf.Lerp(0.3f, 1f, day), 0.5f);
                L.fogDensity = Mathf.Max(L.fogDensity, 0.0007f);
            }

            switch (c.weatherKind)
            {
                case WeatherKind.Rain: L.wet = 0.65f; break;
                case WeatherKind.HeavyRain: L.wet = 0.9f; break;
                case WeatherKind.Typhoon: L.wet = 1f; break;
                case WeatherKind.Snow: L.wet = 0.15f; L.snow = 0.85f; break;
            }
            L.windStrength = c.windStrength;
            float wd = c.windDirDeg * Mathf.Deg2Rad;
            L.wind = new Vector2(Mathf.Sin(wd), Mathf.Cos(wd));
            L.precip = c.precipIntensity;
            return L;
        }

        static Vector3 Dir(float azDeg, float elevDeg)
        {
            float a = azDeg * Mathf.Deg2Rad, e = elevDeg * Mathf.Deg2Rad;
            return new Vector3(Mathf.Sin(a) * Mathf.Cos(e), Mathf.Sin(e), Mathf.Cos(a) * Mathf.Cos(e)).normalized;
        }
    }
}
