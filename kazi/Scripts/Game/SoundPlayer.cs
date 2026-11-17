using System;
using System.Collections.Generic;
using Godot;

namespace Kazi.Game
{
    /// <summary>
    /// Ses çalar. AudioBus'lar: Music, Sfx, Ambient, Loops (gadget döngüleri; konuşmada kısılır) ve 5 sabit panlı
    /// kazıma yolu (ScratchPan0..4: sürükleme sesi parmağın sütununa göre sol-sağ yerleşir).
    /// Klipler res://Audio/ altından açılışta ÖNYÜKLENİR; çalarken düğüm yaratılmaz, oyuncu havuzu kullanılır.
    /// Dosya yoksa o olayda sessiz kalınır (sentez yok), uyarı yazılır ve MissingCount artar (selftest bunu denetler).
    /// </summary>
    public partial class SoundPlayer : Node
    {
        public static SoundPlayer Instance { get; private set; }

        public const string AudioRoot = "res://Audio/";
        public const string BusMusic = "Music", BusSfx = "Sfx", BusAmbient = "Ambient", BusLoops = "Loops";
        static readonly string[] PanBuses = { "ScratchPan0", "ScratchPan1", "ScratchPan2", "ScratchPan3", "ScratchPan4" };
        static readonly float[] PanValues = { -0.9f, -0.45f, 0f, 0.45f, 0.9f };

        const int SfxPoolSize = 8, ScratchPoolSize = 6, TickPoolSize = 3;

        // Seviye hedefleri (varsayılan kaydırıcı konumunda): müzik -18 dB, ambiyans -22 dB, efekt -2 dB (adım 8/10).
        const float MusicDbAtDefault = -18f, AmbientDbAtDefault = -22f;
        const int MusicDefaultStep = 5, SfxDefaultStep = 8;

        // Konuşma sırasında kısma: yumuşak (dB alanında, yavaş iniş, daha da yavaş çıkış, kısa bekleme).
        const float DuckDb = -7f, DuckAttackDbPerSec = 14f, DuckReleaseDbPerSec = 5f, DuckHoldSec = 0.5f;
        const float PitchJitter = 0.05f; // rastgele perde ±%5

        public static int MissingCount { get; private set; }
        public static int PlayCalls { get; private set; }
        public static int LoadedClips { get; private set; }
        readonly List<string> _playLog = new List<string>();

        void LogPlay(string what, AudioStreamPlayer p)
        {
            PlayCalls++;
            if (_playLog.Count < 20)
                _playLog.Add(what + " bus=" + p.Bus + " vol_db=" + p.VolumeDb.ToString("0.0") + " playing=" + p.Playing + " pitch=" + p.PitchScale.ToString("0.00"));
        }

        /// <summary>Ses tanı günlüğü: user://audio_diag.txt (bus listesi, seviyeler, klip sayısı, ilk 20 Play çağrısı, çıkış sürücüsü).</summary>
        public string BuildDiag()
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("== Ses tanı günlüğü == " + Time.GetDatetimeStringFromSystem());
            sb.AppendLine("platform=" + OS.GetName() + " driver=" + DisplayServer.GetName() + " mix_rate=" + AudioServer.GetMixRate() + " output_latency=" + AudioServer.GetOutputLatency().ToString("0.000")
                + " output_device=" + AudioServer.OutputDevice + " speaker_mode=" + AudioServer.GetSpeakerMode());
            sb.AppendLine("yüklü klip=" + LoadedClips + " eksik=" + MissingCount + " play_calls=" + PlayCalls + " music_tracks=" + _musicTracks.Count);
            sb.AppendLine("ayar: musicStep=" + Settings.MusicStep + " sfxStep=" + Settings.SfxStep + " duckDb=" + _duckDb.ToString("0.0"));
            for (int i = 0; i < AudioServer.BusCount; i++)
            {
                var n = AudioServer.GetBusName(i);
                sb.AppendLine("bus[" + i + "] " + n + " send=" + AudioServer.GetBusSend(i) + " vol_db=" + AudioServer.GetBusVolumeDb(i).ToString("0.0")
                    + " mute=" + AudioServer.IsBusMute(i) + " solo=" + AudioServer.IsBusSolo(i) + " bypass=" + AudioServer.IsBusBypassingEffects(i)
                    + " effects=" + AudioServer.GetBusEffectCount(i) + " peak_db=" + AudioServer.GetBusPeakVolumeLeftDb(i, 0).ToString("0.0") + "/" + AudioServer.GetBusPeakVolumeRightDb(i, 0).ToString("0.0"));
            }
            sb.AppendLine("android_ses=" + (AndroidTts.AudioInfoJson() is { Length: > 0 } aj ? aj : "(yok/masaüstü)"));
            sb.Append("kare başına en yüksek tepe dB (açılıştan beri): ");
            foreach (var kv in _peakMaxDb) sb.Append(kv.Key + "=" + kv.Value.ToString("0.0") + " ");
            sb.AppendLine();
            sb.AppendLine("ilk Play çağrıları:");
            foreach (var l in _playLog) sb.AppendLine("  " + l);
            return sb.ToString();
        }

