using System;
using System.Collections.Generic;
using System.Text.Json;
using Godot;

namespace Maden.Game
{
    public sealed class VoiceInfo
    {
        /// <summary>Android köprüsünde ses adı; DisplayServer yedeğinde Godot voice id.</summary>
        public string Id = "", Name = "", Language = "";
        public int Quality;
        public bool Network;
        public bool Installed = true;
        public bool IsTurkish;
    }

    public sealed class TtsEngineInfo
    {
        public string Package = "", Label = "";
        public bool IsDefault;
    }

    public sealed class TtsStatus
    {
        /// <summary>none | initializing | ready | error</summary>
        public string State = "none";
        public string Engine = "", LastError = "", AudioUsage = "";
        public long ElapsedMs;
        public bool Speaking;
        public int Queued;
        public int LangResult = 100, VoiceResult = 100;
    }

    /// <summary>
    /// Android TTS köprüsü sarmalayıcısı (tools/android_tts, plugin v2 "MadenTts").
    /// Kurulu HER metin okuma motorunu listeler/seçtirir. Köprü yoksa (masaüstü, eski APK) Godot'nun
    /// DisplayServer.Tts* çağrılarına düşer: motor listesi boştur, ses listesi DisplayServer'dan gelir.
    /// </summary>
    public static class AndroidTts
    {
        public const string PluginName = "MadenTts";
        /// <summary>Java tarafında SetLanguage/SetVoice: motor hazır değil, istek saklandı.</summary>
        public const int Pending = 100;

        static GodotObject _plugin;
        static bool _probed;
        static string _lastError = "";
        static string _fallbackVoiceId = "";

        /// <summary>Köprü eklentisi yüklü mü (yalnızca Android APK'sında).</summary>
        public static bool HasPlugin
        {
            get
            {
                if (_probed) return _plugin != null;
                _probed = true;
                try
                {
                    if (OS.GetName() == "Android" && Engine.HasSingleton(PluginName))
                        _plugin = Engine.GetSingleton(PluginName);
                }
                catch (Exception e) { _lastError = "Köprü bulunamadı: " + e.Message; }
                return _plugin != null;
            }
        }

        /// <summary>Son hata metni (köprü + sarmalayıcı).</summary>
        public static string LastError
        {
            get
            {
                string j = "";
                if (HasPlugin) { try { j = Call("GetLastError").AsString(); } catch { } }
                return string.IsNullOrEmpty(j) ? _lastError : j;
            }
        }

        static Variant Call(string method, params Variant[] args)
        {
            try { return _plugin.Call(method, args); }
            catch (Exception e) { _lastError = method + " çağrısı başarısız: " + e.Message; return default; }
        }

        static JsonElement? ParseJson(string json)
        {
            if (string.IsNullOrEmpty(json)) return null;
            try { return JsonDocument.Parse(json).RootElement.Clone(); }
            catch (Exception e) { _lastError = "JSON okunamadı: " + e.Message; return null; }
        }

