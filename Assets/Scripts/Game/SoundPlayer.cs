using System;
using System.Collections.Generic;
using Godot;

namespace Maden.Game
{
    public enum HitGroup { Dirt, Stone, Coal, Metal, Crystal }

    /// <summary>
    /// Ses çalar. Ayrı AudioBus'lar: Music, Sfx, Ambient. Klipler res://Assets/Resources/Audio/ altından yüklenir;
    /// dosya yoksa o olayda sessiz kalınır (sentez yok) ve uyarı yazılır.
    /// </summary>
    public partial class SoundPlayer : Node
    {
        public static SoundPlayer Instance { get; private set; }

        public const string AudioRoot = "res://Assets/Resources/Audio/";
        public const string BusMusic = "Music", BusSfx = "Sfx", BusAmbient = "Ambient";

        // Havuzlar: vuruşlar kendi havuzunda (önemli efektleri çalmasın), polifoni sınırlı.
        const int HitPoolSize = 4;
        const int SfxPoolSize = 5;

        // Seviye hedefleri (varsayılan kaydırıcı konumunda): müzik arkada -18 dB, ortam -20 dB, efekt ~ -2 dB.
        const float MusicDbAtDefault = -18f;   // MusicStep == 5
        const float AmbientDbAtDefault = -20f; // SfxStep == 8
        const int MusicDefaultStep = 5, SfxDefaultStep = 8;

        // Konuşma sırasında kısma: yumuşak (dB alanında, yavaş iniş, daha da yavaş çıkış, kısa bekleme).
        const float DuckDb = -7f, DuckAttackDbPerSec = 14f, DuckReleaseDbPerSec = 5f, DuckHoldSec = 0.5f;

        // Rastgele perde (pitch) aralığı: ±%5.
        const float PitchJitter = 0.05f;

        readonly Dictionary<string, AudioStream> _streamCache = new Dictionary<string, AudioStream>();
        readonly Dictionary<string, string[]> _variantCache = new Dictionary<string, string[]>();
        readonly HashSet<string> _warned = new HashSet<string>();
        readonly Random _rng = new Random();

        /// <summary>Havuzdaki bir oyuncu + süre sınırı/sönme bilgisi.</summary>
        sealed class Voice
        {
            public AudioStreamPlayer P;
            public double StartedAt, StopAt, FadeSec;
            public float BaseDb;
        }
        readonly List<Voice> _hitPool = new List<Voice>();
        readonly List<Voice> _sfxPool = new List<Voice>();

        Crossfade _music, _ambient;
        AudioStreamPlayer _drill;
        double _drillStopAt, _drillStartedAt;
        float _drillGainDb;
        int _drillAlt;

        List<string> _musicTracks = new List<string>();
        readonly List<int> _musicBag = new List<int>();
        int _lastMusic = -1;
        double _musicNextAt = -1;
        int _ambientIndex = -1;
        float _duckDb;
        double _duckHoldUntil;
        readonly Dictionary<string, float> _lastBusDb = new Dictionary<string, float>();

        public int PickaxeTier { get; set; }

        static double NowSec() => Time.GetTicksMsec() / 1000.0;

        public override void _Ready()
        {
            Instance = this;
            EnsureBus(BusMusic); EnsureBus(BusSfx); EnsureBus(BusAmbient);
            AddMasterLimiter();

            for (int i = 0; i < HitPoolSize; i++) _hitPool.Add(new Voice { P = NewPlayer("hit" + i, BusSfx) });
            for (int i = 0; i < SfxPoolSize; i++) _sfxPool.Add(new Voice { P = NewPlayer("sfx" + i, BusSfx) });
            _music = new Crossfade(NewPlayer("musicA", BusMusic), NewPlayer("musicB", BusMusic));
            _ambient = new Crossfade(NewPlayer("ambA", BusAmbient), NewPlayer("ambB", BusAmbient));
            _drill = NewPlayer("drill", BusSfx);

            _musicTracks = FindMusic();
            if (_musicTracks.Count == 0) GD.PushWarning("Müzik dosyası bulunamadı: " + AudioRoot + "music/");
            Preload();
        }

        /// <summary>Üst üste binen sesler master'da kırpılmasın: yumuşak sınırlayıcı.</summary>
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

        static readonly string[] SfxFamilies =
        {
            "buy", "coal_hit", "crystal_hit", "dirt_hit", "dynamite", "error", "fanfare", "fuse", "legend_find",
            "menu_close", "menu_open", "metal_hit", "rare_find", "sell", "stone_hit", "upgrade"
        };

        /// <summary>Açılışta tüm efektleri yükler: ilk vuruşta disk/çözme gecikmesi olmasın.</summary>
        void Preload()
        {
            int n = 0;
            foreach (var fam in SfxFamilies)
                foreach (var v in Variants("sfx", fam)) { if (Load(v) != null) n++; }
            foreach (var d in new[] { "sfx/drill_loop", "sfx/drill_loop_2" })
                if (Load(d) is AudioStreamOggVorbis ogg) { ogg.Loop = true; n++; }
            GD.Print("Ses önyüklendi: " + n + " klip");
        }

