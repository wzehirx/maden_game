using System.Collections.Generic;
using UnityEngine;

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
/// Android TextToSpeech (tr-TR) sarmalayıcısı. Editörde/PC'de Debug.Log'a düşer.
/// Kuyruk, kendi süre tahminiyle sınırlanır; böylece saatlerce dinlerken konuşma yığılmaz.
/// </summary>
public class Speech : MonoBehaviour
{
    public static Speech Instance { get; private set; }

    /// <summary>Son söylenen metin (ekranda kısa durum satırı olarak gösterilir).</summary>
    public string LastText { get; private set; } = "";

    /// <summary>Tahmini olarak konuşma sürüyor mu (müzik kısmak için).</summary>
    public bool IsBusy => Time.unscaledTime < _busyUntil;

    const int QueueFlush = 0;
    const int QueueAdd = 1;
    const float SecondsPerChar = 0.075f; // tr hızı 1.0'da yaklaşık

    float _busyUntil;
    float _lastSayTime = -10f;
    string _lastDedupText = "";
    float _lastDedupTime = -10f;
    int _utteranceId;
    float _appliedRate = -1f;
    readonly List<string> _preInit = new List<string>();

#if UNITY_ANDROID && !UNITY_EDITOR
    AndroidJavaObject _tts;
    AndroidJavaObject _bundle;
    volatile int _initStatus; // 0 bekliyor, 1 başarılı, -1 başarısız
    bool _ready;
    float _nextPoll;

    class InitListener : AndroidJavaProxy
    {
        readonly Speech _owner;
        public InitListener(Speech owner) : base("android.speech.tts.TextToSpeech$OnInitListener") { _owner = owner; }
        // Başka iş parçacığından çağrılabilir: yalnızca bayrak yazar.
        public void onInit(int status) { _owner._initStatus = status == 0 ? 1 : -1; }
    }
#endif

    void Awake()
    {
        Instance = this;
        MadenSettings.Changed += ApplyRate;
#if UNITY_ANDROID && !UNITY_EDITOR
        try
        {
            using (var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            {
                var activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
                activity.Call("runOnUiThread", new AndroidJavaRunnable(() =>
                {
                    try
                    {
                        _tts = new AndroidJavaObject("android.speech.tts.TextToSpeech", activity, new InitListener(this));
                    }
                    catch (System.Exception e)
                    {
                        Debug.LogWarning("TTS oluşturulamadı: " + e.Message);
                        _initStatus = -1;
                    }
                }));
            }
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("TTS başlatılamadı: " + e.Message);
            _initStatus = -1;
        }
#endif
    }

    void OnDestroy()
    {
        MadenSettings.Changed -= ApplyRate;
#if UNITY_ANDROID && !UNITY_EDITOR
        try { _tts?.Call("stop"); _tts?.Call("shutdown"); } catch { }
#endif
    }

    void Update()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        if (!_ready && _initStatus != 0)
        {
            if (_initStatus == 1) FinishInit();
            _ready = true; // başarısız olsa da denemeyi bırak
            if (_initStatus == 1) FlushPreInit();
        }
        if (_ready && _initStatus == 1 && Time.unscaledTime >= _nextPoll)
        {
            _nextPoll = Time.unscaledTime + 0.25f;
            // Tahmini süre gerçekte bittiyse düzelt (kısa bir ilk-başlama toleransıyla).
            if (Time.unscaledTime - _lastSayTime > 0.6f && Time.unscaledTime < _busyUntil)
            {
                try { if (!_tts.Call<bool>("isSpeaking")) _busyUntil = Time.unscaledTime; } catch { }
            }
        }
#endif
    }

#if UNITY_ANDROID && !UNITY_EDITOR
    void FinishInit()
    {
        try
        {
            using (var locale = new AndroidJavaObject("java.util.Locale", "tr", "TR"))
            {
                int r = _tts.Call<int>("setLanguage", locale); // -1 veri yok, -2 desteklenmiyor
                if (r < 0) Debug.LogWarning("TTS tr-TR desteklenmiyor veya ses verisi yok: " + r);
            }
            ApplyAudioAttributes();
            _bundle = new AndroidJavaObject("android.os.Bundle");
            ApplyRate();
        }
        catch (System.Exception e) { Debug.LogWarning("TTS ayarı başarısız: " + e.Message); }
    }

