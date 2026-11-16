using System;
using UnityEngine;

/// <summary>Kullanıcı ayarları (PlayerPrefs). Adımlı değerler: ekran okuyucusuz kolay değiştirilsin diye.</summary>
public static class MadenSettings
{
    public static event Action Changed;

    public const int MaxStep = 10;
    static readonly float[] SpeechRates = { 0.7f, 0.85f, 1.0f, 1.15f, 1.3f, 1.5f, 1.75f, 2.0f };

    public static int MusicStep { get => Get("musicStep", 5); set => Set("musicStep", Mathf.Clamp(value, 0, MaxStep)); }
    public static int SfxStep { get => Get("sfxStep", 8); set => Set("sfxStep", Mathf.Clamp(value, 0, MaxStep)); }
    public static int SpeechStep { get => Get("speechStep", 3); set => Set("speechStep", Mathf.Clamp(value, 0, SpeechRates.Length - 1)); }
    public static bool HapticsOn { get => Get("haptics", 1) == 1; set => Set("haptics", value ? 1 : 0); }
    public static bool TutorialDone { get => Get("tutorialDone", 0) == 1; set => Set("tutorialDone", value ? 1 : 0); }

    public static int SpeechStepCount => SpeechRates.Length;
    public static float MusicVolume => MusicStep / (float)MaxStep;
    public static float SfxVolume => SfxStep / (float)MaxStep;
    public static float SpeechRate => SpeechRates[Mathf.Clamp(SpeechStep, 0, SpeechRates.Length - 1)];

    static int Get(string key, int def) => PlayerPrefs.GetInt("maden." + key, def);

    static void Set(string key, int value)
    {
        PlayerPrefs.SetInt("maden." + key, value);
        Changed?.Invoke();
    }

    public static void Save() => PlayerPrefs.Save();
}