        public void WriteDiag()
        {
            try
            {
                using var f = FileAccess.Open("user://audio_diag.txt", FileAccess.ModeFlags.Write);
                f?.StoreString(BuildDiag());
            }
            catch (Exception e) { GD.PushWarning("audio_diag yazılamadı: " + e.Message); }
        }
        static readonly double[] DiagTimes = { 5, 30, 120 };
        int _diagIdx;
        readonly Dictionary<string, float> _peakMaxDb = new Dictionary<string, float>();
        bool _mediaChecked;

        /// <summary>Kare başına en yüksek tepe seviyesi (dB): Master'a gerçekten ses ulaşıyor mu cihazda da görülsün.</summary>
        void SamplePeaks()
        {
            for (int i = 0; i < AudioServer.BusCount; i++)
            {
                string n = AudioServer.GetBusName(i);
                float pk = Math.Max(AudioServer.GetBusPeakVolumeLeftDb(i, 0), AudioServer.GetBusPeakVolumeRightDb(i, 0));
                if (!_peakMaxDb.TryGetValue(n, out float cur) || pk > cur) _peakMaxDb[n] = pk;
            }
        }

        /// <summary>Açılıştan kısa süre sonra: telefonun MEDYA sesi 0 ise (konuşma erişilebilirlik akışında olduğu için duyulur) kullanıcıyı uyar.</summary>
        void CheckMediaVolume()
        {
            _mediaChecked = true;
            try
            {
                if (OS.GetName() == "Android" && AndroidTts.MediaVolumeIsZero())
                    Speech.Instance?.Say("Telefonun medya sesi kapalı. Efektler ve müzik için ses tuşuyla medya sesini aç.", SpeechPriority.High);
            }
            catch (Exception e) { GD.PushWarning("Medya sesi denetimi: " + e.Message); }
        }
        public static readonly List<string> Missing = new List<string>();

        /// <summary>Kodun kullandığı TÜM efekt aileleri (base, base_1..8). Açılışta önyüklenir ve varlığı denetlenir.</summary>
        public static readonly string[] Families =
        {
            "cell_reveal", "match_tick", "boundary_tick", "scratch_short", "scratch",
            "win_chime_1", "win_chime_2", "win_chime_3", "win_small", "win_mid", "win_big_bell", "jackpot_gong", "jackpot_cheer",
            "lose_thud", "trap_hit", "trap_crack", "trap_glass",
            "ticket_tear", "coin_slide", "coin_single", "coin_bag", "coins_drop", "coin_count", "banknote_count", "cash_drawer",
            "help_money_chime", "catalog_unlock_fanfare", "prestige_swell",
            "buy_drawer", "buy_lock", "buy_unlock", "card_riffle", "card_shuffle",
            "menu_paper_open", "menu_whoosh", "page_slide", "page_turn_2", "paper_crumple", "paper_bin_drop", "error_wood",
            "counter_bell", "gadget_crank_gears", "gadget_crank_wood", "gadget_drill", "gadget_fan", "gadget_press",
            "gadget_register", "gadget_sewing_motor", "gadget_steam_gear", "gadget_typewriter"
        };
        public static readonly string[] LoopKeys = { "auto_scratch_loop", "conveyor_loop", "counter_ticks_loop" };
        public static readonly string[] AmbientKeys = { "cafe_loop", "rain_loop" };
        static readonly string[] MusicFallback = { "calm_daisy", "calm_guitar_ladada", "calm_guitar_plains" };

