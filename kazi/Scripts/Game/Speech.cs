using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Godot;

namespace Kazi.Game
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

    /// <summary>
    /// Konuşma katmanı. Android'de AndroidTts köprüsüyle (tools/android_tts) kurulu HER metin okuma motoru
    /// seçilebilir; köprü yoksa (masaüstü) DisplayServer TTS'ine düşer.
    /// Varsayılan: motor "Telefonun varsayılanı", dil Türkçe, ses motorun kendi Türkçe varsayılanı.
    /// Tanı günlüğü: user://tts_diag.txt (motorlar, seçilen motor, sesler, hatalar, hangi sesle konuşuldu).
    /// </summary>
    public partial class Speech : Node
    {
        public static Speech Instance { get; private set; }

        public const string DiagPath = "user://tts_diag.txt";
        public const string NotTurkishWarning = "Varsayılan konuşma motoru Türkçe değil. Telefonun ayarlarından metin okuma bölümünü açıp Türkçe seç.";
        public const string NotTurkishEngineWarning = "Seçili konuşma motoru Türkçe konuşamıyor. Ayarlarda konuşma motoru seçeneğinden Türkçe destekleyen bir motor seç.";

        public string LastText { get; private set; } = "";

        /// <summary>Konuşma denenebilir mi (ses listesi dolu, Android ya da köprü var).</summary>
        public bool VoiceAvailable { get; private set; }

        /// <summary>Konuşmanın Türkçe olacağına inanılıyor mu.</summary>
        public bool VoiceIsTurkish { get; private set; } = true;

        /// <summary>Konuşma sürüyor mu (müzik kısmak için).</summary>
        public bool IsBusy => NowSec() < _busyUntil;

        /// <summary>Kuyrukta tahmini kaç saniyelik konuşma kaldı.</summary>
        public double Backlog => Math.Max(0.0, _busyUntil - NowSec());

        /// <summary>Menüde gezilebilir sesler: seçili motorun Türkçe sesleri (hiç yoksa tümü); varsayılan seçenek hariç.</summary>
        public IReadOnlyList<VoiceInfo> TurkishVoices => VoiceList;

        /// <summary>Şu an konuşmada kullanılan ses (köprüde ses adı, yedekte Godot voice id; "" = varsayılan).</summary>
        public string EffectiveVoiceId { get; private set; } = "";

        /// <summary>Android TTS köprüsü var mı (motor seçimi yalnızca o zaman anlamlı).</summary>
        public bool EngineChoiceAvailable => _plugin;

        /// <summary>Şu an açık olan motorun paket adı ("" = telefonun varsayılanı).</summary>
        public string EffectiveEngine => _engineEffective;

        IReadOnlyList<VoiceInfo> VoiceList => _tr.Count > 0 ? _tr : _all;
        public int VoiceOptionCount => 1 + VoiceList.Count;
        public int EngineOptionCount => _plugin ? 1 + _engines.Count : 1;

        const float SecondsPerChar = 0.075f;
        const string SampleSentence = "Merhaba, ben kazı kazan görevlisi. Bugün şanslı gününüz.";
        const double PluginPollSec = 0.25;

        readonly List<VoiceInfo> _all = new List<VoiceInfo>();
        readonly List<VoiceInfo> _tr = new List<VoiceInfo>();
        List<TtsEngineInfo> _engines = new List<TtsEngineInfo>();
        readonly StringBuilder _diag = new StringBuilder();
        bool _plugin;
        int _phase;                 // 0 yok, 1 motor açılıyor, 2 hazır, 3 başarısız (yalnızca köprü)
        string _engineEffective = "";
        string _lastLoggedError = "";
        string _notice = "";        // motor hazır olunca söylenecek bilgi (geri dönüş vb.)
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
        string _warnText = NotTurkishWarning;
        double _nextPluginPoll;
        int _speakLogCount;

        static double NowSec() => Time.GetTicksMsec() / 1000.0;

        public override void _Ready()
        {
            Instance = this;
            _bornAt = NowSec();
            Diag("== Konuşma tanı günlüğü == " + Time.GetDatetimeStringFromSystem());
            Diag("platform=" + OS.GetName() + " locale=" + OS.GetLocale());
            _defaultIsTurkish = OS.GetLocale().StartsWith("tr", StringComparison.OrdinalIgnoreCase);
            _plugin = AndroidTts.HasPlugin;
            Diag("Android TTS köprüsü (" + AndroidTts.PluginName + "): " + (_plugin ? "VAR" : "yok -> DisplayServer yedeği") + (_plugin ? "" : " " + AndroidTts.LastError));
            if (_plugin)
            {
                _engines = AndroidTts.ListEngines();
                Diag("varsayılan motor='" + AndroidTts.GetDefaultEngine() + "'");
                Diag("kurulu motorlar (" + _engines.Count + "):");
                foreach (var e in _engines) Diag("  paket='" + e.Package + "' ad='" + e.Label + "'" + (e.IsDefault ? " [varsayılan]" : ""));
                if (_engines.Count == 0) Diag("UYARI: hiç motor listelenmedi (manifestte <queries> TTS_SERVICE eksik olabilir). Son hata: " + AndroidTts.LastError);
                AndroidTts.SetAudioUsage("accessibility");
                StartEngine(Settings.EngineId);
            }
            else Scan();
        }

        // ---------- Motor (yalnızca köprü) ----------

        /// <summary>Belirtilen motoru açar ("" = telefonun varsayılanı). Kayıtlı motor artık kurulu değilse varsayılana düşer.</summary>
        void StartEngine(string package)
        {
            package ??= "";
            if (package != "" && !_engines.Exists(e => e.Package == package))
            {
                Diag("kayıtlı motor kurulu değil: '" + package + "' -> varsayılan");
                package = "";
                Settings.EngineId = "";
                Settings.VoiceId = "";
            }
            _engineEffective = package;
            _phase = 1;
            _scanFinal = false;
            VoiceAvailable = true; // motor hazır olana kadar konuşmalar Java tarafında kuyruğa alınır
            _all.Clear(); _tr.Clear();
            Diag("motor açılıyor: '" + (package == "" ? "(telefonun varsayılanı)" : package) + "'");
            AndroidTts.Init(package);
            int lr = AndroidTts.SetLanguage("tr", "TR");
            if (!string.IsNullOrEmpty(Settings.VoiceId)) AndroidTts.SetVoice(Settings.VoiceId);
            Diag("dil isteği tr-TR (kod=" + lr + "), kayıtlı ses='" + Settings.VoiceId + "'");
        }

        void TickPlugin(double now)
        {
            var st = AndroidTts.GetStatus();
            if (!string.IsNullOrEmpty(st.LastError) && st.LastError != _lastLoggedError)
            {
                _lastLoggedError = st.LastError;
                Diag("HATA (köprü): " + st.LastError);
            }
            if (_phase != 1) return;
            if (st.State == "ready") OnEngineReady(st);
            else if (st.State == "error") OnEngineFailed(st);
        }

        void OnEngineReady(TtsStatus st)
        {
            _phase = 2;
            _scanFinal = true;
            Scan();
            // Dil kodu: >= 0 kullanılabilir. Türkçe ses listesi doluysa da Türkçe sayılır.
            bool langOk = st.LangResult >= 0 && st.LangResult != AndroidTts.Pending;
            Diag("motor hazır: '" + _engineEffective + "' dil kodu=" + st.LangResult + " ses kodu=" + st.VoiceResult + " akış=" + st.AudioUsage);
            VoiceIsTurkish = langOk || _tr.Count > 0;
            if (!VoiceIsTurkish) { _warnPending = true; _warnText = NotTurkishEngineWarning; }
            if (!string.IsNullOrEmpty(_notice)) { Say(_notice, SpeechPriority.High); _notice = ""; }
        }

        void OnEngineFailed(TtsStatus st)
        {
            Diag("motor açılamadı: '" + _engineEffective + "' hata=" + st.LastError);
            if (_engineEffective != "")
            {
                string failed = EngineName(_engineEffective);
                Settings.EngineId = "";
                Settings.VoiceId = "";
                _notice = "Konuşma motoru açılamadı: " + failed + ". Telefonun varsayılanına dönüldü.";
                Haptics.Play(HapticKind.Error);
                StartEngine("");
            }
            else
            {
                _phase = 3;
                VoiceAvailable = false;
                VoiceIsTurkish = false;
                Haptics.Play(HapticKind.Error);
                Diag("UYARI: varsayılan motor da açılamadı; konuşma yok.");
            }
        }

        string EngineName(string package)
        {
            var e = _engines.Find(x => x.Package == package);
            return e == null || string.IsNullOrWhiteSpace(e.Label) ? package : e.Label;
        }

        /// <summary>0 = telefonun varsayılanı; 1.. = kurulu motorlar.</summary>
        public int CurrentEngineOption()
        {
            string saved = Settings.EngineId;
            if (!_plugin || string.IsNullOrEmpty(saved)) return 0;
            int i = _engines.FindIndex(e => e.Package == saved);
            return i < 0 ? 0 : i + 1;
        }

        public string EngineOptionName(int option)
        {
            if (!_plugin) return "Telefonun varsayılanı";
            if (option <= 0 || option > _engines.Count)
            {
                var def = _engines.Find(e => e.IsDefault);
                return def == null ? "Telefonun varsayılanı" : "Telefonun varsayılanı, " + EngineName(def.Package);
            }
            return EngineName(_engines[option - 1].Package);
        }

        /// <summary>Motoru değiştirir, kaydeder, yeni motorla örnek cümleyi okur (motor hazır olunca).</summary>
        public void SelectEngineOption(int option)
        {
            if (!_plugin) { Say("Bu cihazda konuşma motoru seçimi yok.", SpeechPriority.Critical); return; }
            int n = EngineOptionCount;
            option = ((option % n) + n) % n;
            Settings.EngineId = option == 0 ? "" : _engines[option - 1].Package;
            Settings.VoiceId = "";
            Diag("menü: motor seçildi -> '" + Settings.EngineId + "'");
            StartEngine(Settings.EngineId);
            Say("Motor " + (option + 1) + " / " + n + ": " + EngineOptionName(option) + ". " + SampleSentence, SpeechPriority.Critical);
        }

        // ---------- Ses listesi / seçim ----------

        void Scan()
        {
            _all.Clear(); _tr.Clear();
            try
            {
                foreach (var vi in AndroidTts.ListVoices())
                {
                    _all.Add(vi);
                    if (vi.IsTurkish) _tr.Add(vi);
                }
            }
            catch (Exception e) { Diag("ses listesi hatası: " + e.Message); }

            // Sıralama: kurulu olanlar, Google önce (köprü dışı), local (düşük gecikme) network'ten önce; sonra ada göre.
            _tr.Sort((a, b) => VoiceRank(a).CompareTo(VoiceRank(b)) != 0
                ? VoiceRank(a).CompareTo(VoiceRank(b))
                : string.CompareOrdinal(a.Id, b.Id));

            bool android = OS.GetName() == "Android";
            if (_plugin) VoiceAvailable = _phase != 3;
            else VoiceAvailable = _all.Count > 0 || _tr.Count > 0 || android;

            Diag("tüm sesler (" + _all.Count + "):");
            foreach (var v in _all)
                Diag("  id='" + v.Id + "' ad='" + v.Name + "' dil='" + v.Language + "'" + (_plugin ? " kalite=" + v.Quality + " ağ=" + v.Network + " kurulu=" + v.Installed + " tr=" + v.IsTurkish : ""));
            Diag("Türkçe sesler (" + _tr.Count + "): " + string.Join(", ", _tr.ConvertAll(x => x.Id)));
            Diag("varsayılan Türkçe mi (cihaz dili sezgisi)=" + _defaultIsTurkish);

            if (_plugin) { ApplyVoiceSetting(true); return; }

            // Köprüsüz Android'de TTS motoru asenkron açılır: liste ilk anda boş olabilir; bir süre yeniden tara.
            bool listReady = _all.Count > 0;
            if (listReady || !android || NowSec() - _bornAt > 6.0) { _scanFinal = true; ApplyVoiceSetting(true); }
            else _nextScan = NowSec() + 0.5;
        }

        static int VoiceRank(VoiceInfo v)
        {
            string s = (v.Id + " " + v.Name).ToLowerInvariant();
            int r = 0;
            if (!v.Installed) r += 4;
            if (!s.Contains("google")) r += 2;
            if (v.Network || s.Contains("network") || s.Contains("online")) r += 1;
            return r;
        }

        /// <summary>Ayarlardaki seçime göre konuşulacak sesi belirler.</summary>
        public void ApplyVoiceSetting(bool logAndWarn = false)
        {
            string saved = Settings.VoiceId;
            string reason;
            var list = VoiceList;
            if (!string.IsNullOrEmpty(saved) && list.Any(v => v.Id == saved))
            {
                EffectiveVoiceId = saved; VoiceIsTurkish = true; reason = "kayıtlı ses";
            }
            else if (_plugin)
            {
                // Köprü: ses seçilmediyse motorun Türkçe varsayılan sesi (dil tr-TR).
                EffectiveVoiceId = ""; reason = "motorun Türkçe varsayılan sesi";
                if (!string.IsNullOrEmpty(saved)) Settings.VoiceId = "";
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
                if (logAndWarn && VoiceAvailable) { _warnPending = true; _warnText = NotTurkishWarning; }
            }
            int r = AndroidTts.SetVoice(EffectiveVoiceId);
            Diag("seçim: mod='" + saved + "' -> konuşma sesi='" + EffectiveVoiceId + "' (" + reason + ") SetVoice=" + r);
        }

        /// <summary>0 = varsayılan ses; 1.. = menüdeki sesler.</summary>
        public int CurrentVoiceOption()
        {
            string saved = Settings.VoiceId;
            if (string.IsNullOrEmpty(saved)) return 0;
            int i = -1;
            var list = VoiceList;
            for (int k = 0; k < list.Count; k++) if (list[k].Id == saved) { i = k; break; }
            return i < 0 ? 0 : i + 1;
        }

        public string VoiceOptionName(int option)
        {
            var list = VoiceList;
            if (option <= 0 || option > list.Count) return "Varsayılan ses";
            var v = list[option - 1];
            return string.IsNullOrWhiteSpace(v.Name) ? v.Id : v.Name;
        }

        /// <summary>Seçeneği kaydeder ve yeni sesle örnek cümleyi okur.</summary>
        public void SelectVoiceOption(int option)
        {
            int n = VoiceOptionCount;
            option = ((option % n) + n) % n;
            Settings.VoiceId = option == 0 ? "" : VoiceList[option - 1].Id;
            ApplyVoiceSetting();
            string text = "Ses " + (option + 1) + " / " + n + ": " + VoiceOptionName(option) + ". " + SampleSentence;
            Say(text, SpeechPriority.Critical);
        }

        // ---------- Döngü ----------

        public override void _Process(double delta)
        {
            double now = NowSec();
            if (_plugin)
            {
                if (now >= _nextPluginPoll) { _nextPluginPoll = now + PluginPollSec; TickPlugin(now); }
            }
            else if (!_scanFinal)
            {
                if (now >= _nextScan) Scan();
            }
            if (_warnPending && _scanFinal && now - _bornAt > 2.5)
            {
                _warnPending = false;
                Diag("UYARI: " + _warnText);
                Haptics.Play(HapticKind.Error);
                Say(_warnText, SpeechPriority.High);
            }

            if (!VoiceAvailable || now < _nextPoll) return;
            _nextPoll = now + 0.25;
            // Tahmini süre gerçekte bittiyse düzelt (kısa bir ilk-başlama toleransıyla).
            if (now - _lastSayTime > 0.8 && now < _busyUntil)
            {
                try { if (!AndroidTts.IsSpeaking()) _busyUntil = now; } catch { }
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
                    Diag("konuş motor='" + _engineEffective + "' ses='" + EffectiveVoiceId + "' hız=" + Settings.SpeechRate + " flush=" + flush + " metin='" + (text.Length > 60 ? text.Substring(0, 60) : text) + "'");
                }
                // Köprü: seçili motor/ses; yedek: DisplayServer.TtsSpeak. rate = konuşma hızı ayarı, pitch 1.
                AndroidTts.Speak(text, Settings.SpeechRate, 1f, flush, _utteranceId);
            }
            catch (Exception e) { GD.PushWarning("TTS speak: " + e.Message); }
        }

        /// <summary>Konuşmayı hemen keser.</summary>
        public void Stop()
        {
            _busyUntil = 0;
            if (!VoiceAvailable) return;
            try { AndroidTts.Stop(); } catch { }
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
