using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Ses çalar: efektler (havuz), katman ortamı (çapraz geçiş), sakin müzik (rastgele sıra, çapraz karartma).
/// Klipler Resources/Audio/{sfx,music,ambient} altından yüklenir; yoksa kodla sentezlenir.
/// </summary>
public class SoundPlayer : MonoBehaviour
{
    public static SoundPlayer Instance { get; private set; }

    /// <summary>Core katman indeksi 0'dan mı başlıyor (10 katman: 0..9) yoksa 1'den mi (1..10)?</summary>
    public const int LayerIndexBase = 0;

    const int SfxPoolSize = 8;
    const float MusicMaxGain = 0.7f;
    const float AmbientMaxGain = 0.35f;

    readonly Dictionary<string, AudioClip> _sfxCache = new Dictionary<string, AudioClip>();
    readonly List<AudioSource> _sfxPool = new List<AudioSource>();
    int _sfxNext;

    CrossfadeChannel _music;
    CrossfadeChannel _ambient;
    AudioSource _drillSource;
    float _drillStopAt;

    AudioClip[] _musicClips;
    readonly List<int> _musicBag = new List<int>();
    int _lastMusicIndex = -1;
    float _musicNextAt;

    int _ambientIndex = -1;
    float _duck = 1f;

    public int PickaxeTier { get; set; }

    void Awake()
    {
        Instance = this;
        for (int i = 0; i < SfxPoolSize; i++) _sfxPool.Add(NewSource("sfx" + i, false));
        _music = new CrossfadeChannel(NewSource("musicA", false), NewSource("musicB", false));
        _ambient = new CrossfadeChannel(NewSource("ambA", true), NewSource("ambB", true));
        _drillSource = NewSource("drill", true);

        _musicClips = Resources.LoadAll<AudioClip>("Audio/music");
        if (_musicClips == null || _musicClips.Length == 0)
            _musicClips = new[] { Synth.Music(0), Synth.Music(1) };
    }

    AudioSource NewSource(string n, bool loop)
    {
        var go = new GameObject(n);
        go.transform.SetParent(transform, false);
        var s = go.AddComponent<AudioSource>();
        s.playOnAwake = false;
        s.loop = loop;
        s.spatialBlend = 0f;
        return s;
    }

    // ---------- Klip yükleme ----------

    AudioClip GetSfx(string name)
    {
        if (_sfxCache.TryGetValue(name, out var c) && c != null) return c;
        c = Resources.Load<AudioClip>("Audio/sfx/" + name);
        if (c == null) c = Synth.Sfx(name);
        _sfxCache[name] = c;
        return c;
    }

    // ---------- Efektler ----------

    public void PlaySfx(string name, float volume = 1f, float pitch = 1f)
    {
        var clip = GetSfx(name);
        if (clip == null) return;
        var s = _sfxPool[_sfxNext];
        _sfxNext = (_sfxNext + 1) % _sfxPool.Count;
        s.pitch = pitch;
        s.PlayOneShot(clip, Mathf.Clamp01(volume * MadenSettings.SfxVolume));
    }

    /// <summary>Katmandaki malzemeye göre grup: 0 toprak/kil, 1 taş/kömür, 2 metal, 3 kristal.</summary>
    public static int HitGroup(int layer)
    {
        int l = Mathf.Clamp(layer - LayerIndexBase, 0, 9);
        if (l <= 1) return 0;
        if (l <= 3) return 1;
        if (l <= 6) return 2;
        return 3;
    }

    public void PlayHit(int layer, float pitchMul = 1f)
    {
        string[] names = { "dirt_hit", "stone_hit", "metal_hit", "crystal_hit" };
        float pitch = (0.92f + PickaxeTier * 0.035f + Random.Range(-0.04f, 0.04f)) * pitchMul;
        PlaySfx(names[HitGroup(layer)], 1f, Mathf.Clamp(pitch, 0.7f, 1.5f));
    }

    public void PlayDynamite() { PlaySfx("dynamite", 1f, Random.Range(0.95f, 1.05f)); }