        readonly Dictionary<string, AudioStream> _streamCache = new Dictionary<string, AudioStream>();
        readonly Dictionary<string, string[]> _variantCache = new Dictionary<string, string[]>();
        readonly HashSet<string> _warned = new HashSet<string>();
        readonly Random _rng = new Random();

        sealed class Voice
        {
            public AudioStreamPlayer P;
            public double StartedAt, StopAt, FadeSec;
            public float BaseDb;
        }
        readonly List<Voice> _sfxPool = new List<Voice>();
        readonly List<Voice> _scratchPool = new List<Voice>();
        readonly List<Voice> _tickPool = new List<Voice>();

        sealed class LoopVoice
        {
            public AudioStreamPlayer P;
            public float Target, Level, GainDb;
        }
        readonly Dictionary<string, LoopVoice> _loops = new Dictionary<string, LoopVoice>();

        Crossfade _music, _ambient;
        List<string> _musicTracks = new List<string>();
        readonly List<int> _musicBag = new List<int>();
        int _lastMusic = -1;
        double _musicNextAt = -1;
        double _ambientNextAt = -1;
        int _ambientIdx = -1;
        float _duckDb;
        double _duckHoldUntil;
        readonly Dictionary<string, float> _lastBusDb = new Dictionary<string, float>();

        static double NowSec() => Time.GetTicksMsec() / 1000.0;

        public override void _Ready()
        {
            Instance = this;
            ProcessMode = ProcessModeEnum.Always;
            EnsureBus(BusSfx, "Master"); EnsureBus(BusMusic, "Master"); EnsureBus(BusAmbient, "Master");
            EnsureBus(BusLoops, BusSfx);
            for (int i = 0; i < PanBuses.Length; i++) EnsurePanBus(PanBuses[i], PanValues[i]);
            AddMasterLimiter();

            for (int i = 0; i < SfxPoolSize; i++) _sfxPool.Add(new Voice { P = NewPlayer("sfx" + i, BusSfx) });
            for (int i = 0; i < ScratchPoolSize; i++) _scratchPool.Add(new Voice { P = NewPlayer("scr" + i, PanBuses[2]) });
            for (int i = 0; i < TickPoolSize; i++) _tickPool.Add(new Voice { P = NewPlayer("tick" + i, PanBuses[2]) });
            _music = new Crossfade(NewPlayer("musicA", BusMusic), NewPlayer("musicB", BusMusic), this);
            _ambient = new Crossfade(NewPlayer("ambA", BusAmbient), NewPlayer("ambB", BusAmbient), this) { FadeSeconds = 4f };
            foreach (var k in LoopKeys) _loops[k] = new LoopVoice { P = NewPlayer("loop_" + k, BusLoops) };

            _musicTracks = FindMusic();
            Preload();
        }

        static void AddMasterLimiter()
        {
            try
            {
                int master = AudioServer.GetBusIndex("Master");
                if (master < 0) return;
                for (int i = 0; i < AudioServer.GetBusEffectCount(master); i++)
                    if (AudioServer.GetBusEffect(master, i) is AudioEffectLimiter) return;
                AudioServer.AddBusEffect(master, new AudioEffectLimiter { CeilingDb = -1f, ThresholdDb = -4f, SoftClipDb = 2f });
            }
            catch (Exception e) { GD.PushWarning("Sınırlayıcı eklenemedi: " + e.Message); }
        }

        static void EnsureBus(string name, string send)
        {
            if (AudioServer.GetBusIndex(name) >= 0) return;
            AudioServer.AddBus();
            int idx = AudioServer.BusCount - 1;
            AudioServer.SetBusName(idx, name);
            AudioServer.SetBusSend(idx, send);
        }

