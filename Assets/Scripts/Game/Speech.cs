using System;
using System.Collections.Generic;
using System.Text;
using Godot;

namespace Maden.Game
{
    public enum SpeechPriority
    {
        /// <summary>Yalnızca sessizken söylenir (makine olayları gibi).</summary>
        Low,
        /// <summary>Kuyruk kısaysa söylenir, uzunsa atılır (eşya adları).</summary>
        Normal,
        /// <summary>Kuyruğa eklenir; kuyruk çok uzamışsa öncekini keser (nadir bulgu, satış).</summary>
        High,
        /// <summary>Hemen keser ve söyler (menü gezinme, katman değişimi).</summary>
        Critical
    }

    public sealed class VoiceInfo
    {
        public string Id = "", Name = "", Language = "";
    }

    /// <summary>
    /// DisplayServer TTS sarmalayıcısı (Android'de telefonun varsayılan metin okuma motoru).
    /// Varsayılan: voice id BOŞ verilir, yani telefonun seçili motoru/sesi konuşur (Godot'nun ses listesi
    /// motoru dayatmasın diye). Ayarlardan istenirse belirli bir Türkçe ses seçilebilir.
    /// Tanı günlüğü: user://tts_diag.txt (ses listesi, seçim, hangi id ile konuşuldu).
    /// </summary>
    public partial class Speech : Node
    {
        public static Speech Instance { get; private set; }

        public const string DiagPath = "user://tts_diag.txt";
        public const string NotTurkishWarning = "Varsayılan konuşma motoru Türkçe değil. Telefonun ayarlarından metin okuma bölümünü açıp Türkçe seç.";

        public string LastText { get; private set; } = "";

        /// <summary>Konuşma denenebilir mi (ses listesi dolu ya da Android).</summary>
        public bool VoiceAvailable { get; private set; }

        /// <summary>Konuşmanın Türkçe olacağına inanılıyor mu (varsayılan Türkçe ya da Türkçe ses bulundu).</summary>
        public bool VoiceIsTurkish { get; private set; } = true;

        /// <summary>Konuşma sürüyor mu (müzik kısmak için).</summary>
        public bool IsBusy => NowSec() < _busyUntil;

        /// <summary>Menüde gezilebilir Türkçe sesler (varsayılan seçenek hariç).</summary>
        public IReadOnlyList<VoiceInfo> TurkishVoices => _tr;

        /// <summary>Şu an TtsSpeak'e verilen voice id ("" = telefonun varsayılanı).</summary>
        public string EffectiveVoiceId { get; private set; } = "";

        public int VoiceOptionCount => 1 + _tr.Count;

        const float SecondsPerChar = 0.075f;
        const string SampleSentence = "Merhaba, ben madenci. Bugün altın bulacağız.";

        readonly List<VoiceInfo> _all = new List<VoiceInfo>();
        readonly List<VoiceInfo> _tr = new List<VoiceInfo>();
        readonly StringBuilder _diag = new StringBuilder();
        bool _defaultIsTurkish;
        bool _scanFinal;
        double _bornAt;
        double _nextScan;
        double _busyUntil;
        string _lastDedupText = "";
        double _lastDedupTime = -10;
        long _utteranceId;
        double _nextPoll;
        double _lastSayTime = -10;
        bool _warnPending;
        int _speakLogCount;

        static double NowSec() => Time.GetTicksMsec() / 1000.0;

        public override void _Ready()
        {
            Instance = this;
            _bornAt = NowSec();
            Diag("== Konuşma tanı günlüğü == " + Time.GetDatetimeStringFromSystem());
            Diag("platform=" + OS.GetName() + " locale=" + OS.GetLocale());
            Scan();
        }

        // ---------- Ses listesi / seçim ----------

