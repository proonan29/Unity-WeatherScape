using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using WeatherScape.Net;

namespace WeatherScape
{
    /// <summary>App state: place search, live/virtual data, user overrides (scene, time of day, weather, virtual hazards),
    /// periodic refresh. The last conditions are serialized so the editor shows the same scene when the scene is opened.</summary>
    [ExecuteAlways]
    public class AppController : MonoBehaviour
    {
        public WeatherWorld world;
        public DataMode mode = DataMode.Live;
        public Overrides overrides = new Overrides();
        public Conditions current;
        [Tooltip("Live data refresh interval (minutes)")] public float refreshMinutes = 10f;
        public string startPlace = "서울";

        public event Action Changed, StatusChanged, ResultsChanged;
        public string Status { get; private set; } = "";
        public bool Busy { get; private set; }
        public readonly List<PlaceInfo> results = new List<PlaceInfo>();
        public bool Korean => UI.Loc.Korean;

        CancellationTokenSource cts;
        float nextRefresh;

        void OnEnable()
        {
            if (current != null && current.place != null && world != null)
            {
                Resolver.Resolve(current, overrides);
                world.Show(current, true);
            }
        }

        void Start()
        {
            if (!Application.isPlaying) return;
            string last = PlayerPrefs.GetString("ws_last_place", "");
            var e = Gazetteer.Find(string.IsNullOrEmpty(last) ? startPlace : last);
            if (current != null && current.place != null && current.place.name == last) Select(current.place);
            else if (e != null) Select(Gazetteer.ToPlace(e, Korean));
            else Search(last);
        }

        void Update()
        {
            if (!Application.isPlaying || Busy || current?.place == null) return;
            if (Time.unscaledTime >= nextRefresh) { nextRefresh = Time.unscaledTime + refreshMinutes * 60f; if (Time.unscaledTime > 5f) Refresh(); }
        }

        void SetStatus(string s) { Status = s; StatusChanged?.Invoke(); }

        // ------------------------------------------------------------ search / select

        public async void Search(string query)
        {
            if (string.IsNullOrWhiteSpace(query)) return;
            query = query.Trim();
            Cancel();
            var token = cts.Token;
            Busy = true; SetStatus("Searching…");
            results.Clear();
            try
            {
                if (mode == DataMode.Virtual)
                {
                    var e = Gazetteer.Find(query);
                    results.Add(e != null ? Gazetteer.ToPlace(e, Korean, query) : VirtualWeather.FakePlace(query, Korean));
                }
                else results.AddRange(await Geocoder.Search(query, Korean, token));
                if (token.IsCancellationRequested) return;
                ResultsChanged?.Invoke();
                if (results.Count == 0) { Busy = false; SetStatus("No place found"); return; }
                Busy = false;
                Select(results[0]);
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                Debug.LogWarning($"[WeatherScape] search failed: {ex.Message}");
                var e = Gazetteer.Find(query);
                results.Clear();
                results.Add(e != null ? Gazetteer.ToPlace(e, Korean, query) : VirtualWeather.FakePlace(query, Korean));
                ResultsChanged?.Invoke();
                Busy = false;
                SetStatus("Offline – using virtual data");
                ShowVirtual(results[0]);
            }
            finally { if (!token.IsCancellationRequested) Busy = false; }
        }

        public async void Select(PlaceInfo p)
        {
            if (p == null) return;
            Cancel();
            var token = cts.Token;
            PlayerPrefs.SetString("ws_last_place", p.name ?? p.query ?? "");
            if (mode == DataMode.Virtual) { ShowVirtual(p); return; }
            Busy = true; SetStatus("Loading weather…");
            try
            {
                var c = await Resolver.Live(p, Korean, token);
                if (token.IsCancellationRequested) return;
                Apply(c, false);
                nextRefresh = Time.unscaledTime + refreshMinutes * 60f;
                SetStatus("");
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                Debug.LogWarning($"[WeatherScape] live data failed: {ex.Message}");
                SetStatus("Offline – using virtual data");
                ShowVirtual(p);
            }
            finally { if (!token.IsCancellationRequested) Busy = false; }
        }

        public void Refresh()
        {
            if (current?.place == null) return;
            if (mode == DataMode.Virtual) ShowVirtual(current.place);
            else Select(current.place);
        }

        void ShowVirtual(PlaceInfo p)
        {
            var c = Resolver.Virtual(p, DateTime.UtcNow);
            Apply(c, false);
            if (mode == DataMode.Virtual) SetStatus("");
        }

        void Cancel()
        {
            cts?.Cancel();
            cts = new CancellationTokenSource();
        }

        // ------------------------------------------------------------ apply / overrides

        /// <summary>Resolve overrides and show. Used by the UI and by editor commands (synchronously).</summary>
        public void Apply(Conditions c, bool instant)
        {
            current = c;
            Resolver.Resolve(current, overrides);
            if (world != null) world.Show(current, instant || !Application.isPlaying);
            Changed?.Invoke();
        }

        void Reapply()
        {
            if (current == null) return;
            Resolver.Resolve(current, overrides);
            if (world != null) world.Show(current, !Application.isPlaying);
            Changed?.Invoke();
        }

        public void SetScene(int k) { overrides.scene = k; Reapply(); }
        public void SetPhase(int k) { overrides.phase = k; Reapply(); }
        public void SetWeather(int k) { overrides.weather = k; Reapply(); }

        public void ToggleVirtualHazard(HazardKind k)
        {
            if (!overrides.virtualHazards.Remove(k)) overrides.virtualHazards.Add(k);
            Reapply();
        }

        public void ClearOverrides()
        {
            overrides.scene = overrides.phase = overrides.weather = -1;
            overrides.virtualHazards.Clear();
            Reapply();
        }

        public void SetMode(DataMode m)
        {
            if (mode == m) return;
            mode = m;
            Refresh();
        }
    }
}
