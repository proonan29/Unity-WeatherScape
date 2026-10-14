using System;
using System.Collections.Generic;
using UnityEngine;

namespace WeatherScape
{
    /// <summary>Built-in places: offline/virtual mode, quick buttons, and Korean short names the geocoders miss (e.g. "경주").</summary>
    public static class Gazetteer
    {
        public class Entry
        {
            public string ko, en; public string[] aliases;
            public double lat, lon; public long pop; public SceneKind scene; public float utc; public bool coast;
            public string regionKo, regionEn;
        }

        static Entry E(string ko, string en, double lat, double lon, long pop, SceneKind s, float utc, bool coast, string regionKo, string regionEn, params string[] aliases)
            => new Entry { ko = ko, en = en, lat = lat, lon = lon, pop = pop, scene = s, utc = utc, coast = coast, regionKo = regionKo, regionEn = regionEn, aliases = aliases };

        const SceneKind M = SceneKind.Metropolis, T = SceneKind.Town, V = SceneKind.Village, P = SceneKind.Tourist, F = SceneKind.Forest, C = SceneKind.Coast;

        public static readonly List<Entry> All = new List<Entry>
        {
            // Korea
            E("서울", "Seoul", 37.5665, 126.9780, 9_500_000, M, 9, false, "대한민국", "South Korea", "서울시", "서울특별시"),
            E("부산", "Busan", 35.1796, 129.0756, 3_300_000, M, 9, true, "대한민국", "South Korea", "부산시", "부산광역시", "Pusan"),
            E("인천", "Incheon", 37.4563, 126.7052, 2_950_000, M, 9, true, "대한민국", "South Korea", "인천시", "인천광역시"),
            E("대구", "Daegu", 35.8714, 128.6014, 2_380_000, M, 9, false, "대한민국", "South Korea", "대구시", "대구광역시"),
            E("대전", "Daejeon", 36.3504, 127.3845, 1_450_000, M, 9, false, "대한민국", "South Korea", "대전시", "대전광역시"),
            E("광주", "Gwangju", 35.1595, 126.8526, 1_430_000, M, 9, false, "대한민국", "South Korea", "광주시", "광주광역시"),
            E("수원", "Suwon", 37.2636, 127.0286, 1_190_000, M, 9, false, "경기도, 대한민국", "Gyeonggi, South Korea", "수원시"),
            E("전주", "Jeonju", 35.8242, 127.1480, 650_000, T, 9, false, "전북, 대한민국", "Jeonbuk, South Korea", "전주시"),
            E("춘천", "Chuncheon", 37.8813, 127.7298, 285_000, T, 9, false, "강원, 대한민국", "Gangwon, South Korea", "춘천시"),
            E("안동", "Andong", 36.5684, 128.7294, 155_000, T, 9, false, "경북, 대한민국", "Gyeongbuk, South Korea", "안동시"),
            E("강릉", "Gangneung", 37.7519, 128.8761, 210_000, C, 9, true, "강원, 대한민국", "Gangwon, South Korea", "강릉시", "경포대", "Gyeongpodae"),
            E("속초", "Sokcho", 38.2070, 128.5918, 82_000, C, 9, true, "강원, 대한민국", "Gangwon, South Korea", "속초시"),
            E("여수", "Yeosu", 34.7604, 127.6622, 270_000, C, 9, true, "전남, 대한민국", "Jeonnam, South Korea", "여수시"),
            E("통영", "Tongyeong", 34.8544, 128.4332, 122_000, C, 9, true, "경남, 대한민국", "Gyeongnam, South Korea", "통영시"),
            E("제주", "Jeju", 33.4996, 126.5312, 490_000, C, 9, true, "제주, 대한민국", "Jeju, South Korea", "제주시", "제주도", "Jeju Island"),
            E("서귀포", "Seogwipo", 33.2541, 126.5601, 183_000, C, 9, true, "제주, 대한민국", "Jeju, South Korea", "서귀포시"),
            E("해운대", "Haeundae", 35.1587, 129.1604, 0, C, 9, true, "부산, 대한민국", "Busan, South Korea", "해운대해수욕장", "Haeundae Beach"),
            E("울릉도", "Ulleungdo", 37.4844, 130.9057, 9_000, C, 9, true, "경북, 대한민국", "Gyeongbuk, South Korea", "Ulleung"),
            E("경주", "Gyeongju", 35.8562, 129.2247, 250_000, P, 9, false, "경북, 대한민국", "Gyeongbuk, South Korea", "경주시", "불국사", "Bulguksa"),
            E("경복궁", "Gyeongbokgung", 37.5796, 126.9770, 0, P, 9, false, "서울, 대한민국", "Seoul, South Korea", "Gyeongbokgung Palace"),
            E("N서울타워", "N Seoul Tower", 37.5512, 126.9882, 0, P, 9, false, "서울, 대한민국", "Seoul, South Korea", "남산타워", "남산서울타워", "Namsan Tower"),
            E("에버랜드", "Everland", 37.2940, 127.2026, 0, P, 9, false, "용인, 대한민국", "Yongin, South Korea"),
            E("남이섬", "Nami Island", 37.7906, 127.5254, 0, P, 9, false, "춘천, 대한민국", "Chuncheon, South Korea"),
            E("하회마을", "Hahoe Village", 36.5390, 128.5181, 230, V, 9, false, "안동, 대한민국", "Andong, South Korea", "안동하회마을", "Hahoe"),
            E("보성", "Boseong", 34.7714, 127.0800, 38_000, V, 9, false, "전남, 대한민국", "Jeonnam, South Korea", "보성군", "보성 녹차밭"),
            E("평창", "Pyeongchang", 37.3705, 128.3900, 41_000, V, 9, false, "강원, 대한민국", "Gangwon, South Korea", "평창군"),
            E("설악산", "Seoraksan", 38.1194, 128.4656, 0, F, 9, false, "강원, 대한민국", "Gangwon, South Korea", "설악산국립공원", "Seoraksan National Park"),
            E("지리산", "Jirisan", 35.3370, 127.7306, 0, F, 9, false, "경남·전남·전북, 대한민국", "South Korea", "지리산국립공원", "Jirisan National Park"),
            E("한라산", "Hallasan", 33.3617, 126.5292, 0, F, 9, false, "제주, 대한민국", "Jeju, South Korea", "한라산국립공원"),
            E("북한산", "Bukhansan", 37.6588, 126.9779, 0, F, 9, false, "서울·경기, 대한민국", "Seoul, South Korea", "북한산국립공원"),
            // World
            E("도쿄", "Tokyo", 35.6762, 139.6503, 14_000_000, M, 9, true, "일본", "Japan", "동경"),
            E("오사카", "Osaka", 34.6937, 135.5023, 2_700_000, M, 9, true, "일본", "Japan"),
            E("교토", "Kyoto", 35.0116, 135.7681, 1_460_000, P, 9, false, "일본", "Japan"),
            E("가고시마", "Kagoshima", 31.5966, 130.5571, 595_000, C, 9, true, "일본", "Japan", "사쿠라지마", "Sakurajima"),
            E("베이징", "Beijing", 39.9042, 116.4074, 21_500_000, M, 8, false, "중국", "China", "북경"),
            E("상하이", "Shanghai", 31.2304, 121.4737, 24_800_000, M, 8, true, "중국", "China", "상해"),
            E("홍콩", "Hong Kong", 22.3193, 114.1694, 7_400_000, M, 8, true, "중국", "China"),
            E("마닐라", "Manila", 14.5995, 120.9842, 1_850_000, M, 8, true, "필리핀", "Philippines"),
            E("방콕", "Bangkok", 13.7563, 100.5018, 10_500_000, M, 7, false, "태국", "Thailand"),
            E("다낭", "Da Nang", 16.0544, 108.2022, 1_200_000, C, 7, true, "베트남", "Vietnam"),
            E("발리", "Bali", -8.7180, 115.1686, 0, C, 8, true, "인도네시아", "Indonesia", "쿠타", "Kuta"),
            E("앙코르와트", "Angkor Wat", 13.4125, 103.8670, 0, P, 7, false, "캄보디아", "Cambodia", "시엠립", "Siem Reap"),
            E("말레", "Malé", 4.1755, 73.5093, 210_000, C, 5, true, "몰디브", "Maldives", "몰디브", "Maldives"),
            E("두바이", "Dubai", 25.2048, 55.2708, 3_600_000, M, 4, true, "아랍에미리트", "UAE"),
            E("시드니", "Sydney", -33.8688, 151.2093, 5_300_000, M, 11, true, "호주", "Australia"),
            E("뉴욕", "New York", 40.7128, -74.0060, 8_400_000, M, -4, true, "미국", "USA", "NYC", "New York City"),
            E("로스앤젤레스", "Los Angeles", 34.0522, -118.2437, 3_900_000, M, -7, true, "미국", "USA", "LA", "엘에이"),
            E("샌프란시스코", "San Francisco", 37.7749, -122.4194, 810_000, M, -7, true, "미국", "USA"),
            E("힐로", "Hilo", 19.7241, -155.0868, 45_000, C, -10, true, "하와이, 미국", "Hawaii, USA", "하와이", "Hawaii", "킬라우에아", "Kilauea"),
            E("옐로스톤", "Yellowstone", 44.4280, -110.5885, 0, F, -6, false, "미국", "USA", "옐로스톤국립공원", "Yellowstone National Park"),
            E("그랜드캐니언", "Grand Canyon", 36.1069, -112.1129, 0, F, -7, false, "미국", "USA", "Grand Canyon National Park"),
            E("밴프", "Banff", 51.1784, -115.5708, 0, F, -6, false, "캐나다", "Canada", "밴프국립공원", "Banff National Park"),
            E("런던", "London", 51.5074, -0.1278, 8_900_000, M, 1, false, "영국", "UK"),
            E("파리", "Paris", 48.8566, 2.3522, 2_100_000, M, 2, false, "프랑스", "France"),
            E("에펠탑", "Eiffel Tower", 48.8584, 2.2945, 0, P, 2, false, "파리, 프랑스", "Paris, France"),
            E("로마", "Rome", 41.9028, 12.4964, 2_800_000, M, 2, false, "이탈리아", "Italy"),
            E("콜로세움", "Colosseum", 41.8902, 12.4922, 0, P, 2, false, "로마, 이탈리아", "Rome, Italy"),
            E("나폴리", "Naples", 40.8518, 14.2681, 910_000, C, 2, true, "이탈리아", "Italy", "베수비오", "Vesuvius"),
            E("카타니아", "Catania", 37.5079, 15.0830, 300_000, C, 2, true, "시칠리아, 이탈리아", "Sicily, Italy", "에트나", "Etna"),
            E("산토리니", "Santorini", 36.3932, 25.4615, 15_000, C, 3, true, "그리스", "Greece"),
            E("할슈타트", "Hallstatt", 47.5622, 13.6493, 750, V, 2, false, "오스트리아", "Austria"),
            E("체르마트", "Zermatt", 46.0207, 7.7491, 5_800, V, 2, false, "스위스", "Switzerland"),
            E("슈바르츠발트", "Black Forest", 48.0000, 8.2000, 0, F, 2, false, "독일", "Germany", "검은숲", "Schwarzwald"),
            E("레이캬비크", "Reykjavik", 64.1466, -21.9426, 135_000, C, 0, true, "아이슬란드", "Iceland", "Reykjavík"),
            E("마추픽추", "Machu Picchu", -13.1631, -72.5450, 0, P, -5, false, "페루", "Peru"),
            E("아마존", "Amazon Rainforest", -3.4653, -62.2159, 0, F, -4, false, "브라질", "Brazil", "아마존 열대우림"),
        };

