using UnityEngine;

public enum HapticKind { Soft, Hard, Metal, Rare, Sell, Blast, Tick }

/// <summary>Titreşim desenleri. API 26+ VibrationEffect, API 31+ VibratorManager, altı Handheld.Vibrate.</summary>
public static class Haptics
{
    // Her desen: zamanlar (ms; ilk değer bekleme) ve genlikler (0 = sessiz, 1..255).
    struct Pattern
    {
        public long[] Times; public int[] Amps;
        public Pattern(long[] t, int[] a) { Times = t; Amps = a; }
    }

    static Pattern Get(HapticKind k)
    {
        switch (k)
        {
            case HapticKind.Soft:  return new Pattern(new long[] { 0, 22 }, new int[] { 0, 70 });
            case HapticKind.Hard:  return new Pattern(new long[] { 0, 40 }, new int[] { 0, 255 });
            case HapticKind.Metal: return new Pattern(new long[] { 0, 30, 45, 30 }, new int[] { 0, 210, 0, 210 });
            case HapticKind.Rare:  return new Pattern(new long[] { 0, 70, 50, 70, 50, 220 }, new int[] { 0, 255, 0, 255, 0, 255 });
            case HapticKind.Sell:  return new Pattern(new long[] { 0, 30, 30, 30, 30, 60 }, new int[] { 0, 120, 0, 170, 0, 220 });
            case HapticKind.Blast: return new Pattern(new long[] { 0, 140, 40, 380 }, new int[] { 0, 255, 0, 255 });
            default:               return new Pattern(new long[] { 0, 15 }, new int[] { 0, 90 });
        }
    }

    /// <summary>Katmana göre kazma titreşimi: toprak yumuşak, taş sert, metal çift, kristal sert.</summary>
    public static HapticKind ForLayerGroup(int group)
    {
        switch (group)
        {
            case 0: return HapticKind.Soft;
            case 1: return HapticKind.Hard;
            case 2: return HapticKind.Metal;
            default: return HapticKind.Hard;
        }
    }

    public static void Play(HapticKind kind)
    {
        if (!MadenSettings.HapticsOn) return;
        try { PlayInternal(kind); }
        catch (System.Exception e) { Debug.LogWarning("Titreşim: " + e.Message); }
    }

#if UNITY_ANDROID && !UNITY_EDITOR
    static bool _init;
    static AndroidJavaObject _vibrator;
    static AndroidJavaClass _effectClass;
    static int _sdk;
    static bool _amplitude;

    static void Init()
    {
        _init = true;
        using (var version = new AndroidJavaClass("android.os.Build$VERSION"))
            _sdk = version.GetStatic<int>("SDK_INT");
        using (var up = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
        {
            var activity = up.GetStatic<AndroidJavaObject>("currentActivity");
            if (_sdk >= 31)
            {
                using (var mgr = activity.Call<AndroidJavaObject>("getSystemService", "vibrator_manager"))
                    _vibrator = mgr?.Call<AndroidJavaObject>("getDefaultVibrator");
            }
            if (_vibrator == null)
                _vibrator = activity.Call<AndroidJavaObject>("getSystemService", "vibrator");
        }
        if (_vibrator != null && _sdk >= 26)
        {
            _effectClass = new AndroidJavaClass("android.os.VibrationEffect");
            try { _amplitude = _vibrator.Call<bool>("hasAmplitudeControl"); } catch { _amplitude = false; }
        }
    }

    static void PlayInternal(HapticKind kind)
    {
        if (!_init) Init();
        var p = Get(kind);
        if (_vibrator == null) { Handheld.Vibrate(); return; }
        if (_sdk >= 26 && _effectClass != null)
        {
            AndroidJavaObject effect;
            if (_amplitude)
                effect = _effectClass.CallStatic<AndroidJavaObject>("createWaveform", p.Times, p.Amps, -1);
            else
            {
                // Genlik denetimi yok: sadece zamanlama (ilk eleman bekleme).
                effect = _effectClass.CallStatic<AndroidJavaObject>("createWaveform", p.Times, -1);
            }
            _vibrator.Call("vibrate", effect);
            effect.Dispose();
        }
        else
        {
            // API < 26: eski vibrate(long[] pattern, int repeat)
            _vibrator.Call("vibrate", p.Times, -1);
        }
    }
#else
    static void PlayInternal(HapticKind kind) { /* Editör/PC: titreşim yok */ }
#endif
}