        static void EnsurePanBus(string name, float pan)
        {
            EnsureBus(name, BusSfx);
            int idx = AudioServer.GetBusIndex(name);
            if (idx >= 0 && AudioServer.GetBusEffectCount(idx) == 0)
                AudioServer.AddBusEffect(idx, new AudioEffectPanner { Pan = pan });
        }

        AudioStreamPlayer NewPlayer(string name, string bus)
        {
            var p = new AudioStreamPlayer { Name = name, Bus = bus };
            AddChild(p);
            return p;
        }

        // ---------- Klip yükleme ve önyükleme ----------

        static void NoteMissing(string what)
        {
            MissingCount++;
            Missing.Add(what);
            GD.PushWarning("Ses dosyası yok: " + what);
        }

        AudioStream Load(string relPath)
        {
            if (_streamCache.TryGetValue(relPath, out var s)) return s;
            string path = AudioRoot + relPath + ".ogg";
            AudioStream stream = null;
            if (ResourceLoader.Exists(path)) stream = ResourceLoader.Load<AudioStream>(path);
            if (stream == null && _warned.Add(relPath)) NoteMissing(path);
            _streamCache[relPath] = stream;
            return stream;
        }

        /// <summary>base, base_1, base_2... biçimindeki var olan dosyaları bulur.</summary>
        string[] Variants(string baseName)
        {
            if (_variantCache.TryGetValue(baseName, out var v)) return v;
            string key = "sfx/" + baseName;
            var list = new List<string>();
            if (ResourceLoader.Exists(AudioRoot + key + ".ogg")) list.Add(key);
            for (int i = 1; i <= 8; i++)
                if (ResourceLoader.Exists(AudioRoot + key + "_" + i + ".ogg")) list.Add(key + "_" + i);
            v = list.ToArray();
            _variantCache[baseName] = v;
            if (v.Length == 0 && _warned.Add(key)) NoteMissing(AudioRoot + key + "[_n].ogg");
            return v;
        }

        List<string> FindMusic()
        {
            var list = new List<string>();
            using var dir = DirAccess.Open(AudioRoot + "music");
            if (dir != null)
            {
                foreach (var f in dir.GetFiles())
                {
                    string n = f;
                    if (n.EndsWith(".import")) n = n.Substring(0, n.Length - 7);
                    else if (n.EndsWith(".remap")) n = n.Substring(0, n.Length - 6);
                    if (n.EndsWith(".ogg")) { n = n.Substring(0, n.Length - 4); if (!list.Contains("music/" + n)) list.Add("music/" + n); }
                }
            }
            if (list.Count == 0) foreach (var n in MusicFallback) list.Add("music/" + n);
            list.Sort(StringComparer.Ordinal);
            return list;
        }

        /// <summary>Açılışta her şeyi yükler: ilk kullanımda disk/çözme gecikmesi olmasın.</summary>
        void Preload()
        {
            int n = 0;
            foreach (var fam in Families)
                foreach (var v in Variants(fam)) { if (Load(v) != null) n++; }
            foreach (var k in LoopKeys)
                if (Load("sfx/" + k) is AudioStreamOggVorbis ogg) { ogg.Loop = true; n++; }
            foreach (var k in AmbientKeys)
                if (Load("ambient/" + k) is AudioStreamOggVorbis ogg) { ogg.Loop = true; n++; }
            foreach (var m in _musicTracks) if (Load(m) != null) n++;
            LoadedClips = n;
            GD.Print("Ses önyüklendi: " + n + " klip, eksik: " + MissingCount);
        }

        // ---------- Efektler ----------

        float Rand(float a, float b) => a + (float)_rng.NextDouble() * (b - a);

        static SfxMeta.Info MetaOf(string key) => SfxMeta.Table.TryGetValue(key, out var m) ? m : new SfxMeta.Info(0f, 0f, 0f);

        static Voice Acquire(List<Voice> pool)
        {
            Voice best = null;
            foreach (var v in pool) if (!v.P.Playing) return v;
            foreach (var v in pool) if (best == null || v.StartedAt < best.StartedAt) best = v;
            return best;
        }