        static string Norm(string s) => (s ?? "").Trim().Replace(" ", "").ToLowerInvariant();

        public static Entry Find(string query)
        {
            string q = Norm(query);
            if (q.Length == 0) return null;
            foreach (var e in All)
            {
                if (Norm(e.ko) == q || Norm(e.en) == q) return e;
                if (e.aliases != null) foreach (var a in e.aliases) if (Norm(a) == q) return e;
            }
            return null;
        }

        public static PlaceInfo ToPlace(Entry e, bool korean, string query = null)
        {
            return new PlaceInfo
            {
                query = query ?? e.ko,
                name = korean ? e.ko : e.en,
                region = korean ? e.regionKo : e.regionEn,
                country = korean ? e.regionKo : e.regionEn,
                lat = e.lat, lon = e.lon, population = e.pop,
                category = "builtin", type = e.scene.ToString(),
                coastal = e.coast, coastalKnown = true,
                scene = e.scene, sceneFixed = true,
                sceneReason = "built-in list",
                utcOffsetHours = e.utc,
                source = "built-in",
            };
        }

        /// <summary>Quick buttons: one example per scene kind plus volcano/typhoon-prone places.</summary>
        public static readonly string[] QuickPicks = { "서울", "전주", "하회마을", "경주", "설악산", "해운대", "도쿄", "뉴욕", "레이캬비크", "마닐라", "카타니아", "힐로" };
    }

