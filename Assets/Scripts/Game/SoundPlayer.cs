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

        const int SfxPoolSize = 8;
        const float MusicMaxGain = 0.7f;
        const float AmbientMaxGain = 0.35f;

        readonly Dictionary<string, AudioStream> _streamCache = new Dictionary<string, AudioStream>();
        readonly Dictionary<string, string[]> _variantCache = new Dictionary<string, string[]>();
        readonly HashSet<string> _warned = new HashSet<string>();
        readonly List<AudioStreamPlayer> _sfxPool = new List<AudioStreamPlayer>();
        readonly Random _rng = new Random();
        int _sfxNext;

        Crossfade _music, _ambient;
        AudioStreamPlayer _drill;
        double _drillStopAt;
        int _drillAlt;

        List<string> _musicTracks = new List<string>();
        readonly List<int> _musicBag = new List<int>();
        int _lastMusic = -1;
        double _musicNextAt = -1;
        int _ambientIndex = -1;
        float _duck = 1f;

        public int PickaxeTier { get; set; }

        static double NowSec() => Time.GetTicksMsec() / 1000.0;

        public override void _Ready()
        {
            Instance = this;
            EnsureBus(BusMusic); EnsureBus(BusSfx); EnsureBus(BusAmbient);

            for (int i = 0; i < SfxPoolSize; i++) _sfxPool.Add(NewPlayer("sfx" + i, BusSfx));
            _music = new Crossfade(NewPlayer("musicA", BusMusic), NewPlayer("musicB", BusMusic));
            _ambient = new Crossfade(NewPlayer("ambA", BusAmbient), NewPlayer("ambB", BusAmbient));
            _drill = NewPlayer("drill", BusSfx);

            _musicTracks = FindMusic();
            if (_musicTracks.Count == 0) GD.PushWarning("Müzik dosyası bulunamadı: " + AudioRoot + "music/");
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

        /// <summary>Tek ses (varyasyon seçmeden). name: klasör içi göreli, örn. "sfx/error".</summary>
        void PlayStream(AudioStream s, float volume, float pitch)
        {
            if (s == null) return;
            var p = _sfxPool[_sfxNext];
            _sfxNext = (_sfxNext + 1) % _sfxPool.Count;
            p.Stream = s;
            p.PitchScale = Math.Clamp(pitch, 0.5f, 2f);
            p.VolumeDb = Mathf.LinearToDb(Math.Clamp(volume, 0.0001f, 1.5f));
            p.Play();
        }

        /// <summary>Ses ailesi çal (örn. "rare_find": rare_find, rare_find_1..). Rastgele varyasyon + hafif pitch.</summary>
        public void PlaySfx(string family, float volume = 1f, float pitch = 1f, bool randomize = true)
        {
            var v = Variants("sfx", family);
            if (v.Length == 0) return;
            string pick = v[_rng.Next(v.Length)];
            float p = pitch * (randomize ? Rand(0.97f, 1.03f) : 1f);
            PlayStream(Load(pick), volume, p);
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
            float pitch = (0.92f + PickaxeTier * 0.035f + Rand(-0.04f, 0.04f)) * pitchMul;
            PlaySfx(fam, 1f, pitch, false);
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
            if (s is AudioStreamOggVorbis ogg) ogg.Loop = true;
            if (!_drill.Playing || _drill.Stream != s) { _drill.Stream = s; _drill.Play(); }
            _drillStopAt = NowSec() + seconds;
        }

        // ---------- Ortam ve müzik ----------

        public void SetLayer(int layer)
        {
            int idx = Math.Clamp(Math.Clamp(layer, 0, 9) / 2 + 1, 1, 5);
            if (idx == _ambientIndex) return;
            _ambientIndex = idx;
            var s = Load("ambient/layer_" + idx);
            if (s is AudioStreamOggVorbis ogg) ogg.Loop = true;
            _ambient.CrossTo(s);
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
            if (s == null) { _musicNextAt = NowSec() + 5; return; }
            if (s is AudioStreamOggVorbis ogg) ogg.Loop = false;
            double len = s.GetLength();
            double fade = Math.Min(6.0, len * 0.4);
            _music.FadeSeconds = (float)fade;
            _music.CrossTo(s);
            _musicNextAt = NowSec() + Math.Max(5.0, len - fade);
        }

        public override void _Process(double delta)
        {
            double now = NowSec();
            float dt = (float)delta;

            if (_musicNextAt >= 0 && now >= _musicNextAt) NextMusic();

            var sp = Speech.Instance;
            float duckTarget = (sp != null && sp.IsBusy) ? 0.45f : 1f;
            _duck = Mathf.MoveToward(_duck, duckTarget, dt * (duckTarget < _duck ? 4f : 0.6f));

            SetBus(BusMusic, Settings.MusicVolume * MusicMaxGain * _duck);
            SetBus(BusAmbient, Settings.SfxVolume * AmbientMaxGain * _duck);
            SetBus(BusSfx, Settings.SfxVolume);
            _music.Tick(dt);
            _ambient.Tick(dt);

            if (_drill.Playing && now >= _drillStopAt) _drill.Stop();
        }

        static void SetBus(string bus, float linear)
        {
            int idx = AudioServer.GetBusIndex(bus);
            if (idx < 0) return;
            AudioServer.SetBusVolumeDb(idx, linear <= 0.0001f ? -80f : Mathf.LinearToDb(linear));
        }

        // ---------- Çapraz geçiş kanalı ----------

        sealed class Crossfade
        {
            readonly AudioStreamPlayer[] _p = new AudioStreamPlayer[2];
            readonly float[] _level = new float[2];
            int _active = -1;
            public float FadeSeconds = 3f;

            public Crossfade(AudioStreamPlayer a, AudioStreamPlayer b) { _p[0] = a; _p[1] = b; }

            public void CrossTo(AudioStream s)
            {
                if (s == null) return;
                int next = _active == 0 ? 1 : 0;
                _p[next].Stream = s;
                _level[next] = 0f;
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
                    _p[i].VolumeDb = _level[i] <= 0.0001f ? -80f : Mathf.LinearToDb(_level[i]);
                    if (i != _active && _level[i] <= 0f && _p[i].Playing) _p[i].Stop();
                }
            }
        }
    }
}