    void ApplyAudioAttributes()
    {
        try
        {
            using (var b = new AndroidJavaObject("android.media.AudioAttributes$Builder"))
            {
                b.Call<AndroidJavaObject>("setUsage", 1);        // USAGE_MEDIA
                b.Call<AndroidJavaObject>("setContentType", 1);  // CONTENT_TYPE_SPEECH
                using (var attrs = b.Call<AndroidJavaObject>("build"))
                    _tts.Call<int>("setAudioAttributes", attrs);
            }
        }
        catch (System.Exception e) { Debug.LogWarning("TTS AudioAttributes: " + e.Message); }
    }

    void FlushPreInit()
    {
        foreach (var t in _preInit) NativeSpeak(t, QueueAdd);
        _preInit.Clear();
    }

    void NativeSpeak(string text, int mode)
    {
        try
        {
            _utteranceId++;
            _tts.Call<int>("speak", text, mode, _bundle, "u" + _utteranceId);
        }
        catch (System.Exception e) { Debug.LogWarning("TTS speak: " + e.Message); }
    }
#endif

    void ApplyRate()
    {
        float rate = MadenSettings.SpeechRate;
        if (Mathf.Approximately(rate, _appliedRate)) return;
        _appliedRate = rate;
#if UNITY_ANDROID && !UNITY_EDITOR
        try { if (_tts != null && _ready && _initStatus == 1) _tts.Call<int>("setSpeechRate", rate); } catch { }
#endif
    }

    float EstimateSeconds(string text)
    {
        return 0.3f + text.Length * SecondsPerChar / Mathf.Max(0.5f, MadenSettings.SpeechRate);
    }

    /// <summary>Söyle. Aynı metin kısa sürede tekrar gelirse atılır.</summary>
    public void Say(string text, SpeechPriority priority = SpeechPriority.Normal)
    {
        if (string.IsNullOrWhiteSpace(text)) return;
        float now = Time.unscaledTime;
        if (text == _lastDedupText && now - _lastDedupTime < 0.8f) return;

        float backlog = Mathf.Max(0f, _busyUntil - now);
        bool flush = false;
        switch (priority)
        {
            case SpeechPriority.Low: if (backlog > 0.3f) return; break;
            case SpeechPriority.Normal: if (backlog > 2.5f) return; break;
            case SpeechPriority.High: if (backlog > 5f) flush = true; break;
            case SpeechPriority.Critical: flush = true; break;
        }

        _lastDedupText = text;
        _lastDedupTime = now;
        _lastSayTime = now;
        LastText = text;
        _busyUntil = (flush ? now : Mathf.Max(now, _busyUntil)) + EstimateSeconds(text);

#if UNITY_ANDROID && !UNITY_EDITOR
        if (!_ready)
        {
            if (flush) _preInit.Clear();
            if (_preInit.Count < 3) _preInit.Add(text);
            return;
        }
        if (_initStatus == 1) { ApplyRate(); NativeSpeak(text, flush ? QueueFlush : QueueAdd); }
        else Debug.Log("[TTS-yok] " + text);
#else
        Debug.Log("[TTS " + priority + "] " + text);
#endif
    }

    /// <summary>Konuşmayı hemen keser.</summary>
    public void Stop()
    {
        _busyUntil = 0f;
        _preInit.Clear();
#if UNITY_ANDROID && !UNITY_EDITOR
        try { if (_tts != null && _initStatus == 1) _tts.Call<int>("stop"); } catch { }
#endif
    }
}