    /// <summary>Well-known active volcanoes (for "near an active volcano" info and the volcano backdrop).</summary>
    public static class Volcanoes
    {
        public struct V { public string ko, en; public double lat, lon; public V(string k, string e, double la, double lo) { ko = k; en = e; lat = la; lon = lo; } }

        public static readonly V[] All =
        {
            new V("사쿠라지마", "Sakurajima", 31.585, 130.657), new V("아소산", "Mount Aso", 32.884, 131.104),
            new V("후지산", "Mount Fuji", 35.3606, 138.7274), new V("아사마산", "Mount Asama", 36.406, 138.523),
            new V("운젠다케", "Unzen", 32.761, 130.299), new V("백두산", "Baekdusan (Changbai)", 42.006, 128.056),
            new V("에트나", "Mount Etna", 37.751, 14.993), new V("베수비오", "Vesuvius", 40.821, 14.426),
            new V("스트롬볼리", "Stromboli", 38.789, 15.213), new V("캄피 플레그레이", "Campi Flegrei", 40.827, 14.139),
            new V("산토리니(테라)", "Santorini (Thera)", 36.404, 25.396), new V("킬라우에아", "Kilauea", 19.421, -155.287),
            new V("마우나로아", "Mauna Loa", 19.475, -155.608), new V("세인트헬렌스산", "Mount St. Helens", 46.200, -122.180),
            new V("레이니어산", "Mount Rainier", 46.853, -121.760), new V("옐로스톤 칼데라", "Yellowstone Caldera", 44.430, -110.670),
            new V("포포카테페틀", "Popocatépetl", 19.023, -98.622), new V("므라피", "Merapi", -7.542, 110.442),
            new V("아낙 크라카타우", "Anak Krakatau", -6.102, 105.423), new V("아궁산", "Mount Agung", -8.343, 115.508),
            new V("스메루", "Semeru", -8.108, 112.922), new V("마욘", "Mayon", 13.257, 123.685),
            new V("탈 화산", "Taal", 14.002, 120.993), new V("피나투보", "Pinatubo", 15.130, 120.350),
            new V("헤클라", "Hekla", 63.980, -19.700), new V("레이캬네스 화산계", "Reykjanes (Sundhnúkur)", 63.880, -22.420),
            new V("에이야퍄들라이외퀴들", "Eyjafjallajökull", 63.630, -19.620), new V("비야리카", "Villarrica", -39.420, -71.930),
            new V("코토팍시", "Cotopaxi", -0.677, -78.436), new V("니라공고", "Nyiragongo", -1.520, 29.250),
            new V("루아페후", "Ruapehu", -39.280, 175.570), new V("클류쳅스카야", "Klyuchevskaya Sopka", 56.056, 160.642),
        };