        void PlayStream(List<Voice> pool, string key, float volume, float pitch, string bus = null)
        {
            var s = Load(key);
            if (s == null) return;
            var m = MetaOf(key);
            var v = Acquire(pool);
            double now = NowSec();
            float pc = Math.Clamp(pitch, 0.5f, 2f);
            v.P.Stop();
            if (bus != null) v.P.Bus = bus;
            v.P.Stream = s;
            v.P.PitchScale = pc;
            v.BaseDb = m.GainDb + Mathf.LinearToDb(Math.Clamp(volume, 0.0001f, 1.5f));
            v.P.VolumeDb = v.BaseDb;
            v.StartedAt = now;
            double len = (m.End > 0f ? m.End : (float)s.GetLength()) - m.Start;
            if (m.End > 0f)
            {
                len /= pc;
                v.StopAt = now + len;
                v.FadeSec = Math.Min(0.08, len * 0.4);
            }
            else { v.StopAt = 0; }
            v.P.Play(m.Start);
            LogPlay("sfx " + key, v.P);
        }

        /// <summary>Kırpılmış kısa vuruşlarda bitişe yaklaşırken sön, sonra durdur (çıtlama olmasın).</summary>
        static void TickVoices(List<Voice> pool, double now)
        {
            foreach (var v in pool)
            {
                if (!v.P.Playing || v.StopAt <= 0) continue;
                double left = v.StopAt - now;
                if (left <= 0) { v.P.Stop(); continue; }
                if (left < v.FadeSec) v.P.VolumeDb = v.BaseDb + Mathf.LinearToDb((float)Math.Max(0.02, left / v.FadeSec));
            }
        }

        /// <summary>Ses ailesi çal (örn. "win_small": win_small_1.. ya da tam ad "win_chime_2"). Rastgele varyasyon + ±%5 perde.</summary>
        public void PlaySfx(string family, float volume = 1f, float pitch = 1f, bool randomize = true)
        {
            var v = Variants(family);
            if (v.Length == 0) return;
            string pick = v[_rng.Next(v.Length)];
            float p = pitch * (randomize ? Rand(1f - PitchJitter, 1f + PitchJitter) : 1f);
            PlayStream(_sfxPool, pick, volume, p);
        }

        /// <summary>Gecikmeli çal (zamanlayıcı; kare hızından bağımsız). Çalarken düğüm yaratmaz (SceneTreeTimer hafiftir).</summary>
        public void PlaySfxLater(string family, double delaySec, float volume = 1f, float pitch = 1f)
        {
            if (delaySec <= 0.001) { PlaySfx(family, volume, pitch); return; }
            GetTree().CreateTimer(delaySec, true, false, true).Timeout += () => PlaySfx(family, volume, pitch);
        }

        /// <summary>
        /// Sürükleme kazıma sesi. speed 0..1 (parmak hızı) -> perde ve ses; pan01 0..1 (parmağın ekrandaki x konumu) -> sol-sağ.
        /// En fazla 20-30 ms'de bir çağrılması beklenir (çağıran sınırlar).
        /// </summary>
        public void PlayScratch(float speed01, float pan01)
        {
            var v = Variants("scratch_short");
            if (v.Length == 0) return;
            speed01 = Math.Clamp(speed01, 0f, 1f);
            string pick = v[_rng.Next(v.Length)];
            float pitch = (0.85f + 0.6f * speed01) * Rand(1f - PitchJitter, 1f + PitchJitter);
            float vol = 0.35f + 0.65f * speed01;
            int bus = Math.Clamp((int)Math.Round(Math.Clamp(pan01, 0f, 1f) * (PanBuses.Length - 1)), 0, PanBuses.Length - 1);
            PlayStream(_scratchPool, pick, vol, pitch, PanBuses[bus]);
        }

        /// <summary>Hücre sınırı geçişi ipucu (kısa tık).</summary>
        public void PlayBoundary(float pan01)
        {
            var v = Variants("boundary_tick");
            if (v.Length == 0) return;
            int bus = Math.Clamp((int)Math.Round(Math.Clamp(pan01, 0f, 1f) * (PanBuses.Length - 1)), 0, PanBuses.Length - 1);
            PlayStream(_tickPool, v[_rng.Next(v.Length)], 1f, Rand(0.97f, 1.03f), PanBuses[bus]);
        }

