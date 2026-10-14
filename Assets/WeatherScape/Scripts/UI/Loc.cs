using System.Collections.Generic;
using UnityEngine;

namespace WeatherScape.UI
{
    /// <summary>Tiny localisation table: English text is the key, Korean is looked up. Choice saved in PlayerPrefs.</summary>
    public static class Loc
    {
        const string PrefKey = "ws_lang";
        static bool? korean;
        public static event System.Action Changed;

        public static bool Korean
        {
            get { if (korean == null) korean = PlayerPrefs.GetInt(PrefKey, 1) == 1; return korean.Value; }
            set { korean = value; PlayerPrefs.SetInt(PrefKey, value ? 1 : 0); PlayerPrefs.Save(); Changed?.Invoke(); }
        }

        static readonly Dictionary<string, string> Ko = new Dictionary<string, string>
        {
            // panel
            ["Weather scenes for any place"] = "장소별 날씨 장면",
            ["Place"] = "장소",
            ["City, landmark, mountain, beach…"] = "도시, 명소, 산, 해변…",
            ["Search"] = "검색",
            ["Searching…"] = "검색 중…",
            ["Loading weather…"] = "날씨 불러오는 중…",
            ["No place found"] = "장소를 찾지 못했습니다",
            ["Offline – using virtual data"] = "오프라인 – 가상 데이터를 사용합니다",
            ["Quick picks"] = "바로 가기",
            ["Results"] = "검색 결과",
            ["Now"] = "현재",
            ["Data"] = "데이터",
            ["Live"] = "실시간",
            ["Virtual"] = "가상",
            ["Refresh"] = "새로고침",
            ["Display"] = "표시",
            ["Scene"] = "장면",
            ["Time of day"] = "시간대",
            ["Weather"] = "날씨",
            ["Auto"] = "자동",
            ["All auto"] = "모두 자동",
            ["Virtual disaster"] = "가상 재해",
            ["Settings"] = "설정",
            ["Language"] = "언어",
            ["Sound"] = "소리",
            ["On"] = "켜기",
            ["Off"] = "끄기",
            ["Auto orbit"] = "자동 회전",
            ["Data: Open-Meteo, OpenStreetMap Nominatim, USGS, GDACS, GloFAS"] = "데이터: Open-Meteo, OpenStreetMap Nominatim, USGS, GDACS, GloFAS",
            ["Drag to rotate · wheel to zoom"] = "드래그: 회전 · 휠: 확대/축소",
            ["No special notices"] = "특이사항 없음",
            ["local time"] = "현지 시각",
            ["Sunrise {0} · Sunset {1}"] = "일출 {0} · 일몰 {1}",
            ["Feels like {0}"] = "체감 {0}",
            ["Humidity {0}%"] = "습도 {0}%",
            ["Wind {0} m/s {1} (gust {2})"] = "바람 {1} {0} m/s (돌풍 {2})",
            ["Precipitation {0} mm/h"] = "강수 {0} mm/h",
            ["Snowfall {0} cm/h"] = "강설 {0} cm/h",
            ["Cloud {0}%"] = "구름 {0}%",
            ["Visibility {0} km"] = "가시거리 {0} km",
            ["Waves {0} m"] = "파고 {0} m",
            ["Source: {0} · updated {1}"] = "출처: {0} · 갱신 {1}",
            ["auto: {0}"] = "자동: {0}",
            ["(override)"] = "(직접 선택)",
            ["Showing"] = "표시 중",
            ["virtual"] = "가상",
            ["built-in list"] = "내장 목록",
            ["population {0} ≥ 1,000,000"] = "인구 {0} ≥ 100만",
            ["population {0} ≥ 50,000"] = "인구 {0} ≥ 5만",
            ["population {0} < 50,000"] = "인구 {0} < 5만",

            // scenes
            ["Metropolis"] = "대도시",
            ["Town"] = "중소도시",
            ["Village"] = "시골마을",
            ["Tourist site"] = "관광지",
            ["Forest / park"] = "숲/국립공원",
            ["Coast / harbour"] = "바다/항구",
            // phases
            ["Night"] = "밤",
            ["Morning"] = "아침",
            ["Day"] = "낮",
            ["Evening"] = "저녁",
            // weather
            ["Clear"] = "맑음",
            ["Cloudy"] = "구름",
            ["Rain"] = "비",
            ["Snow"] = "눈",
            ["Heavy rain"] = "폭우",
            ["Typhoon"] = "태풍",
            ["thunder"] = "뇌우",
            ["fog"] = "안개",
            // hazards
            ["Volcano"] = "화산",
            ["Earthquake"] = "지진",
            ["Flood"] = "홍수",
            ["Tsunami"] = "쓰나미",
            ["Wildfire"] = "산불",
            ["Volcanic eruption"] = "화산 분화",
            ["Volcanic activity"] = "화산 활동",
            ["Active volcano nearby"] = "활화산 인근 지역",
            ["Earthquake (GDACS alert)"] = "지진 (GDACS 경보)",
            ["Tsunami possible – check official warnings"] = "쓰나미 가능성 – 공식 경보를 확인하세요",
            ["Tropical cyclone"] = "열대저기압(태풍)",
            ["Flood alert"] = "홍수 경보",
            ["River level high"] = "하천 수위 높음",
            ["Heavy rain – flood risk"] = "호우 – 침수 위험",
            ["Drought"] = "가뭄",
            ["Heat wave"] = "폭염",
            ["Cold wave"] = "한파",
            ["Strong wind"] = "강풍",
            ["Heavy snow"] = "대설",
            ["Dense fog"] = "짙은 안개",
            // severity
            ["Info"] = "정보",
            ["Advisory"] = "주의",
            ["Warning"] = "경보",
            ["Severe"] = "위험",
            // seasons
            ["Spring"] = "봄", ["Summer"] = "여름", ["Autumn"] = "가을", ["Winter"] = "겨울",
        };

