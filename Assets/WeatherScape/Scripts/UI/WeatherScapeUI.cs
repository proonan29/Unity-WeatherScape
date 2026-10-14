using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.UIElements;

namespace WeatherScape.UI
{
    /// <summary>UI Toolkit front end built in code: left panel (search, current weather, display overrides, virtual disasters,
    /// settings), a notice banner for special conditions, and a weather HUD in the lower right.</summary>
    [RequireComponent(typeof(UIDocument))]
    public class WeatherScapeUI : MonoBehaviour
    {
        public AppController app;
        public CameraRig rig;

        public static readonly Color Bg = new Color(0.06f, 0.08f, 0.11f, 0.90f), Card = new Color(0.12f, 0.14f, 0.18f, 0.95f),
            Accent = new Color(0.30f, 0.72f, 0.98f), Text = new Color(0.93f, 0.94f, 0.96f), Dim = new Color(0.62f, 0.66f, 0.72f),
            Btn = new Color(0.20f, 0.23f, 0.28f), BtnOn = new Color(0.14f, 0.50f, 0.80f);
        static readonly Color[] SevColor = { new Color(0.35f, 0.65f, 0.95f), new Color(0.98f, 0.82f, 0.25f), new Color(1f, 0.55f, 0.15f), new Color(1f, 0.25f, 0.25f) };

        const float PanelWidth = 390f;
        VisualElement root, panel, banner, resultsBox, hud;
        Label status, placeName, placeRegion, localTime, sceneLine, phaseLine, weatherBig, details, sourceLine, hudTemp, hudLine;
        TextField search;
        readonly List<(Button b, System.Func<bool> on)> toggles = new List<(Button, System.Func<bool>)>();
        bool built;

        static string F0(float v) => v.ToString("0", CultureInfo.InvariantCulture);
        static string F1(float v) => v.ToString("0.0", CultureInfo.InvariantCulture);
        static string Clock(float minutes) { int m = Mathf.RoundToInt(minutes); return $"{m / 60 % 24:00}:{m % 60:00}"; }

        void OnEnable()
        {
            Loc.Changed += Rebuild;
            if (app != null) { app.Changed += Refresh; app.StatusChanged += Refresh; app.ResultsChanged += RefreshResults; }
            Build();
        }

        void OnDisable()
        {
            Loc.Changed -= Rebuild;
            if (app != null) { app.Changed -= Refresh; app.StatusChanged -= Refresh; app.ResultsChanged -= RefreshResults; }
        }

        void Start()
        {
            if (rig != null) { rig.isOverUI = IsOverPanel; }
            Rebuild();
        }

        void Rebuild() { Build(); RefreshResults(); Refresh(); }

        void Update()
        {
            if (rig != null && panel != null && panel.panel != null)
            {
                // panel width in screen pixels (panel scale may differ from 1)
                float scale = panel.worldBound.width > 0 ? Screen.width / Mathf.Max(1f, root.worldBound.width) : 1f;
                rig.panelPixels = panel.resolvedStyle.display == DisplayStyle.None ? 0f : panel.worldBound.width * scale;
            }
        }

        public bool IsOverPanel(Vector2 screenPos)
        {
            if (panel == null || panel.panel == null) return false;
            var p = RuntimePanelUtils.ScreenToPanel(panel.panel, new Vector2(screenPos.x, Screen.height - screenPos.y));
            return panel.worldBound.Contains(p) || (banner != null && banner.worldBound.Contains(p));
        }

        // ------------------------------------------------------------------ helpers

        static void Pad(VisualElement e, float v) { e.style.paddingLeft = e.style.paddingRight = e.style.paddingTop = e.style.paddingBottom = v; }
        static void Radius(VisualElement e, float r) { e.style.borderTopLeftRadius = e.style.borderTopRightRadius = e.style.borderBottomLeftRadius = e.style.borderBottomRightRadius = r; }

        static Label L(string text, float size = 13, Color? c = null, bool bold = false)
        {
            var l = new Label(text);
            l.style.fontSize = size; l.style.color = c ?? Text; l.style.whiteSpace = WhiteSpace.Normal; l.style.marginBottom = 2;
            if (bold) l.style.unityFontStyleAndWeight = FontStyle.Bold;
            l.enableRichText = true;
            return l;
        }