        // ---------- Gadget / otomasyon döngüleri ----------

        /// <summary>Döngüyü aç/kapat (yumuşak giriş-çıkış). Her döngü ayrı, ayırt edilebilir bir ses.</summary>
        public void SetLoop(string key, bool on)
        {
            if (!_loops.TryGetValue(key, out var l)) return;
            l.Target = on ? 1f : 0f;
            if (on && !l.P.Playing)
            {
                var s = Load("sfx/" + key);
                if (s == null) return;
                l.GainDb = MetaOf("sfx/" + key).GainDb;
                l.P.Stream = s;
                l.Level = 0f;
                l.P.VolumeDb = -80f;
                l.P.Play((float)(_rng.NextDouble() * Math.Max(0.1, s.GetLength() - 0.1)));
                LogPlay("loop " + key, l.P);
            }
        }

        /// <summary>Selftest: her şeyi durdur (ölçüm temiz olsun); müzik/ortam zamanlayıcıları askıya alınır.</summary>
        public void StopAllForTest()
        {
            _musicNextAt = -1; _ambientNextAt = -1;
            foreach (var c in GetChildren()) if (c is AudioStreamPlayer ap) ap.Stop();
            foreach (var l in _loops.Values) { l.Target = 0f; l.Level = 0f; }
            _music.Reset(); _ambient.Reset();
        }

        public bool LoopOn(string key) => _loops.TryGetValue(key, out var l) && l.Target > 0f;

        // ---------- Ortam ve müzik ----------

        public void StartMusic() { _musicNextAt = 0; if (_ambientNextAt < 0) _ambientNextAt = 0; }

        void NextAmbient()
        {
            // Kafe ya da yağmur: rastgele seç, birkaç dakikada bir çapraz geçişle değiştir.
            int idx = _rng.Next(AmbientKeys.Length);
            if (AmbientKeys.Length > 1 && idx == _ambientIdx) idx = (idx + 1) % AmbientKeys.Length;
            _ambientIdx = idx;
            string key = "ambient/" + AmbientKeys[idx];
            var s = Load(key);
            if (s is AudioStreamOggVorbis ogg) ogg.Loop = true;
            _ambient.CrossTo(s, MetaOf(key).GainDb);
            _ambientNextAt = NowSec() + 240 + _rng.Next(120);
        }

        void NextMusic()
        {
            if (_musicTracks.Count == 0) { _musicNextAt = NowSec() + 30; return; }
            if (_musicBag.Count == 0)
            {
                for (int i = 0; i < _musicTracks.Count; i++) _musicBag.Add(i);
                for (int i = _musicBag.Count - 1; i > 0; i--)
                {
                    int j = _rng.Next(i + 1);
                    (_musicBag[i], _musicBag[j]) = (_musicBag[j], _musicBag[i]);
                }
                if (_musicBag.Count > 1 && _musicBag[0] == _lastMusic)
                    (_musicBag[0], _musicBag[_musicBag.Count - 1]) = (_musicBag[_musicBag.Count - 1], _musicBag[0]);
            }
            int pick = _musicBag[0];
            _musicBag.RemoveAt(0);
            _lastMusic = pick;
            var s = Load(_musicTracks[pick]);
            float musicGain = MetaOf(_musicTracks[pick]).GainDb;
            if (s == null) { _musicNextAt = NowSec() + 5; return; }
            if (s is AudioStreamOggVorbis ogg) ogg.Loop = false;
            double len = s.GetLength();
            double fade = Math.Min(6.0, len * 0.4);
            _music.FadeSeconds = (float)fade;
            _music.CrossTo(s, musicGain);
            _musicNextAt = NowSec() + Math.Max(5.0, len - fade);
        }