        void Scan()
        {
            _all.Clear(); _tr.Clear();
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
                        _all.Add(vi);
                        string lang = vi.Language.Replace('_', '-').ToLowerInvariant();
                        if (lang == "tr" || lang.StartsWith("tr-") || lang.StartsWith("tur"))
                            _tr.Add(vi);
                    }
                // Dil bilgisi vermeyen motorlar için TtsGetVoicesForLanguage yedeği.
                if (_tr.Count == 0)
                {
                    var ids = DisplayServer.TtsGetVoicesForLanguage("tr");
                    if (ids != null)
                        foreach (var id in ids)
                        {
                            var found = _all.Find(x => x.Id == id) ?? new VoiceInfo { Id = id, Name = id, Language = "tr" };
                            _tr.Add(found);
                        }
                }
            }
            catch (Exception e) { Diag("TtsGetVoices hatası: " + e.Message); }

            // Sıralama: Google önce, "local" (düşük gecikme) "network"ten önce; sonra ada göre.
            _tr.Sort((a, b) => VoiceRank(a).CompareTo(VoiceRank(b)) != 0
                ? VoiceRank(a).CompareTo(VoiceRank(b))
                : string.CompareOrdinal(a.Id, b.Id));

            bool android = OS.GetName() == "Android";
            VoiceAvailable = _all.Count > 0 || _tr.Count > 0 || android;
            _defaultIsTurkish = OS.GetLocale().StartsWith("tr", StringComparison.OrdinalIgnoreCase);

            Diag("tüm sesler (" + _all.Count + "):");
            foreach (var v in _all) Diag("  id='" + v.Id + "' ad='" + v.Name + "' dil='" + v.Language + "'");
            Diag("Türkçe sesler (" + _tr.Count + "): " + string.Join(", ", _tr.ConvertAll(x => x.Id)));
            Diag("varsayılan Türkçe mi (cihaz dili sezgisi)=" + _defaultIsTurkish);