        static Button B(string text, System.Action onClick, float grow = 1f)
        {
            var b = new Button(onClick) { text = text };
            b.style.flexGrow = grow; b.style.height = 27; b.style.fontSize = 12; b.style.color = Text; b.style.backgroundColor = Btn;
            b.style.borderLeftWidth = b.style.borderRightWidth = b.style.borderTopWidth = b.style.borderBottomWidth = 0;
            Radius(b, 5);
            b.style.marginLeft = b.style.marginRight = 2; b.style.marginBottom = 3;
            b.style.paddingLeft = b.style.paddingRight = 6;
            return b;
        }

        Button Toggle(string text, System.Action onClick, System.Func<bool> on)
        {
            var b = B(text, () => { onClick(); RefreshToggles(); });
            toggles.Add((b, on));
            return b;
        }

        static VisualElement Row(bool wrap = false)
        {
            var r = new VisualElement();
            r.style.flexDirection = FlexDirection.Row; r.style.marginBottom = 2;
            if (wrap) r.style.flexWrap = Wrap.Wrap;
            return r;
        }

        static VisualElement Section(VisualElement parent, string title)
        {
            var c = new VisualElement();
            c.style.backgroundColor = Card; Radius(c, 8); Pad(c, 9); c.style.marginBottom = 8;
            if (!string.IsNullOrEmpty(title)) { var h = L(title, 12, Dim, true); h.style.marginBottom = 5; c.Add(h); }
            parent.Add(c);
            return c;
        }

        // ------------------------------------------------------------------ build

