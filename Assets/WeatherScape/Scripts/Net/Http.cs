using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace WeatherScape.Net
{
    /// <summary>Shared HttpClient. Every await uses ConfigureAwait(false) so editor commands can block on the task safely.</summary>
    public static class Http
    {
        public const string UserAgent = "WeatherScape/0.1 (Unity weather scene demo; https://github.com/proonan29/Unity-WeatherScape)";
        static HttpClient client;
        static readonly SemaphoreSlim nominatimGate = new SemaphoreSlim(1, 1);
        static DateTime lastNominatim = DateTime.MinValue;

        static HttpClient Client
        {
            get
            {
                if (client != null) return client;
                client = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
                client.DefaultRequestHeaders.UserAgent.ParseAdd(UserAgent);
                client.DefaultRequestHeaders.AcceptLanguage.ParseAdd("ko,en;q=0.8");
                return client;
            }
        }

        public static async Task<string> Get(string url, CancellationToken ct = default)
        {
            using (var resp = await Client.GetAsync(url, ct).ConfigureAwait(false))
            {
                resp.EnsureSuccessStatusCode();
                var bytes = await resp.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
                return System.Text.Encoding.UTF8.GetString(bytes);
            }
        }

        public static async Task<object> GetJson(string url, CancellationToken ct = default)
        {
            var s = await Get(url, ct).ConfigureAwait(false);
            return Json.Parse(s);
        }

        /// <summary>Nominatim usage policy: at most one request per second, identifying User-Agent.</summary>
        public static async Task<object> GetNominatim(string url, CancellationToken ct = default)
        {
            await nominatimGate.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                var wait = lastNominatim.AddMilliseconds(1100) - DateTime.UtcNow;
                if (wait > TimeSpan.Zero) await Task.Delay(wait, ct).ConfigureAwait(false);
                lastNominatim = DateTime.UtcNow;
                return await GetJson(url, ct).ConfigureAwait(false);
            }
            finally { nominatimGate.Release(); }
        }

        public static string Esc(string s) => Uri.EscapeDataString(s ?? "");
        public static string D(double v) => v.ToString("0.#####", System.Globalization.CultureInfo.InvariantCulture);
    }
}