        static void EnsureBus(string name)
        {
            if (AudioServer.GetBusIndex(name) >= 0) return;
            AudioServer.AddBus();
            int idx = AudioServer.BusCount - 1;
            AudioServer.SetBusName(idx, name);
            AudioServer.SetBusSend(idx, "Master");
        }

        AudioStreamPlayer NewPlayer(string name, string bus)
        {
            var p = new AudioStreamPlayer { Name = name, Bus = bus };
            AddChild(p);
            return p;
        }

        // ---------- Klip yükleme ----------

        AudioStream Load(string relPath)
        {
            if (_streamCache.TryGetValue(relPath, out var s)) return s;
            string path = AudioRoot + relPath + ".ogg";
            AudioStream stream = null;
            if (ResourceLoader.Exists(path)) stream = ResourceLoader.Load<AudioStream>(path);
            if (stream == null && _warned.Add(relPath)) GD.PushWarning("Ses dosyası yok: " + path);
            _streamCache[relPath] = stream;
            return stream;
        }

        /// <summary>base, base_1, base_2... biçimindeki var olan dosyaları bulur.</summary>
        string[] Variants(string folder, string baseName)
        {
            string key = folder + "/" + baseName;
            if (_variantCache.TryGetValue(key, out var v)) return v;
            var list = new List<string>();
            if (ResourceLoader.Exists(AudioRoot + key + ".ogg")) list.Add(key);
            for (int i = 1; i <= 8; i++)
                if (ResourceLoader.Exists(AudioRoot + key + "_" + i + ".ogg")) list.Add(key + "_" + i);
            v = list.ToArray();
            _variantCache[key] = v;
            if (v.Length == 0 && _warned.Add(key)) GD.PushWarning("Ses dosyası yok: " + AudioRoot + key + "[_n].ogg");
            return v;
        }

        List<string> FindMusic()
        {
            var list = new List<string>();
            using var dir = DirAccess.Open(AudioRoot + "music");
            if (dir == null) return list;
            foreach (var f in dir.GetFiles())
            {
                string n = f;
                if (n.EndsWith(".import")) n = n.Substring(0, n.Length - 7);
                else if (n.EndsWith(".remap")) n = n.Substring(0, n.Length - 6);
                if (n.EndsWith(".ogg")) { n = n.Substring(0, n.Length - 4); if (!list.Contains("music/" + n)) list.Add("music/" + n); }
            }
            list.Sort(StringComparer.Ordinal);
            return list;
        }

        // ---------- Efektler ----------

        float Rand(float a, float b) => a + (float)_rng.NextDouble() * (b - a);

        static SfxMeta.Info MetaOf(string key) => SfxMeta.Table.TryGetValue(key, out var m) ? m : new SfxMeta.Info(0f, 0f, 0f);

        /// <summary>Havuzdan boş oyuncu; yoksa en eski çalanı (kısa sönmeyle) devralır.</summary>
        Voice Acquire(List<Voice> pool)
        {
            Voice best = null;
            foreach (var v in pool) if (!v.P.Playing) return v;
            foreach (var v in pool) if (best == null || v.StartedAt < best.StartedAt) best = v;
            return best;
        }

        void PlayStream(List<Voice> pool, string key, float volume, float pitch)
        {
            var s = Load(key);
            if (s == null) return;
            var m = MetaOf(key);
            var v = Acquire(pool);
            double now = NowSec();
            float pitchClamped = Math.Clamp(pitch, 0.5f, 2f);
            v.P.Stop();
            v.P.Stream = s;
            v.P.PitchScale = pitchClamped;
            v.BaseDb = m.GainDb + Mathf.LinearToDb(Math.Clamp(volume, 0.0001f, 1.5f));
            v.P.VolumeDb = v.BaseDb;
            v.StartedAt = now;
            if (m.End > 0f)
            {
                double len = (m.End - m.Start) / pitchClamped;
                v.StopAt = now + len;
                v.FadeSec = Math.Min(0.08, len * 0.4);
            }
            else v.StopAt = 0;
            v.P.Play(m.Start);
        }

        /// <summary>Süre sınırı: bitişe yaklaşırken sön, sonra durdur (kuyruk uzamasın, çıtlama olmasın).</summary>
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

        /// <summary>Ses ailesi çal (örn. "rare_find": rare_find, rare_find_1..). Rastgele varyasyon + hafif (±%5) pitch.</summary>
        public void PlaySfx(string family, float volume = 1f, float pitch = 1f, bool randomize = true)
        {
            var v = Variants("sfx", family);
            if (v.Length == 0) return;
            string pick = v[_rng.Next(v.Length)];
            float p = pitch * (randomize ? Rand(1f - PitchJitter, 1f + PitchJitter) : 1f);
            PlayStream(_sfxPool, pick, volume, p);
        }

