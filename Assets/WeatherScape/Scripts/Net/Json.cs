using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace WeatherScape.Net
{
    /// <summary>Small JSON reader: objects → Dictionary, arrays → List, numbers → double.
    /// Used because the APIs return shapes JsonUtility cannot read (nulls, mixed geometry arrays).</summary>
    public static class Json
    {
        public static object Parse(string s)
        {
            if (string.IsNullOrEmpty(s)) return null;
            int i = 0;
            var v = Value(s, ref i);
            return v;
        }

        static void Ws(string s, ref int i) { while (i < s.Length && char.IsWhiteSpace(s[i])) i++; }

        static object Value(string s, ref int i)
        {
            Ws(s, ref i);
            if (i >= s.Length) return null;
            char c = s[i];
            if (c == '{') return Obj(s, ref i);
            if (c == '[') return Arr(s, ref i);
            if (c == '"') return Str(s, ref i);
            if (c == 't' && string.CompareOrdinal(s, i, "true", 0, 4) == 0) { i += 4; return true; }
            if (c == 'f' && string.CompareOrdinal(s, i, "false", 0, 5) == 0) { i += 5; return false; }
            if (c == 'n' && string.CompareOrdinal(s, i, "null", 0, 4) == 0) { i += 4; return null; }
            return Num(s, ref i);
        }

        static Dictionary<string, object> Obj(string s, ref int i)
        {
            var d = new Dictionary<string, object>();
            i++;
            while (true)
            {
                Ws(s, ref i);
                if (i >= s.Length) break;
                if (s[i] == '}') { i++; break; }
                if (s[i] == ',') { i++; continue; }
                string k = Str(s, ref i);
                Ws(s, ref i);
                if (i < s.Length && s[i] == ':') i++;
                d[k] = Value(s, ref i);
            }
            return d;
        }

        static List<object> Arr(string s, ref int i)
        {
            var l = new List<object>();
            i++;
            while (true)
            {
                Ws(s, ref i);
                if (i >= s.Length) break;
                if (s[i] == ']') { i++; break; }
                if (s[i] == ',') { i++; continue; }
                l.Add(Value(s, ref i));
            }
            return l;
        }

        static string Str(string s, ref int i)
        {
            var sb = new StringBuilder();
            i++; // opening quote
            while (i < s.Length)
            {
                char c = s[i++];
                if (c == '"') break;
                if (c != '\\') { sb.Append(c); continue; }
                if (i >= s.Length) break;
                char e = s[i++];
                switch (e)
                {
                    case 'n': sb.Append('\n'); break;
                    case 't': sb.Append('\t'); break;
                    case 'r': sb.Append('\r'); break;
                    case 'b': sb.Append('\b'); break;
                    case 'f': sb.Append('\f'); break;
                    case 'u':
                        if (i + 4 <= s.Length) { sb.Append((char)Convert.ToInt32(s.Substring(i, 4), 16)); i += 4; }
                        break;
                    default: sb.Append(e); break;
                }
            }
            return sb.ToString();
        }

        static object Num(string s, ref int i)
        {
            int st = i;
            while (i < s.Length && "+-0123456789.eE".IndexOf(s[i]) >= 0) i++;
            if (i == st) { i++; return null; }
            double.TryParse(s.Substring(st, i - st), NumberStyles.Float, CultureInfo.InvariantCulture, out var d);
            return d;
        }

        // ---------- access helpers ----------

        public static object Get(object o, params object[] path)
        {
            foreach (var p in path)
            {
                if (o == null) return null;
                if (p is string key) { if (o is Dictionary<string, object> d && d.TryGetValue(key, out var v)) o = v; else return null; }
                else if (p is int idx) { if (o is List<object> l && idx >= 0 && idx < l.Count) o = l[idx]; else return null; }
            }
            return o;
        }

        public static double Num(object o, double fallback = double.NaN)
        {
            if (o is double d) return d;
            if (o is string s && double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var v)) return v;
            if (o is bool b) return b ? 1 : 0;
            return fallback;
        }

        public static float F(object root, params object[] path) => (float)Num(Get(root, path));
        public static string S(object root, params object[] path) { var v = Get(root, path); return v == null ? null : v as string ?? Convert.ToString(v, CultureInfo.InvariantCulture); }
        public static List<object> A(object root, params object[] path) => Get(root, path) as List<object> ?? new List<object>();
    }
}