        void Build()
        {
            var doc = GetComponent<UIDocument>();
            if (doc == null || doc.rootVisualElement == null) return;
            root = doc.rootVisualElement;
            root.Clear(); toggles.Clear();
            root.pickingMode = PickingMode.Ignore;

            panel = new VisualElement();
            panel.style.position = Position.Absolute; panel.style.left = 0; panel.style.top = 0; panel.style.bottom = 0;
            panel.style.width = PanelWidth; panel.style.backgroundColor = Bg; Pad(panel, 10);
            root.Add(panel);
            var scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.style.flexGrow = 1;
            panel.Add(scroll);
            var content = scroll.contentContainer;

            var title = L("WeatherScape", 22, Accent, true); title.style.marginBottom = 0; content.Add(title);
            var sub = L(Loc.T("Weather scenes for any place"), 12, Dim); sub.style.marginBottom = 8; content.Add(sub);

            // search
            var sc = Section(content, Loc.T("Place"));
            var row = Row();
            search = new TextField { value = app?.current?.place?.query ?? "" };
            search.style.flexGrow = 1; search.style.fontSize = 13; search.style.marginRight = 2;
            search.textEdition.placeholder = Loc.T("City, landmark, mountain, beach…");
            search.RegisterCallback<KeyDownEvent>(e => { if (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter) DoSearch(); }, TrickleDown.TrickleDown);
            row.Add(search);
            var sb = B(Loc.T("Search"), DoSearch, 0); sb.style.width = 64; sb.style.backgroundColor = BtnOn; row.Add(sb);
            sc.Add(row);
            status = L("", 12, Accent); sc.Add(status);
            resultsBox = new VisualElement(); sc.Add(resultsBox);
            sc.Add(L(Loc.T("Quick picks"), 11, Dim));
            var qp = Row(true);
            foreach (var q in Gazetteer.QuickPicks)
            {
                var e = Gazetteer.Find(q);
                string label = e != null ? (Loc.Korean ? e.ko : e.en) : q;
                var b = B(label, () => { search.value = label; if (e != null) app?.Select(Gazetteer.ToPlace(e, Loc.Korean, q)); else app?.Search(q); }, 0);
                b.style.height = 24; b.style.fontSize = 11;
                qp.Add(b);
            }
            sc.Add(qp);

            // current conditions
            var cc = Section(content, Loc.T("Now"));
            placeName = L("", 19, Text, true); cc.Add(placeName);
            placeRegion = L("", 12, Dim); cc.Add(placeRegion);
            localTime = L("", 13, Text); cc.Add(localTime);
            weatherBig = L("", 17, Accent, true); weatherBig.style.marginTop = 4; cc.Add(weatherBig);
            details = L("", 12, Text); cc.Add(details);
            sceneLine = L("", 12, Text); sceneLine.style.marginTop = 4; cc.Add(sceneLine);
            phaseLine = L("", 12, Text); cc.Add(phaseLine);
            sourceLine = L("", 10, Dim); sourceLine.style.marginTop = 3; cc.Add(sourceLine);

            // data mode
            var dm = Section(content, Loc.T("Data"));
            row = Row();
            row.Add(Toggle(Loc.T("Live"), () => app?.SetMode(DataMode.Live), () => app != null && app.mode == DataMode.Live));
            row.Add(Toggle(Loc.T("Virtual"), () => app?.SetMode(DataMode.Virtual), () => app != null && app.mode == DataMode.Virtual));
            row.Add(B(Loc.T("Refresh"), () => app?.Refresh()));
            dm.Add(row);

            // overrides
            var ov = Section(content, Loc.T("Display"));
            ov.Add(L(Loc.T("Scene"), 11, Dim));
            var r1 = Row(true);
            r1.Add(Toggle(Loc.T("Auto"), () => app?.SetScene(-1), () => app != null && app.overrides.scene < 0));
            for (int i = 0; i < 6; i++) { int k = i; r1.Add(Toggle(Loc.Scene((SceneKind)k), () => app?.SetScene(k), () => app != null && app.overrides.scene == k)); }
            ov.Add(r1);
            ov.Add(L(Loc.T("Time of day"), 11, Dim));
            var r2 = Row(true);
            r2.Add(Toggle(Loc.T("Auto"), () => app?.SetPhase(-1), () => app != null && app.overrides.phase < 0));
            for (int i = 0; i < 4; i++) { int k = i; r2.Add(Toggle(Loc.Phase((DayPhase)k), () => app?.SetPhase(k), () => app != null && app.overrides.phase == k)); }
            ov.Add(r2);
            ov.Add(L(Loc.T("Weather"), 11, Dim));
            var r3 = Row(true);
            r3.Add(Toggle(Loc.T("Auto"), () => app?.SetWeather(-1), () => app != null && app.overrides.weather < 0));
            for (int i = 0; i < 6; i++) { int k = i; r3.Add(Toggle(Loc.Weather((WeatherKind)k), () => app?.SetWeather(k), () => app != null && app.overrides.weather == k)); }
            ov.Add(r3);
            ov.Add(L(Loc.T("Virtual disaster"), 11, Dim));
            var r4 = Row(true);
            foreach (var hk in Net.HazardService.VirtualKinds)
            {
                var kk = hk;
                r4.Add(Toggle(Loc.Hazard(kk), () => app?.ToggleVirtualHazard(kk), () => app != null && app.overrides.virtualHazards.Contains(kk)));
            }
            ov.Add(r4);
            ov.Add(B(Loc.T("All auto"), () => { app?.ClearOverrides(); RefreshToggles(); }));

            // settings
            var st = Section(content, Loc.T("Settings"));
            row = Row();
            row.Add(L(Loc.T("Language"), 12, Dim)); row[0].style.width = 80;
            row.Add(Toggle("한국어", () => Loc.Korean = true, () => Loc.Korean));
            row.Add(Toggle("English", () => Loc.Korean = false, () => !Loc.Korean));
            st.Add(row);
            row = Row();
            row.Add(L(Loc.T("Sound"), 12, Dim)); row[0].style.width = 80;
            row.Add(Toggle(Loc.T("On"), () => { if (app?.world != null) app.world.Muted = false; }, () => app?.world != null && !app.world.Muted));
            row.Add(Toggle(Loc.T("Off"), () => { if (app?.world != null) app.world.Muted = true; }, () => app?.world == null || app.world.Muted));
            st.Add(row);
            row = Row();
            row.Add(L(Loc.T("Auto orbit"), 12, Dim)); row[0].style.width = 80;
            row.Add(Toggle(Loc.T("On"), () => { if (rig != null) rig.autoOrbit = true; }, () => rig != null && rig.autoOrbit));
            row.Add(Toggle(Loc.T("Off"), () => { if (rig != null) rig.autoOrbit = false; }, () => rig == null || !rig.autoOrbit));
            st.Add(row);
            content.Add(L(Loc.T("Drag to rotate · wheel to zoom"), 10, Dim));
            content.Add(L(Loc.T("Data: Open-Meteo, OpenStreetMap Nominatim, USGS, GDACS, GloFAS"), 10, Dim));

            // notice banner (top, right of the panel)
            banner = new VisualElement { pickingMode = PickingMode.Ignore };
            banner.style.position = Position.Absolute; banner.style.top = 12; banner.style.left = PanelWidth + 16; banner.style.right = 16;
            banner.style.alignItems = Align.FlexStart;
            root.Add(banner);

            // HUD (bottom right)
            hud = new VisualElement { pickingMode = PickingMode.Ignore };
            hud.style.position = Position.Absolute; hud.style.right = 22; hud.style.bottom = 18; hud.style.alignItems = Align.FlexEnd;
            hudTemp = L("", 46, Text, true); hudTemp.style.marginBottom = -6;
            hudLine = L("", 16, Text);
            foreach (var l in new[] { hudTemp, hudLine }) { l.style.unityTextAlign = TextAnchor.MiddleRight; l.style.textShadow = new TextShadow { offset = new Vector2(1, 1), blurRadius = 3, color = new Color(0, 0, 0, 0.8f) }; }
            hud.Add(hudTemp); hud.Add(hudLine);
            root.Add(hud);
            built = true;
        }