            // Android'de TTS motoru asenkron açılır: liste ilk anda boş olabilir; bir süre yeniden tara.
            bool listReady = _all.Count > 0;
            if (listReady || !android || NowSec() - _bornAt > 6.0) { _scanFinal = true; ApplyVoiceSetting(true); }
            else _nextScan = NowSec() + 0.5;
        }

        static int VoiceRank(VoiceInfo v)
        {
            string s = (v.Id + " " + v.Name).ToLowerInvariant();
            int r = 0;
            if (!s.Contains("google")) r += 2;
            if (s.Contains("network") || s.Contains("online")) r += 1;
            return r;
        }

        /// <summary>Ayarlardaki seçime göre konuşulacak voice id'yi belirler.</summary>
        public void ApplyVoiceSetting(bool logAndWarn = false)
        {
            string saved = Settings.VoiceId;
            string reason;
            if (!string.IsNullOrEmpty(saved) && _tr.Exists(v => v.Id == saved))
            {
                EffectiveVoiceId = saved; VoiceIsTurkish = true; reason = "kayıtlı ses";
            }
            else if (_defaultIsTurkish)
            {
                EffectiveVoiceId = ""; VoiceIsTurkish = true; reason = "telefonun varsayılanı (Türkçe)";
            }
            else if (_tr.Count > 0)
            {
                EffectiveVoiceId = _tr[0].Id; VoiceIsTurkish = true; reason = "varsayılan Türkçe değil; uyumlu Türkçe ses";
            }
            else
            {
                EffectiveVoiceId = ""; VoiceIsTurkish = false; reason = "varsayılan Türkçe değil ve Türkçe ses yok";
                if (logAndWarn && VoiceAvailable) _warnPending = true;
            }
            Diag("seçim: mod='" + saved + "' -> konuşma id='" + EffectiveVoiceId + "' (" + reason + ")");
        }

        /// <summary>0 = telefonun varsayılanı; 1.. = TurkishVoices[i-1].</summary>
        public int CurrentVoiceOption()
        {
            string saved = Settings.VoiceId;
            if (string.IsNullOrEmpty(saved)) return 0;
            int i = _tr.FindIndex(v => v.Id == saved);
            return i < 0 ? 0 : i + 1;
        }

        public string VoiceOptionName(int option)
        {
            if (option <= 0 || option > _tr.Count) return "Telefonun varsayılanı";
            var v = _tr[option - 1];
            return string.IsNullOrWhiteSpace(v.Name) ? v.Id : v.Name;
        }

        /// <summary>Seçeneği kaydeder ve yeni sesle örnek cümleyi okur.</summary>
        public void SelectVoiceOption(int option)
        {
            int n = VoiceOptionCount;
            option = ((option % n) + n) % n;
            Settings.VoiceId = option == 0 ? "" : _tr[option - 1].Id;
            ApplyVoiceSetting();
            string text = "Ses " + (option + 1) + " / " + n + ": " + VoiceOptionName(option) + ". " + SampleSentence;
            Say(text, SpeechPriority.Critical);
        }

        // ---------- Döngü ----------

        public override void _Process(double delta)
        {
            double now = NowSec();
            if (!_scanFinal)
            {
                if (now >= _nextScan) Scan();
            }
            else if (_warnPending && now - _bornAt > 2.5)
            {
                _warnPending = false;
                Diag("UYARI: " + NotTurkishWarning);
                Haptics.Play(HapticKind.Error);
                Say(NotTurkishWarning, SpeechPriority.High);
            }

            if (!VoiceAvailable || now < _nextPoll) return;
            _nextPoll = now + 0.25;
            // Tahmini süre gerçekte bittiyse düzelt (kısa bir ilk-başlama toleransıyla).
            if (now - _lastSayTime > 0.8 && now < _busyUntil)
            {
                try { if (!DisplayServer.TtsIsSpeaking()) _busyUntil = now; } catch { }
            }
        }

        static float EstimateSeconds(string text) => 0.3f + text.Length * SecondsPerChar / Math.Max(0.5f, Settings.SpeechRate);

        /// <summary>Söyle. Aynı metin kısa sürede tekrar gelirse atılır.</summary>
        public void Say(string text, SpeechPriority priority = SpeechPriority.Normal)
        {
            if (string.IsNullOrWhiteSpace(text)) return;
            double now = NowSec();
            if (text == _lastDedupText && now - _lastDedupTime < 0.8) return;

            double backlog = Math.Max(0.0, _busyUntil - now);
            bool flush = false;
            switch (priority)
            {
                case SpeechPriority.Low: if (backlog > 0.3) return; break;
                case SpeechPriority.Normal: if (backlog > 1.5) return; break;
                case SpeechPriority.High: if (backlog > 5.0) flush = true; break;
                case SpeechPriority.Critical: flush = true; break;
            }

            _lastDedupText = text;
            _lastDedupTime = now;
            _lastSayTime = now;
            LastText = text;
            _busyUntil = (flush ? now : Math.Max(now, _busyUntil)) + EstimateSeconds(text);

            if (!VoiceAvailable)
            {
                GD.Print("[TTS-yok " + priority + "] " + text);
                if (priority >= SpeechPriority.High) Haptics.Play(HapticKind.Tick);
                return;
            }
            try
            {
                _utteranceId++;
                if (_speakLogCount < 12)
                {
                    _speakLogCount++;
                    Diag("konuş id='" + EffectiveVoiceId + "' hız=" + Settings.SpeechRate + " flush=" + flush + " metin='" + (text.Length > 60 ? text.Substring(0, 60) : text) + "'");
                }
                // volume 0..100, pitch 1, rate = konuşma hızı ayarı
                DisplayServer.TtsSpeak(text, EffectiveVoiceId, 100, 1f, Settings.SpeechRate, _utteranceId, flush);
            }
            catch (Exception e) { GD.PushWarning("TTS speak: " + e.Message); }
        }

        /// <summary>Konuşmayı hemen keser.</summary>
        public void Stop()
        {
            _busyUntil = 0;
            if (!VoiceAvailable) return;
            try { DisplayServer.TtsStop(); } catch { }
        }

        // ---------- Tanı günlüğü ----------

        void Diag(string line)
        {
            GD.Print("[TTS] " + line);
            _diag.AppendLine(line);
            try
            {
                using var f = Godot.FileAccess.Open(DiagPath, Godot.FileAccess.ModeFlags.Write);
                if (f != null) f.StoreString(_diag.ToString());
            }
            catch { }
        }
    }
}
