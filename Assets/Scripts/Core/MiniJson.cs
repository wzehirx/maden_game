using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Maden.Core
{
    /// <summary>
    /// Küçük, bağımlılıksız JSON okuyucu/yazıcı. Okuma: Dictionary&lt;string,object&gt;, List&lt;object&gt;,
    /// double, string, bool, null. Hatalı girdide FormatException fırlatır.
    /// </summary>
    public static class MiniJson
    {
        public static object Parse(string json)
        {
            if (json == null) throw new FormatException("JSON boş.");
            var p = new Parser(json);
            p.SkipWs();
            object v = p.ReadValue(0);
            p.SkipWs();
            if (!p.End) throw new FormatException("JSON sonunda fazla veri var.");
            return v;
        }

        public static string Serialize(object value, bool pretty = false)
        {
            var sb = new StringBuilder();
            Write(sb, value, pretty, 0);
            return sb.ToString();
        }

        // ---- Erişim yardımcıları ----
        public static Dictionary<string, object> AsObject(object o)
        {
            return o as Dictionary<string, object>;
        }

        public static List<object> AsArray(object o)
        {
            return o as List<object>;
        }

        public static double GetDouble(Dictionary<string, object> d, string key, double def)
        {
            object o;
            if (d != null && d.TryGetValue(key, out o) && o is double)
            {
                double v = (double)o;
                if (double.IsNaN(v) || double.IsInfinity(v)) return def;
                return v;
            }
            return def;
        }

        public static long GetLong(Dictionary<string, object> d, string key, long def)
        {
            double v = GetDouble(d, key, double.NaN);
            if (double.IsNaN(v)) return def;
            if (v > 9.0e15) return 9000000000000000L;
            if (v < -9.0e15) return -9000000000000000L;
            return (long)v;
        }

        public static int GetInt(Dictionary<string, object> d, string key, int def)
        {
            long v = GetLong(d, key, def);
            if (v > int.MaxValue) return int.MaxValue;
            if (v < int.MinValue) return int.MinValue;
            return (int)v;
        }

        public static string GetString(Dictionary<string, object> d, string key, string def)
        {
            object o;
            if (d != null && d.TryGetValue(key, out o) && o is string) return (string)o;
            return def;
        }

        public static bool GetBool(Dictionary<string, object> d, string key, bool def)
        {
            object o;
            if (d != null && d.TryGetValue(key, out o) && o is bool) return (bool)o;
            return def;
        }

        public static Dictionary<string, object> GetObject(Dictionary<string, object> d, string key)
        {
            object o;
            if (d != null && d.TryGetValue(key, out o)) return o as Dictionary<string, object>;
            return null;
        }

        public static List<object> GetArray(Dictionary<string, object> d, string key)
        {
            object o;
            if (d != null && d.TryGetValue(key, out o)) return o as List<object>;
            return null;
        }

        // ---- Yazıcı ----
        private static void Write(StringBuilder sb, object v, bool pretty, int depth)
        {
            if (v == null) { sb.Append("null"); return; }
            if (v is string) { WriteString(sb, (string)v); return; }
            if (v is bool) { sb.Append((bool)v ? "true" : "false"); return; }
            if (v is double || v is float)
            {
                double d = Convert.ToDouble(v, CultureInfo.InvariantCulture);
                if (double.IsNaN(d) || double.IsInfinity(d)) d = 0;
                sb.Append(d.ToString("R", CultureInfo.InvariantCulture));
                return;
            }
            if (v is int || v is long || v is short || v is byte || v is uint || v is ulong || v is ushort)
            {
                sb.Append(Convert.ToString(v, CultureInfo.InvariantCulture));
                return;
            }
            var dict = v as IDictionary;
            if (dict != null)
            {
                sb.Append('{');
                bool first = true;
                foreach (DictionaryEntry e in dict)
                {
                    if (!first) sb.Append(',');
                    first = false;
                    NewLine(sb, pretty, depth + 1);
                    WriteString(sb, Convert.ToString(e.Key, CultureInfo.InvariantCulture));
                    sb.Append(pretty ? ": " : ":");
                    Write(sb, e.Value, pretty, depth + 1);
                }
                if (!first) NewLine(sb, pretty, depth);
                sb.Append('}');
                return;
            }
            var list = v as IEnumerable;
            if (list != null)
            {
                sb.Append('[');
                bool first = true;
                foreach (object o in list)
                {
                    if (!first) sb.Append(',');
                    first = false;
                    NewLine(sb, pretty, depth + 1);
                    Write(sb, o, pretty, depth + 1);
                }
                if (!first) NewLine(sb, pretty, depth);
                sb.Append(']');
                return;
            }
            throw new FormatException("Serileştirilemeyen tür: " + v.GetType().Name);
        }

        private static void NewLine(StringBuilder sb, bool pretty, int depth)
        {
            if (!pretty) return;
            sb.Append('\n');
            sb.Append(' ', depth * 2);
        }

        private static void WriteString(StringBuilder sb, string s)
        {
            sb.Append('"');
            foreach (char c in s)
            {
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (c < 0x20) sb.Append("\\u").Append(((int)c).ToString("x4"));
                        else sb.Append(c);
                        break;
                }
            }
            sb.Append('"');
        }

        // ---- Okuyucu ----
        private sealed class Parser
        {
            private readonly string s;
            private int i;
            public Parser(string text) { s = text; i = 0; if (s.Length > 0 && s[0] == '﻿') i = 1; }
            public bool End { get { return i >= s.Length; } }

            public void SkipWs()
            {
                while (i < s.Length && (s[i] == ' ' || s[i] == '\t' || s[i] == '\n' || s[i] == '\r')) i++;
            }

            public object ReadValue(int depth)
            {
                if (depth > 64) throw new FormatException("JSON çok derin.");
                if (i >= s.Length) throw new FormatException("Beklenmeyen son.");
                char c = s[i];
                if (c == '{') return ReadObject(depth);
                if (c == '[') return ReadArray(depth);
                if (c == '"') return ReadString();
                if (c == 't') { Expect("true"); return true; }
                if (c == 'f') { Expect("false"); return false; }
                if (c == 'n') { Expect("null"); return null; }
                return ReadNumber();
            }

            private void Expect(string word)
            {
                if (string.CompareOrdinal(s, i, word, 0, word.Length) != 0)
                    throw new FormatException("Geçersiz sözcük, konum " + i);
                i += word.Length;
            }

            private object ReadObject(int depth)
            {
                var d = new Dictionary<string, object>();
                i++; SkipWs();
                if (i < s.Length && s[i] == '}') { i++; return d; }
                while (true)
                {
                    SkipWs();
                    if (i >= s.Length || s[i] != '"') throw new FormatException("Anahtar bekleniyordu, konum " + i);
                    string key = ReadString();
                    SkipWs();
                    if (i >= s.Length || s[i] != ':') throw new FormatException("':' bekleniyordu, konum " + i);
                    i++; SkipWs();
                    d[key] = ReadValue(depth + 1);
                    SkipWs();
                    if (i >= s.Length) throw new FormatException("Nesne kapanmadı.");
                    if (s[i] == ',') { i++; continue; }
                    if (s[i] == '}') { i++; return d; }
                    throw new FormatException("Geçersiz karakter, konum " + i);
                }
            }

            private object ReadArray(int depth)
            {
                var l = new List<object>();
                i++; SkipWs();
                if (i < s.Length && s[i] == ']') { i++; return l; }
                while (true)
                {
                    SkipWs();
                    l.Add(ReadValue(depth + 1));
                    SkipWs();
                    if (i >= s.Length) throw new FormatException("Dizi kapanmadı.");
                    if (s[i] == ',') { i++; continue; }
                    if (s[i] == ']') { i++; return l; }
                    throw new FormatException("Geçersiz karakter, konum " + i);
                }
            }

            private string ReadString()
            {
                var sb = new StringBuilder();
                i++; // açılış "
                while (true)
                {
                    if (i >= s.Length) throw new FormatException("Metin kapanmadı.");
                    char c = s[i++];
                    if (c == '"') return sb.ToString();
                    if (c == '\\')
                    {
                        if (i >= s.Length) throw new FormatException("Kaçış hatası.");
                        char e = s[i++];
                        switch (e)
                        {
                            case '"': sb.Append('"'); break;
                            case '\\': sb.Append('\\'); break;
                            case '/': sb.Append('/'); break;
                            case 'b': sb.Append('\b'); break;
                            case 'f': sb.Append('\f'); break;
                            case 'n': sb.Append('\n'); break;
                            case 'r': sb.Append('\r'); break;
                            case 't': sb.Append('\t'); break;
                            case 'u':
                                if (i + 4 > s.Length) throw new FormatException("\\u hatası.");
                                int code;
                                if (!int.TryParse(s.Substring(i, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out code))
                                    throw new FormatException("\\u hatası.");
                                sb.Append((char)code);
                                i += 4;
                                break;
                            default: throw new FormatException("Bilinmeyen kaçış: \\" + e);
                        }
                    }
                    else sb.Append(c);
                }
            }

            private object ReadNumber()
            {
                int start = i;
                while (i < s.Length && "+-0123456789.eE".IndexOf(s[i]) >= 0) i++;
                if (start == i) throw new FormatException("Beklenmeyen karakter, konum " + i);
                double d;
                if (!double.TryParse(s.Substring(start, i - start), NumberStyles.Float, CultureInfo.InvariantCulture, out d))
                    throw new FormatException("Geçersiz sayı, konum " + start);
                return d;
            }
        }
    }
}
