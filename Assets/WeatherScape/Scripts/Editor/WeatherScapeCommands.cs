using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Unity.Pipeline.Commands;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.TextCore.Text;
using UnityEngine.UIElements;
using WeatherScape.Net;
using WeatherScape.UI;

namespace WeatherScape.EditorTools
{
    /// <summary>CLI commands for the weather scene app: `unity command ws_*`.</summary>
    public static class WeatherScapeCommands
    {
        const string ScenePath = "Assets/WeatherScape/Scenes/WeatherScape.unity";
        const string MatFolder = "Assets/WeatherScape/Materials";
        const string SkyPath = MatFolder + "/Sky.mat";
        const string SkyShaderPath = "Assets/WeatherScape/Shaders/Sky.shader";
        const string ResFolder = "Assets/WeatherScape/Resources";
        const string UiFolder = "Assets/WeatherScape/UI";
        const string PanelSettingsPath = UiFolder + "/WeatherScapePanelSettings.asset";
        const string ThemePath = UiFolder + "/WeatherScapeTheme.tss";
        const string TextSettingsPath = UiFolder + "/WeatherScapeTextSettings.asset";
        const string FontPath = UiFolder + "/Fonts/NotoSansKR-Regular.otf";
        const string FontAssetPath = UiFolder + "/Fonts/NotoSansKR-Regular SDF.asset";

        static string F1(float v) => v.ToString("0.0", CultureInfo.InvariantCulture);