        void DoSearch() { if (app != null && search != null && !string.IsNullOrWhiteSpace(search.value)) app.Search(search.value); }

        void RefreshToggles()
        {
            foreach (var (b, on) in toggles) b.style.backgroundColor = on() ? BtnOn : Btn;
        }

        void RefreshResults()
        {
            if (resultsBox == null || app == null) return;
            resultsBox.Clear();
            if (app.results.Count <= 1) return;
            resultsBox.Add(L(Loc.T("Results"), 11, Dim));
            foreach (var p in app.results)
            {
                var pp = p;
                string txt = $"{p.name}  <color=#9aa3ad><size=10>{p.region} · {p.source}</size></color>";
                var b = B("", () => app.Select(pp));
                b.text = txt; b.enableRichText = true;
                b.style.unityTextAlign = TextAnchor.MiddleLeft; b.style.height = StyleKeyword.Auto; b.style.minHeight = 24; b.style.whiteSpace = WhiteSpace.Normal;
                resultsBox.Add(b);
            }
        }

        void Refresh()
        {
            if (!built || app == null) return;
            RefreshToggles();
            status.text = Loc.T(app.Status);
            var c = app.current;
            if (c == null || c.place == null) return;
            var p = c.place; var w = c.weather;
            placeName.text = p.name;
            placeRegion.text = p.region;
            if (w != null)
            {
                localTime.text = $"{Loc.T("local time")} {w.localTimeText} (UTC{(w.utcOffsetSeconds >= 0 ? "+" : "-")}{Mathf.Abs(w.utcOffsetSeconds) / 3600}{(Mathf.Abs(w.utcOffsetSeconds) % 3600 != 0 ? ":" + (Mathf.Abs(w.utcOffsetSeconds) % 3600 / 60).ToString("00") : "")})";
                string extra = (c.thunder && c.autoWeather != WeatherKind.Typhoon ? " · " + Loc.T("thunder") : "") + (c.fog ? " · " + Loc.T("fog") : "");
                weatherBig.text = $"{Loc.Weather(c.autoWeather)}{extra}  {F1(w.temperature)}°C";
                var lines = new List<string>
                {
                    Loc.F("Feels like {0}", F1(w.apparent) + "°C") + " · " + Loc.F("Humidity {0}%", F0(w.humidity)),
                    Loc.F("Wind {0} m/s {1} (gust {2})", F1(w.windSpeed), Loc.Compass(w.windDir), F1(w.windGust)),
                    Loc.F("Cloud {0}%", F0(w.cloudCover)) + " · " + Loc.F("Visibility {0} km", F1(w.visibility / 1000f)),
                };
                if (w.precipitation > 0f) lines.Add(Loc.F("Precipitation {0} mm/h", F1(w.precipitation)));
                if (w.snowfall > 0f) lines.Add(Loc.F("Snowfall {0} cm/h", F1(w.snowfall)));
                if (!float.IsNaN(w.waveHeight)) lines.Add(Loc.F("Waves {0} m", F1(w.waveHeight)));
                lines.Add(Loc.F("Sunrise {0} · Sunset {1}", Clock(w.sunriseMinutes), Clock(w.sunsetMinutes)));
                details.text = string.Join("\n", lines);
                string src = c.mode == DataMode.Virtual ? Loc.T("virtual") : w.source;
                sourceLine.text = Loc.F("Source: {0} · updated {1}", $"{src} / {p.source}", w.fetchedUtc == default ? "-" : w.fetchedUtc.ToLocalTime().ToString("HH:mm"));
            }
            string reason = Reason(p.sceneReason);
            sceneLine.text = $"{Loc.T("Scene")}: <b>{Loc.Scene(c.scene)}</b> " + (app.overrides.scene >= 0 ? Loc.T("(override)") : $"<color=#9aa3ad>({Loc.F("auto: {0}", reason)})</color>");
            phaseLine.text = $"{Loc.T("Showing")}: <b>{Loc.Phase(c.phase)}</b> · <b>{Loc.Weather(c.weatherKind)}</b>" +
                             (app.overrides.phase >= 0 || app.overrides.weather >= 0 ? " " + Loc.T("(override)") : "") +
                             $" · {Loc.T(Effects.SceneAnimator.SeasonFor(c).ToString())}";
            if (search != null && string.IsNullOrEmpty(search.value)) search.value = p.query ?? p.name;

            // banner
            banner.Clear();
            if (c.hazards.Count == 0) banner.Add(Chip(Loc.T("No special notices"), new Color(0.3f, 0.75f, 0.45f)));
            int shown = 0;
            foreach (var h in c.hazards)
            {
                if (shown++ >= 6) break;
                var sevName = Loc.T(h.severity.ToString());
                string txt = $"<b>[{sevName}] {Title(h)}</b>";
                if (h.magnitude > 0) txt += $" · M{F1(h.magnitude)}";
                if (h.distanceKm > 0.5f) txt += $" · {F0(h.distanceKm)} km";
                string det = Detail(h);
                if (!string.IsNullOrEmpty(det)) txt += $" · {det}";
                txt += h.isVirtual ? $" <color=#ffd27a>({Loc.T("virtual")})</color>" : $" <color=#c8ced6><size=11>({h.source})</size></color>";
                banner.Add(Chip(txt, SevColor[(int)h.severity]));
            }

            // HUD
            hudTemp.text = w != null ? $"{F0(w.temperature)}°" : "";
            hudLine.text = $"{p.name} · {(w != null ? w.localTimeText.Substring(Mathf.Max(0, w.localTimeText.Length - 5)) : "")} · {Loc.Phase(c.phase)} · {Loc.Weather(c.weatherKind)}";
        }