        public static HitGroup GroupOf(int layer)
        {
            switch (Math.Clamp(layer, 0, 9))
            {
                case 0: case 1: return HitGroup.Dirt;   // toprak, kil
                case 2: return HitGroup.Stone;           // taş
                case 3: return HitGroup.Coal;            // kömür
                case 4: case 5: case 6: return HitGroup.Metal; // demir, gümüş, altın
                default: return HitGroup.Crystal;        // kristal, elmas, kadim
            }
        }

        public void PlayHit(int layer, float pitchMul = 1f)
        {
            string fam;
            switch (GroupOf(layer))
            {
                case HitGroup.Dirt: fam = "dirt_hit"; break;
                case HitGroup.Stone: fam = "stone_hit"; break;
                case HitGroup.Coal: fam = "coal_hit"; break;
                case HitGroup.Metal: fam = "metal_hit"; break;
                default: fam = "crystal_hit"; break;
            }
            var v = Variants("sfx", fam);
            if (v.Length == 0) return;
            string pick = v[_rng.Next(v.Length)];
            // Kazma kademesi perdeyi çok hafif yükseltir (kademe başına +%1, en çok +%6); rastgele ±%5.
            float pitch = (1f + Math.Min(PickaxeTier, 6) * 0.01f) * pitchMul * Rand(1f - PitchJitter, 1f + PitchJitter);
            PlayStream(_hitPool, pick, 1f, pitch);
        }

        public void PlayDynamite()
        {
            PlaySfx("fuse", 0.8f);
            GetTree().CreateTimer(0.45).Timeout += () => PlaySfx("dynamite");
        }

        /// <summary>Matkap: dinamitten ayırt edilir; kısa süreli döngü.</summary>
        public void PlayDrill(double seconds = 1.8)
        {
            string name = (_drillAlt++ % 2 == 0) ? "sfx/drill_loop" : "sfx/drill_loop_2";
            var s = Load(name) ?? Load("sfx/drill_loop");
            if (s == null) return;
            double now = NowSec();
            if (!_drill.Playing || _drill.Stream != s)
            {
                _drillGainDb = MetaOf(name).GainDb;
                _drill.Stream = s;
                _drill.VolumeDb = -60f;
                _drill.Play();
                _drillStartedAt = now;
            }
            _drillStopAt = now + seconds;
        }

        // ---------- Ortam ve müzik ----------

        public void SetLayer(int layer)
        {
            int idx = Math.Clamp(Math.Clamp(layer, 0, 9) / 2 + 1, 1, 5);
            if (idx == _ambientIndex) return;
            _ambientIndex = idx;
            string key = "ambient/layer_" + idx;
            var s = Load(key);
            if (s is AudioStreamOggVorbis ogg) ogg.Loop = true;
            _ambient.CrossTo(s, MetaOf(key).GainDb);
        }

        public void StartMusic() { _musicNextAt = 0; }

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

            if (_musicNextAt >= 0 && now >= _musicNextAt) NextMusic();

            // Konuşma sırasında müzik/ortam yumuşakça kısılır; konuşma bitince kısa bekleyip yavaşça açılır.
            var sp = Speech.Instance;
            bool speaking = sp != null && sp.IsBusy;
            if (speaking) _duckHoldUntil = now + DuckHoldSec;
            float target = (speaking || now < _duckHoldUntil) ? DuckDb : 0f;
            float rate = target < _duckDb ? DuckAttackDbPerSec : DuckReleaseDbPerSec;
            _duckDb = Mathf.MoveToward(_duckDb, target, rate * dt);

            SetBusDb(BusMusic, StepDb(Settings.MusicStep, MusicDefaultStep, MusicDbAtDefault) + _duckDb);
            SetBusDb(BusAmbient, StepDb(Settings.SfxStep, SfxDefaultStep, AmbientDbAtDefault) + _duckDb);
            SetBusDb(BusSfx, StepDb(Settings.SfxStep, Settings.MaxStep, 0f));
            _music.Tick(dt);
            _ambient.Tick(dt);

            TickVoices(_hitPool, now);
            TickVoices(_sfxPool, now);

            if (_drill.Playing)
            {
                double left = _drillStopAt - now;
                if (left <= 0) _drill.Stop();
                else
                {
                    float fadeIn = (float)Math.Min(1.0, (now - _drillStartedAt) / 0.12);
                    float fadeOut = (float)Math.Min(1.0, left / 0.25);
                    float lin = Math.Max(0.001f, Math.Min(fadeIn, fadeOut));
                    _drill.VolumeDb = _drillGainDb + Mathf.LinearToDb(lin);
                }
            }
        }

        /// <summary>Kaydırıcı adımı -> dB: varsayılan adımda dbAtDefault, adım oranına göre 20*log10 ile değişir; 0 = sessiz.</summary>
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

            public Crossfade(AudioStreamPlayer a, AudioStreamPlayer b) { _p[0] = a; _p[1] = b; }

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
            }

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