        public static string T(string en) => Korean && Ko.TryGetValue(en, out var k) ? k : en;
        public static string F(string en, params object[] args) => string.Format(T(en), args);

        public static readonly string[] SceneNames = { "Metropolis", "Town", "Village", "Tourist site", "Forest / park", "Coast / harbour" };
        public static readonly string[] PhaseNames = { "Night", "Morning", "Day", "Evening" };
        public static readonly string[] WeatherNames = { "Clear", "Cloudy", "Rain", "Snow", "Heavy rain", "Typhoon" };

        public static string Scene(SceneKind k) => T(SceneNames[(int)k]);
        public static string Phase(DayPhase p) => T(PhaseNames[(int)p]);
        public static string Weather(WeatherKind w) => T(WeatherNames[(int)w]);

        public static string Hazard(HazardKind k)
        {
            switch (k)
            {
                case HazardKind.TropicalCyclone: return T("Tropical cyclone");
                case HazardKind.HeatWave: return T("Heat wave");
                case HazardKind.ColdWave: return T("Cold wave");
                case HazardKind.StrongWind: return T("Strong wind");
                case HazardKind.HeavySnow: return T("Heavy snow");
                case HazardKind.Fog: return T("Dense fog");
                default: return T(k.ToString());
            }
        }

        static readonly string[] CompassEn = { "N", "NNE", "NE", "ENE", "E", "ESE", "SE", "SSE", "S", "SSW", "SW", "WSW", "W", "WNW", "NW", "NNW" };
        static readonly string[] CompassKo = { "북풍", "북북동풍", "북동풍", "동북동풍", "동풍", "동남동풍", "남동풍", "남남동풍", "남풍", "남남서풍", "남서풍", "서남서풍", "서풍", "서북서풍", "북서풍", "북북서풍" };

        /// <summary>Meteorological wind direction (where it blows from).</summary>
        public static string Compass(float deg)
        {
            int i = Mathf.RoundToInt(((deg % 360f) + 360f) % 360f / 22.5f) % 16;
            return Korean ? CompassKo[i] : CompassEn[i];
        }
    }
}