        static string Title(Hazard h)
        {
            string t = Loc.T(h.title);
            return t == h.title && Loc.Korean ? Loc.Hazard(h.kind) : t;
        }

        static string Detail(Hazard h)
        {
            if (string.IsNullOrEmpty(h.detail)) return "";
            if (h.detail.Contains("|")) { var parts = h.detail.Split('|'); return Loc.Korean ? parts[0] : parts[1]; }
            return h.detail;
        }

        static string Reason(string r)
        {
            if (string.IsNullOrEmpty(r)) return "";
            if (r.StartsWith("population ") && Loc.Korean)
            {
                var num = r.Split(' ')[1];
                if (r.Contains("≥ 1,000,000")) return Loc.F("population {0} ≥ 1,000,000", num);
                if (r.Contains("≥ 50,000")) return Loc.F("population {0} ≥ 50,000", num);
                if (r.Contains("< 50,000")) return Loc.F("population {0} < 50,000", num);
            }
            return Loc.T(r);
        }

        static VisualElement Chip(string text, Color sev)
        {
            var c = new VisualElement { pickingMode = PickingMode.Ignore };
            c.style.flexDirection = FlexDirection.Row; c.style.alignItems = Align.Center;
            c.style.backgroundColor = new Color(0.05f, 0.06f, 0.08f, 0.82f); Radius(c, 6);
            c.style.borderLeftWidth = 5; c.style.borderLeftColor = sev;
            c.style.paddingLeft = 10; c.style.paddingRight = 12; c.style.paddingTop = 5; c.style.paddingBottom = 5; c.style.marginBottom = 5;
            var l = L(text, 14, Text); l.style.marginBottom = 0; l.pickingMode = PickingMode.Ignore;
            c.Add(l);
            return c;
        }
    }
}