    /// <summary>Matkap: dinamitten ayırt edilir; kısa süreli döngü.</summary>
    public void PlayDrill(float seconds = 1.8f)
    {
        var clip = GetSfx("drill_loop");
        if (clip == null) return;
        _drillSource.clip = clip;
        _drillSource.pitch = 1f;
        _drillSource.volume = MadenSettings.SfxVolume * 0.8f;
        if (!_drillSource.isPlaying) _drillSource.Play();
        _drillStopAt = Time.unscaledTime + seconds;
    }

    // ---------- Ortam ve müzik ----------

    public void SetLayer(int layer)
    {
        int idx = Mathf.Clamp((Mathf.Clamp(layer - LayerIndexBase, 0, 9)) / 2 + 1, 1, 5);
        if (idx == _ambientIndex) return;
        _ambientIndex = idx;
        var clip = Resources.Load<AudioClip>("Audio/ambient/layer_" + idx);
        if (clip == null) clip = Synth.Ambient(idx - 1);
        _ambient.CrossTo(clip, true);
    }

    public void StartMusic()
    {
        _musicNextAt = 0f;
    }

    void NextMusic()
    {
        if (_musicClips.Length == 0) return;
        if (_musicBag.Count == 0)
        {
            for (int i = 0; i < _musicClips.Length; i++) _musicBag.Add(i);
            // Karıştır; önceki parça yeni çantanın başına gelmesin.
            for (int i = _musicBag.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                (_musicBag[i], _musicBag[j]) = (_musicBag[j], _musicBag[i]);
            }
            if (_musicBag.Count > 1 && _musicBag[0] == _lastMusicIndex)
            {
                (_musicBag[0], _musicBag[_musicBag.Count - 1]) = (_musicBag[_musicBag.Count - 1], _musicBag[0]);
            }
        }
        int pick = _musicBag[0];
        _musicBag.RemoveAt(0);
        _lastMusicIndex = pick;
        var clip = _musicClips[pick];
        _music.CrossTo(clip, false);
        // Parça bitmeden 6 sn önce sıradakine geç (çapraz karartma); çok kısa parçalarda yarısında.
        float fade = Mathf.Min(6f, clip.length * 0.4f);
        _music.FadeSeconds = fade;
        _musicNextAt = Time.unscaledTime + Mathf.Max(5f, clip.length - fade);
    }

    void Update()
    {
        float now = Time.unscaledTime;
        float dt = Time.unscaledDeltaTime;

        if (_musicClips != null && now >= _musicNextAt) NextMusic();

        var sp = Speech.Instance;
        float duckTarget = (sp != null && sp.IsBusy) ? 0.45f : 1f;
        _duck = Mathf.MoveTowards(_duck, duckTarget, dt * (duckTarget < _duck ? 4f : 0.6f));

        _music.Tick(dt, MadenSettings.MusicVolume * MusicMaxGain * _duck);
        _ambient.Tick(dt, MadenSettings.SfxVolume * AmbientMaxGain * _duck);

        if (_drillSource.isPlaying && now >= _drillStopAt) _drillSource.Stop();
    }

    // ---------- Çapraz geçiş kanalı ----------

    class CrossfadeChannel
    {
        readonly AudioSource[] _src = new AudioSource[2];
        readonly float[] _level = new float[2];
        int _active = -1;
        public float FadeSeconds = 3f;

        public CrossfadeChannel(AudioSource a, AudioSource b) { _src[0] = a; _src[1] = b; }

        public void CrossTo(AudioClip clip, bool loop)
        {
            if (clip == null) return;
            int next = _active == 0 ? 1 : 0;
            var s = _src[next];
            s.clip = clip;
            s.loop = loop;
            s.time = 0f;
            _level[next] = 0f;
            s.volume = 0f;
            s.Play();
            _active = next;
        }

        public void Tick(float dt, float masterGain)
        {
            float step = dt / Mathf.Max(0.2f, FadeSeconds);
            for (int i = 0; i < 2; i++)
            {
                float target = i == _active ? 1f : 0f;
                _level[i] = Mathf.MoveTowards(_level[i], target, step);
                _src[i].volume = _level[i] * masterGain;
                if (i != _active && _level[i] <= 0f && _src[i].isPlaying) _src[i].Stop();
            }
        }
    }
}