        static string Str(JsonElement e, string k) => e.TryGetProperty(k, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() ?? "" : "";
        static int Int(JsonElement e, string k, int d = 0) => e.TryGetProperty(k, out var v) && v.ValueKind == JsonValueKind.Number ? v.GetInt32() : d;
        static long Long(JsonElement e, string k) => e.TryGetProperty(k, out var v) && v.ValueKind == JsonValueKind.Number ? v.GetInt64() : 0;
        static bool Bool(JsonElement e, string k, bool d = false) => e.TryGetProperty(k, out var v) && (v.ValueKind == JsonValueKind.True || v.ValueKind == JsonValueKind.False) ? v.GetBoolean() : d;

        // ---------- Motorlar ----------

        /// <summary>Kurulu metin okuma motorları (köprü yoksa boş).</summary>
        public static List<TtsEngineInfo> ListEngines()
        {
            var list = new List<TtsEngineInfo>();
            if (!HasPlugin) return list;
            var root = ParseJson(Call("ListEngines").AsString());
            if (root is JsonElement r && r.ValueKind == JsonValueKind.Array)
                foreach (var e in r.EnumerateArray())
                    list.Add(new TtsEngineInfo { Package = Str(e, "package"), Label = Str(e, "label"), IsDefault = Bool(e, "default") });
            return list;
        }

        /// <summary>Telefonun varsayılan motorunun paket adı ("" = bilinmiyor / köprü yok).</summary>
        public static string GetDefaultEngine() => HasPlugin ? Call("GetDefaultEngine").AsString() : "";

        /// <summary>Belirli motorla başlat ("" = varsayılan). Asenkron; IsReady/GetStatus ile izle.</summary>
        public static bool Init(string enginePackage)
        {
            if (!HasPlugin) return true;
            return Call("Init", enginePackage ?? "").AsBool();
        }

        public static bool IsReady() => HasPlugin ? Call("IsReady").AsBool() : true;

        public static TtsStatus GetStatus()
        {
            var st = new TtsStatus();
            if (!HasPlugin) { st.State = "ready"; return st; }
            var root = ParseJson(Call("GetStatus").AsString());
            if (root is JsonElement e && e.ValueKind == JsonValueKind.Object)
            {
                st.State = Str(e, "state"); st.Engine = Str(e, "engine"); st.LastError = Str(e, "lastError");
                st.AudioUsage = Str(e, "audioUsage"); st.ElapsedMs = Long(e, "elapsedMs");
                st.Speaking = Bool(e, "speaking"); st.Queued = Int(e, "queued");
                st.LangResult = Int(e, "langResult", Pending); st.VoiceResult = Int(e, "voiceResult", Pending);
            }
            else st.State = "error";
            return st;
        }

        // ---------- Sesler / dil ----------

        /// <summary>Seçili motorun sesleri (köprü yoksa DisplayServer'ın sesleri).</summary>
        public static List<VoiceInfo> ListVoices()
        {
            var list = new List<VoiceInfo>();
            if (HasPlugin)
            {
                var root = ParseJson(Call("ListVoices").AsString());
                if (root is JsonElement r && r.ValueKind == JsonValueKind.Array)
                    foreach (var e in r.EnumerateArray())
                        list.Add(new VoiceInfo
                        {
                            Id = Str(e, "name"), Name = Str(e, "name"), Language = Str(e, "locale"),
                            Quality = Int(e, "quality"), Network = Bool(e, "network"), Installed = Bool(e, "installed", true),
                            IsTurkish = Bool(e, "tr")
                        });
                return list;
            }
            try
            {
                var voices = DisplayServer.TtsGetVoices();
                if (voices != null)
                    foreach (var v in voices)
                    {
                        var vi = new VoiceInfo
                        {
                            Id = v.ContainsKey("id") ? v["id"].AsString() : "",
                            Name = v.ContainsKey("name") ? v["name"].AsString() : "",
                            Language = v.ContainsKey("language") ? v["language"].AsString() : ""
                        };
                        string lang = vi.Language.Replace('_', '-').ToLowerInvariant();
                        vi.IsTurkish = lang == "tr" || lang.StartsWith("tr-") || lang.StartsWith("tur");
                        list.Add(vi);
                    }
                if (!list.Exists(x => x.IsTurkish))
                {
                    var ids = DisplayServer.TtsGetVoicesForLanguage("tr");
                    if (ids != null)
                        foreach (var id in ids)
                        {
                            var found = list.Find(x => x.Id == id);
                            if (found != null) found.IsTurkish = true;
                            else list.Add(new VoiceInfo { Id = id, Name = id, Language = "tr", IsTurkish = true });
                        }
                }
            }
            catch (Exception e) { _lastError = "TtsGetVoices hatası: " + e.Message; }
            return list;
        }

        /// <summary>Ses seç ("" = dilin varsayılanı). Köprü: 0 tamam, -2 yok, 100 bekliyor. Yedek: id saklanır.</summary>
        public static int SetVoice(string voiceId)
        {
            if (!HasPlugin) { _fallbackVoiceId = voiceId ?? ""; return 0; }
            return Call("SetVoice", voiceId ?? "").AsInt32();
        }

        /// <summary>Dil seç. Köprü: LANG_* kodu (&lt;0 = desteklenmiyor/veri yok), 100 = bekliyor. Yedekte her zaman 0.</summary>
        public static int SetLanguage(string lang, string country) =>
            HasPlugin ? Call("SetLanguage", lang ?? "", country ?? "").AsInt32() : 0;

        /// <summary>"accessibility" (varsayılan; müzik hacminden bağımsız) ya da "media".</summary>
        public static void SetAudioUsage(string usage) { if (HasPlugin) Call("SetAudioUsage", usage ?? "accessibility"); }

        // ---------- Konuşma ----------

        public static string Speak(string text, float rate, float pitch, bool interrupt, long utteranceId = 0)
        {
            if (string.IsNullOrWhiteSpace(text)) return "";
            if (HasPlugin) return Call("Speak", text, (double)rate, (double)pitch, interrupt).AsString();
            try
            {
                DisplayServer.TtsSpeak(text, _fallbackVoiceId, 100, pitch, rate, utteranceId, interrupt);
                return utteranceId.ToString();
            }
            catch (Exception e) { _lastError = "TtsSpeak hatası: " + e.Message; return ""; }
        }

        public static void Stop()
        {
            if (HasPlugin) { Call("Stop"); return; }
            try { DisplayServer.TtsStop(); } catch (Exception e) { _lastError = "TtsStop hatası: " + e.Message; }
        }

        public static bool IsSpeaking()
        {
            if (HasPlugin) { var s = GetStatus(); return s.Speaking || s.Queued > 0; }
            try { return DisplayServer.TtsIsSpeaking(); } catch { return false; }
        }
    }
}