        static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder)) return;
            var parent = Path.GetDirectoryName(folder).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(folder));
        }

        static Material EnsureMaterial(string path, Shader shader, Action<Material> init)
        {
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null) { m = new Material(shader) { name = Path.GetFileNameWithoutExtension(path) }; init?.Invoke(m); AssetDatabase.CreateAsset(m, path); }
            else if (m.shader != shader) { m.shader = shader; init?.Invoke(m); EditorUtility.SetDirty(m); }
            return m;
        }

        // ------------------------------------------------------------------ scene

        [CliCommand("ws_setup_scene", "Create the WeatherScape scene: sun/flash lights, camera rig, post-processing volume, world, app controller and UI. Saves the scene.")]
        public static string SetupScene(
            [CliArg("place", "Place shown initially (virtual data)")] string place = "서울",
            [CliArg("scene_path", "Scene asset path")] string scenePath = ScenePath)
        {
            if (Application.isPlaying) return "error: exit play mode first";
            var sw = System.Diagnostics.Stopwatch.StartNew();
            EnsureFolder(MatFolder); EnsureFolder(ResFolder); EnsureFolder(Path.GetDirectoryName(scenePath).Replace('\\', '/'));

            var lit = EnsureMaterial(ResFolder + "/WS_Lit.mat", Shader.Find("Universal Render Pipeline/Lit"), m => m.enableInstancing = true);
            var part = EnsureMaterial(ResFolder + "/WS_Particle.mat", Shader.Find("Universal Render Pipeline/Particles/Unlit"), null);
            var skyShader = AssetDatabase.LoadAssetAtPath<Shader>(SkyShaderPath);
            if (skyShader == null) return $"error: sky shader missing at {SkyShaderPath}";
            var sky = EnsureMaterial(SkyPath, skyShader, null);
            // keep the water shader in builds (it is only found by name at runtime)
            var waterShader = Shader.Find("WeatherScape/Water");
            if (waterShader != null) EnsureMaterial(ResFolder + "/WS_Water.mat", waterShader, null);

            var urp = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            if (urp != null) { urp.shadowDistance = 650f; urp.shadowCascadeCount = 4; urp.supportsHDR = true; EditorUtility.SetDirty(urp); }
            PlayerSettings.runInBackground = true;

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var sunGo = new GameObject("Sun");
            var sun = sunGo.AddComponent<Light>();
            sun.type = LightType.Directional; sun.intensity = 1.3f; sun.shadows = LightShadows.Soft; sun.shadowStrength = 0.85f;
            sunGo.transform.rotation = Quaternion.Euler(45f, -30f, 0f);
            var flashGo = new GameObject("LightningFlash");
            var flash = flashGo.AddComponent<Light>();
            flash.type = LightType.Directional; flash.intensity = 0f; flash.color = new Color(0.8f, 0.85f, 1f); flash.shadows = LightShadows.None; flash.enabled = false;
            flashGo.transform.rotation = Quaternion.Euler(70f, 20f, 0f);

            var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
            var cam = camGo.AddComponent<Camera>();
            cam.fieldOfView = 42f; cam.nearClipPlane = 0.5f; cam.farClipPlane = 6000f; cam.clearFlags = CameraClearFlags.Skybox;
            camGo.AddComponent<AudioListener>();
            var camData = camGo.AddComponent<UniversalAdditionalCameraData>();
            camData.renderPostProcessing = true; camData.stopNaN = true;   // never let a NaN pixel bloom across the screen
            camData.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            var rig = camGo.AddComponent<CameraRig>();

            var volGo = new GameObject("PostProcess");
            var vol = volGo.AddComponent<Volume>(); vol.isGlobal = true;

            var worldGo = new GameObject("World");
            var world = worldGo.AddComponent<WeatherWorld>();
            world.sun = sun; world.flashLight = flash; world.skyMaterial = sky; world.volume = vol; world.rig = rig;

            var appGo = new GameObject("App");
            var app = appGo.AddComponent<AppController>();
            app.world = world;
            app.startPlace = place;

            var uiGo = new GameObject("UI");
            var doc = uiGo.AddComponent<UIDocument>();
            doc.panelSettings = EnsurePanelSettings();
            var ui = uiGo.AddComponent<WeatherScapeUI>();
            ui.app = app; ui.rig = rig;
            SetupFont(false);

            RenderSettings.skybox = sky;
            RenderSettings.sun = sun;
            var e = Gazetteer.Find(place);
            var p = e != null ? Gazetteer.ToPlace(e, Loc.Korean, place) : VirtualWeather.FakePlace(place, Loc.Korean);
            app.mode = WeatherScape.DataMode.Live;
            app.Apply(Resolver.Virtual(p, DateTime.UtcNow), true);

            EditorSceneManager.SaveScene(scene, scenePath);
            var list = EditorBuildSettings.scenes.Where(s => s.path != scenePath && !s.path.EndsWith("SampleScene.unity")).ToList();
            list.Insert(0, new EditorBuildSettingsScene(scenePath, true));
            EditorBuildSettings.scenes = list.ToArray();
            AssetDatabase.SaveAssets();
            return $"scene={scenePath} lit={lit != null} particle={part != null} water={waterShader != null} build_ms={sw.ElapsedMilliseconds}\n{world.Describe()}";
        }

        static PanelSettings EnsurePanelSettings()
        {
            var ps = AssetDatabase.LoadAssetAtPath<PanelSettings>(PanelSettingsPath);
            if (ps != null) return ps;
            ps = ScriptableObject.CreateInstance<PanelSettings>();
            ps.themeStyleSheet = AssetDatabase.LoadAssetAtPath<ThemeStyleSheet>(ThemePath);
            ps.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            ps.referenceResolution = new Vector2Int(1920, 1080);
            ps.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
            ps.match = 0.5f;
            AssetDatabase.CreateAsset(ps, PanelSettingsPath);
            return ps;
        }

        [CliCommand("ws_setup_font", "Create the Noto Sans KR dynamic font asset and register it as a UI fallback font.")]
        public static string SetupFont([CliArg("force", "Recreate the font asset")] bool force = false)
        {
            var font = AssetDatabase.LoadAssetAtPath<Font>(FontPath);
            if (font == null) return $"error: font not found: {FontPath}";
            var fa = AssetDatabase.LoadAssetAtPath<FontAsset>(FontAssetPath);
            if (fa != null && force) { AssetDatabase.DeleteAsset(FontAssetPath); fa = null; }
            if (fa == null)
            {
                fa = FontAsset.CreateFontAsset(font, 90, 9, GlyphRenderMode.SDFAA, 1024, 1024, AtlasPopulationMode.Dynamic, true);
                if (fa == null) return "error: CreateFontAsset failed";
                fa.name = "NotoSansKR-Regular SDF";
                fa.isMultiAtlasTexturesEnabled = true;
                AssetDatabase.CreateAsset(fa, FontAssetPath);
                if (fa.atlasTextures != null) foreach (var tex in fa.atlasTextures) if (tex != null) { tex.name = fa.name + " Atlas"; AssetDatabase.AddObjectToAsset(tex, fa); }
                if (fa.material != null) { fa.material.name = fa.name + " Material"; AssetDatabase.AddObjectToAsset(fa.material, fa); }
                EditorUtility.SetDirty(fa);
            }
            var ts = AssetDatabase.LoadAssetAtPath<PanelTextSettings>(TextSettingsPath);
            if (ts == null) { ts = ScriptableObject.CreateInstance<PanelTextSettings>(); AssetDatabase.CreateAsset(ts, TextSettingsPath); }
            if (ts.fallbackFontAssets == null) ts.fallbackFontAssets = new List<FontAsset>();
            if (!ts.fallbackFontAssets.Contains(fa)) ts.fallbackFontAssets.Add(fa);
            ts.clearDynamicDataOnBuild = true;
            EditorUtility.SetDirty(ts);
            var ps = EnsurePanelSettings();
            ps.textSettings = ts;
            EditorUtility.SetDirty(ps);
            AssetDatabase.SaveAssets();
            return $"font={FontAssetPath} hangul={fa.HasCharacter('한', true, true)}";
        }

        // ------------------------------------------------------------------ helpers

        static AppController App(out string error)
        {
            error = null;
            var app = UnityEngine.Object.FindAnyObjectByType<AppController>();
            if (app == null) error = "error: no AppController in the active scene (run ws_setup_scene)";
            return app;
        }

        static T Run<T>(Func<Task<T>> f) => Task.Run(f).GetAwaiter().GetResult();

        static int ParseEnum<T>(string s) where T : struct
        {
            if (string.IsNullOrEmpty(s) || s == "auto" || s == "-1") return -1;
            if (int.TryParse(s, out var i)) return i;
            return Enum.TryParse<T>(s, true, out var v) ? Convert.ToInt32(v) : -1;
        }

        static List<HazardKind> ParseHazards(string s)
        {
            var list = new List<HazardKind>();
            if (string.IsNullOrEmpty(s)) return list;
            foreach (var part in s.Split(',', ';', ' '))
                if (Enum.TryParse<HazardKind>(part.Trim(), true, out var k)) list.Add(k);
            return list;
        }

        static string Summary(Conditions c)
        {
            var sb = new StringBuilder();
            var p = c.place; var w = c.weather;
            sb.AppendLine($"place={p.name} ({p.region}) lat={p.lat:0.###} lon={p.lon:0.###} pop={p.population} class={p.category}/{p.type} coastal={p.coastal} source={p.source}");
            sb.AppendLine($"scene auto={c.autoScene} ({p.sceneReason}) shown={c.scene}");
            if (w != null)
            {
                sb.AppendLine($"local={w.localTimeText} utc_offset={w.utcOffsetSeconds / 3600f:0.##}h sunrise={w.sunriseMinutes / 60f:0.00}h sunset={w.sunsetMinutes / 60f:0.00}h phase auto={c.autoPhase} shown={c.phase} hour={c.hour:0.00}");
                sb.AppendLine($"weather code={w.weatherCode} T={F1(w.temperature)}C feels={F1(w.apparent)} RH={w.humidity:0}% precip={F1(w.precipitation)}mm rain={F1(w.rain)} snow={F1(w.snowfall)}cm cloud={w.cloudCover:0}% wind={F1(w.windSpeed)}m/s dir={w.windDir:0} gust={F1(w.windGust)} vis={w.visibility:0}m waves={(float.IsNaN(w.waveHeight) ? "-" : F1(w.waveHeight))} river={(float.IsNaN(w.riverDischarge) ? "-" : w.riverDischarge.ToString("0"))}/{(float.IsNaN(w.riverMedian) ? "-" : w.riverMedian.ToString("0"))} source={w.source}");
                sb.AppendLine($"weather auto={c.autoWeather} thunder={c.thunder} fog={c.fog} shown={c.weatherKind} wind_strength={c.windStrength:0.00} precip_intensity={c.precipIntensity:0.00}");
            }
            sb.AppendLine($"hazards={c.hazards.Count}");
            foreach (var h in c.hazards)
                sb.AppendLine($"  [{h.severity}] {h.kind} '{h.title}' M={F1(h.magnitude)} dist={h.distanceKm:0}km {h.detail} ({h.source}{(h.isVirtual ? ", virtual" : "")})");
            return sb.ToString();
        }

        // ------------------------------------------------------------------ data (no scene change)

        [CliCommand("ws_lookup", "Search a place (built-in list → Nominatim → Open-Meteo), classify it and fetch live weather + special conditions. Does not change the scene.")]
        public static string Lookup([CliArg("query", "Place name")] string query = "서울",
                                    [CliArg("pick", "Index of the search result to use")] int pick = 0,
                                    [CliArg("lang", "ko or en")] string lang = "ko")
        {
            bool ko = lang != "en";
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var results = Run(() => Geocoder.Search(query, ko));
            var sb = new StringBuilder();
            sb.AppendLine($"query='{query}' results={results.Count} search_ms={sw.ElapsedMilliseconds}");
            for (int i = 0; i < results.Count; i++) sb.AppendLine($"  #{i} {results[i].name} | {results[i].region} | {results[i].category}/{results[i].type} pop={results[i].population} imp={results[i].importance:0.00} ({results[i].source})");
            if (results.Count == 0) return sb.ToString();
            var p = results[Mathf.Clamp(pick, 0, results.Count - 1)];
            var c = Run(() => Resolver.Live(p, ko));
            Resolver.Resolve(c, new Overrides());
            sb.AppendLine($"total_ms={sw.ElapsedMilliseconds}");
            sb.Append(Summary(c));
            return sb.ToString();
        }

        [CliCommand("ws_classify", "Classify several places into the six scenes (comma separated). Live search, ~1-2 s per place.")]
        public static string ClassifyPlaces([CliArg("queries", "Comma separated place names")] string queries = "서울,전주,하회마을,경주,설악산,해운대",
                                            [CliArg("lang", "ko or en")] string lang = "ko")
        {
            bool ko = lang != "en";
            var sb = new StringBuilder();
            foreach (var q in queries.Split(','))
            {
                var name = q.Trim(); if (name.Length == 0) continue;
                try
                {
                    var res = Run(() => Geocoder.Search(name, ko));
                    if (res.Count == 0) { sb.AppendLine($"{name}: not found"); continue; }
                    var p = res[0];
                    Run(async () => { await Geocoder.Enrich(p, ko); return 0; });
                    sb.AppendLine($"{name} → {p.scene} | {p.name} ({p.region}) {p.category}/{p.type} pop={p.population} coastal={p.coastal} reason={p.sceneReason} src={p.source}");
                }
                catch (Exception e) { sb.AppendLine($"{name}: error {e.Message}"); }
            }
            return sb.ToString();
        }

        // ------------------------------------------------------------------ show (edit mode preview / play mode)

        [CliCommand("ws_show", "Show a place with optional overrides. mode=virtual (default, instant) or live. scene/phase/weather: name or index (-1 = auto). hazards: comma list of virtual disasters (Volcano,Earthquake,Flood,Tsunami,Wildfire).")]
        public static string Show([CliArg("place", "Place name")] string place = "서울",
                                  [CliArg("mode", "virtual or live")] string mode = "virtual",
                                  [CliArg("scene", "Metropolis|Town|Village|Tourist|Forest|Coast or -1")] string scene = "-1",
                                  [CliArg("phase", "Night|Morning|Day|Evening or -1")] string phase = "-1",
                                  [CliArg("weather", "Clear|Cloudy|Rain|Snow|HeavyRain|Typhoon or -1")] string weather = "-1",
                                  [CliArg("hazards", "Virtual hazards, comma separated")] string hazards = "",
                                  [CliArg("yaw", "Camera yaw (deg), NaN = scene default")] float yaw = float.NaN)
        {
            var app = App(out var err); if (app == null) return err;
            bool ko = Loc.Korean;
            Conditions c;
            if (mode == "live")
            {
                var res = Run(() => Geocoder.Search(place, ko));
                if (res.Count == 0) return $"error: '{place}' not found";
                var p = res[0];
                c = Run(() => Resolver.Live(p, ko));
                app.mode = WeatherScape.DataMode.Live;
            }
            else
            {
                var e = Gazetteer.Find(place);
                var p = e != null ? Gazetteer.ToPlace(e, ko, place) : VirtualWeather.FakePlace(place, ko);
                c = Resolver.Virtual(p, DateTime.UtcNow);
                app.mode = WeatherScape.DataMode.Virtual;
            }
            app.overrides.scene = ParseEnum<SceneKind>(scene);
            app.overrides.phase = ParseEnum<DayPhase>(phase);
            app.overrides.weather = ParseEnum<WeatherKind>(weather);
            app.overrides.virtualHazards = ParseHazards(hazards);
            app.Apply(c, true);
            if (!float.IsNaN(yaw) && app.world.rig != null) app.world.rig.yaw = yaw;
            if (!Application.isPlaying) EditorSceneManager.MarkSceneDirty(app.gameObject.scene);
            return Summary(app.current) + app.world.Describe();
        }

        [CliCommand("ws_status", "Current conditions and scene statistics.")]
        public static string Status()
        {
            var app = App(out var err); if (app == null) return err;
            if (app.current == null || app.current.place == null) return "no conditions";
            return $"mode={app.mode} busy={app.Busy} status='{app.Status}' play={Application.isPlaying}\n" + Summary(app.current) + app.world.Describe();
        }

        [CliCommand("ws_search", "Play mode: run the app's search (async, like typing in the UI). Use ws_status afterwards.")]
        public static string PlaySearch([CliArg("query", "Place name")] string query = "부산")
        {
            var app = App(out var err); if (app == null) return err;
            if (!Application.isPlaying) return "error: play mode only (use ws_show in edit mode)";
            app.Search(query);
            return $"searching '{query}'";
        }

        [CliCommand("ws_set", "Set overrides on the running app (-1 = auto) and/or the data mode.")]
        public static string Set([CliArg("scene", "Scene or -1")] string scene = "",
                                 [CliArg("phase", "Phase or -1")] string phase = "",
                                 [CliArg("weather", "Weather or -1")] string weather = "",
                                 [CliArg("hazards", "Virtual hazards (comma list, 'none' clears)")] string hazards = "",
                                 [CliArg("mode", "live or virtual")] string mode = "",
                                 [CliArg("lang", "ko or en")] string lang = "")
        {
            var app = App(out var err); if (app == null) return err;
            if (lang == "ko") Loc.Korean = true; else if (lang == "en") Loc.Korean = false;
            if (scene != "") app.SetScene(ParseEnum<SceneKind>(scene));
            if (phase != "") app.SetPhase(ParseEnum<DayPhase>(phase));
            if (weather != "") app.SetWeather(ParseEnum<WeatherKind>(weather));
            if (hazards != "")
            {
                app.overrides.virtualHazards = hazards == "none" ? new List<HazardKind>() : ParseHazards(hazards);
                app.SetScene(app.overrides.scene);
            }
            if (mode == "live") app.SetMode(WeatherScape.DataMode.Live); else if (mode == "virtual") app.SetMode(WeatherScape.DataMode.Virtual);
            return Status();
        }

        // ------------------------------------------------------------------ screenshots

        static Texture2D Render(Camera cam, int w, int h)
        {
            var rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB) { antiAliasing = 4 };
            var req = new RenderPipeline.StandardRequest { destination = rt };
            if (RenderPipeline.SupportsRenderRequest(cam, req)) RenderPipeline.SubmitRenderRequest(cam, req);
            else { cam.targetTexture = rt; cam.Render(); cam.targetTexture = null; }
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            tex.Apply();
            RenderTexture.active = prev;
            rt.Release(); UnityEngine.Object.DestroyImmediate(rt);
            return tex;
        }

        static string ShotPath(string name)
        {
            var dir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Screenshots"));
            Directory.CreateDirectory(dir);
            return Path.Combine(dir, name.EndsWith(".png") ? name : name + ".png");
        }

        [CliCommand("ws_capture", "Render the main camera (scene only, no UI) to Screenshots/<name>.png.")]
        public static string Capture([CliArg("name", "File name")] string name = "shot", [CliArg("width", "Width")] int width = 1600, [CliArg("height", "Height")] int height = 900)
        {
            var app = App(out var err); if (app == null) return err;
            var rig = app.world.rig;
            rig.panelPixels = 0f;
            app.world.PrepareCapture();
            rig.Place();
            var tex = Render(rig.Cam, width, height);
            var path = ShotPath(name);
            File.WriteAllBytes(path, tex.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(tex);
            return $"saved {path}";
        }

        [CliCommand("ws_matrix", "Contact sheet for one scene: rows = Night, Morning, Day, Evening; columns = Clear, Cloudy, Rain, Snow, HeavyRain, Typhoon. Screenshots/matrix_<scene>.png")]
        public static string Matrix([CliArg("scene", "Scene kind")] string scene = "Metropolis",
                                    [CliArg("place", "Place used for seed/latitude")] string place = "",
                                    [CliArg("tile", "Tile width (px)")] int tile = 400)
        {
            var app = App(out var err); if (app == null) return err;
            int k = ParseEnum<SceneKind>(scene);
            if (k < 0) return "error: scene?";
            if (string.IsNullOrEmpty(place)) place = new[] { "서울", "전주", "하회마을", "경주", "설악산", "해운대" }[k];
            var e = Gazetteer.Find(place);
            var p = e != null ? Gazetteer.ToPlace(e, Loc.Korean, place) : VirtualWeather.FakePlace(place, Loc.Korean);
            var c = Resolver.Virtual(p, DateTime.UtcNow);
            int tw = tile, th = tile * 9 / 16, gap = 4;
            var sheet = new Texture2D(6 * tw + 5 * gap, 4 * th + 3 * gap, TextureFormat.RGB24, false);
            var fill = new Color32[sheet.width * sheet.height];
            for (int i = 0; i < fill.Length; i++) fill[i] = new Color32(20, 22, 26, 255);
            sheet.SetPixels32(fill);
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var rig = app.world.rig;
            rig.panelPixels = 0f;
            app.overrides.virtualHazards.Clear();
            for (int ph = 0; ph < 4; ph++)
                for (int we = 0; we < 6; we++)
                {
                    app.overrides.scene = k; app.overrides.phase = ph; app.overrides.weather = we;
                    app.Apply(c, true);
                    app.world.PrepareCapture();
                    rig.Place();
                    var tex = Render(rig.Cam, tw, th);
                    sheet.SetPixels(we * (tw + gap), (3 - ph) * (th + gap), tw, th, tex.GetPixels());
                    UnityEngine.Object.DestroyImmediate(tex);
                }
            sheet.Apply();
            var path = ShotPath($"matrix_{(SceneKind)k}");
            File.WriteAllBytes(path, sheet.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(sheet);
            return $"saved {path} ({sw.ElapsedMilliseconds} ms) {app.world.Describe()}";
        }

        [CliCommand("ws_hazard_sheet", "Contact sheet of the five virtual disasters (+ none) for one scene: Screenshots/hazards_<scene>.png")]
        public static string HazardSheet([CliArg("scene", "Scene kind")] string scene = "Coast",
                                         [CliArg("phase", "Phase")] string phase = "Evening",
                                         [CliArg("tile", "Tile width (px)")] int tile = 480)
        {
            var app = App(out var err); if (app == null) return err;
            int k = ParseEnum<SceneKind>(scene);
            if (k < 0) return "error: scene?";
            string place = new[] { "서울", "전주", "하회마을", "경주", "설악산", "해운대" }[k];
            var e = Gazetteer.Find(place);
            var c = Resolver.Virtual(Gazetteer.ToPlace(e, Loc.Korean, place), DateTime.UtcNow);
            var kinds = new List<HazardKind?> { null, HazardKind.Volcano, HazardKind.Earthquake, HazardKind.Flood, HazardKind.Tsunami, HazardKind.Wildfire };
            int tw = tile, th = tile * 9 / 16, gap = 4;
            var sheet = new Texture2D(3 * tw + 2 * gap, 2 * th + gap, TextureFormat.RGB24, false);
            var rig = app.world.rig; rig.panelPixels = 0f;
            for (int i = 0; i < kinds.Count; i++)
            {
                app.overrides.scene = k; app.overrides.phase = ParseEnum<DayPhase>(phase); app.overrides.weather = (int)WeatherKind.Cloudy;
                app.overrides.virtualHazards.Clear();
                if (kinds[i].HasValue) app.overrides.virtualHazards.Add(kinds[i].Value);
                app.Apply(c, true);
                app.world.PrepareCapture();
                rig.Place();
                var tex = Render(rig.Cam, tw, th);
                sheet.SetPixels((i % 3) * (tw + gap), (1 - i / 3) * (th + gap), tw, th, tex.GetPixels());
                UnityEngine.Object.DestroyImmediate(tex);
            }
            app.overrides.virtualHazards.Clear();
            sheet.Apply();
            var path = ShotPath($"hazards_{(SceneKind)k}");
            File.WriteAllBytes(path, sheet.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(sheet);
            return $"saved {path} order=none,volcano,earthquake | flood,tsunami,wildfire";
        }
    }
}