        public override void _Process(double delta)
        {
            double now = NowSec();
            float dt = (float)delta;

            SamplePeaks();
            if (_diagIdx < DiagTimes.Length && now >= DiagTimes[_diagIdx]) { WriteDiag(); _diagIdx++; }
            if (!_mediaChecked && now >= 6) CheckMediaVolume();
            if (_musicNextAt >= 0 && now >= _musicNextAt) NextMusic();
            if (_ambientNextAt >= 0 && now >= _ambientNextAt) NextAmbient();

            // Konuşma sırasında müzik/ortam/döngüler yumuşakça kısılır; konuşma bitince kısa bekleyip yavaşça açılır.
            var sp = Speech.Instance;
            bool speaking = sp != null && sp.IsBusy;
            if (speaking) _duckHoldUntil = now + DuckHoldSec;
            float target = (speaking || now < _duckHoldUntil) ? DuckDb : 0f;
            float rate = target < _duckDb ? DuckAttackDbPerSec : DuckReleaseDbPerSec;
            _duckDb = Mathf.MoveToward(_duckDb, target, rate * dt);

            SetBusDb(BusMusic, StepDb(Settings.MusicStep, MusicDefaultStep, MusicDbAtDefault) + _duckDb);
            SetBusDb(BusAmbient, StepDb(Settings.SfxStep, SfxDefaultStep, AmbientDbAtDefault) + _duckDb);
            SetBusDb(BusSfx, StepDb(Settings.SfxStep, Settings.MaxStep, 0f));
            SetBusDb(BusLoops, _duckDb);
            _music.Tick(dt);
            _ambient.Tick(dt);

            TickVoices(_sfxPool, now);
            TickVoices(_scratchPool, now);
            TickVoices(_tickPool, now);

            foreach (var l in _loops.Values)
            {
                if (!l.P.Playing && l.Target <= 0f) continue;
                l.Level = Mathf.MoveToward(l.Level, l.Target, dt / (l.Target > l.Level ? 1.2f : 0.6f));
                if (l.Level <= 0.0001f && l.Target <= 0f) { l.P.Stop(); continue; }
                l.P.VolumeDb = Mathf.LinearToDb(Math.Max(0.0001f, l.Level)) + l.GainDb;
            }
        }

        /// <summary>Kaydırıcı adımı -> dB: varsayılan adımda dbAtDefault, oranla 20*log10 değişir; 0 = sessiz.</summary>
        static float StepDb(int step, int defaultStep, float dbAtDefault)
        {
            if (step <= 0) return -80f;
            return dbAtDefault + 20f * Mathf.Log(step / (float)defaultStep) / Mathf.Log(10f);
        }

        void SetBusDb(string bus, float db)
        {
            if (_lastBusDb.TryGetValue(bus, out float last) && Math.Abs(last - db) < 0.05f) return;
            int idx = AudioServer.GetBusIndex(bus);
            if (idx < 0) return;
            _lastBusDb[bus] = db;
            AudioServer.SetBusVolumeDb(idx, db);
        }

        // ---------- Çapraz geçiş kanalı ----------

        sealed class Crossfade
        {
            readonly AudioStreamPlayer[] _p = new AudioStreamPlayer[2];
            readonly float[] _level = new float[2];
            readonly float[] _gainDb = new float[2];
            int _active = -1;
            public float FadeSeconds = 3f;

            public SoundPlayer Owner;
            public Crossfade(AudioStreamPlayer a, AudioStreamPlayer b, SoundPlayer owner = null) { _p[0] = a; _p[1] = b; Owner = owner; }

            public void CrossTo(AudioStream s, float gainDb = 0f)
            {
                if (s == null) return;
                int next = _active == 0 ? 1 : 0;
                _p[next].Stream = s;
                _level[next] = 0f;
                _gainDb[next] = gainDb;
                _p[next].VolumeDb = -80f;
                _p[next].Play();
                _active = next;
                Owner?.LogPlay("xfade", _p[next]);
            }

            public void Reset() { _active = -1; _level[0] = _level[1] = 0f; }

            public void Tick(float dt)
            {
                float step = dt / Math.Max(0.2f, FadeSeconds);
                for (int i = 0; i < 2; i++)
                {
                    float target = i == _active ? 1f : 0f;
                    _level[i] = Mathf.MoveToward(_level[i], target, step);
                    _p[i].VolumeDb = _level[i] <= 0.0001f ? -80f : Mathf.LinearToDb(_level[i]) + _gainDb[i];
                    if (i != _active && _level[i] <= 0f && _p[i].Playing) _p[i].Stop();
                }
            }
        }
    }
}