        public static bool Nearest(double lat, double lon, out V v, out float km)
        {
            v = default; km = float.MaxValue;
            foreach (var x in All)
            {
                float d = Geo.Km(lat, lon, x.lat, x.lon);
                if (d < km) { km = d; v = x; }
            }
            return km < float.MaxValue;
        }
    }

    public static class Geo
    {
        public static float Km(double lat1, double lon1, double lat2, double lon2)
        {
            const double R = 6371.0;
            double dLat = (lat2 - lat1) * Math.PI / 180, dLon = (lon2 - lon1) * Math.PI / 180;
            double a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) + Math.Cos(lat1 * Math.PI / 180) * Math.Cos(lat2 * Math.PI / 180) * Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
            return (float)(R * 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a)));
        }

        /// <summary>Compass bearing from point 1 to point 2 (degrees, 0 = north).</summary>
        public static float Bearing(double lat1, double lon1, double lat2, double lon2)
        {
            double p1 = lat1 * Math.PI / 180, p2 = lat2 * Math.PI / 180, dl = (lon2 - lon1) * Math.PI / 180;
            double y = Math.Sin(dl) * Math.Cos(p2), x = Math.Cos(p1) * Math.Sin(p2) - Math.Sin(p1) * Math.Cos(p2) * Math.Cos(dl);
            return (float)((Math.Atan2(y, x) * 180 / Math.PI + 360) % 360);
        }
    }
}
