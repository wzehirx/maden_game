using System;
using Godot;

namespace Maden.Game
{
    /// <summary>Kullanıcı ayarları (user://settings.cfg). Adımlı değerler: ekran okuyucusuz kolay değiştirilsin diye.</summary>
    public static class Settings
    {
        public static event Action Changed;

        public const int MaxStep = 10;
        static readonly float[] SpeechRates = { 0.7f, 0.85f, 1.0f, 1.15f, 1.3f, 1.5f, 1.75f, 2.0f };
        const string Path = "user://settings.cfg";
        const string Sec = "maden";

        static readonly ConfigFile Cfg = new ConfigFile();
        static bool _loaded;

        static void Load()
        {
            if (_loaded) return;
            _loaded = true;
            try { Cfg.Load(Path); } catch (Exception e) { GD.PushWarning("Ayarlar okunamadı: " + e.Message); }
        }

        static int Get(string key, int def)
        {
            Load();
            return (int)Cfg.GetValue(Sec, key, def);
        }

        static void Set(string key, int value)
        {
            Load();
            Cfg.SetValue(Sec, key, value);
            Changed?.Invoke();
        }

        public static int MusicStep { get => Get("musicStep", 5); set => Set("musicStep", Math.Clamp(value, 0, MaxStep)); }
        public static int SfxStep { get => Get("sfxStep", 8); set => Set("sfxStep", Math.Clamp(value, 0, MaxStep)); }
        public static int SpeechStep { get => Get("speechStep", 3); set => Set("speechStep", Math.Clamp(value, 0, SpeechRates.Length - 1)); }
        public static bool HapticsOn { get => Get("haptics", 1) == 1; set => Set("haptics", value ? 1 : 0); }
        public static bool TutorialDone { get => Get("tutorialDone", 0) == 1; set => Set("tutorialDone", value ? 1 : 0); }

        public static int SpeechStepCount => SpeechRates.Length;
        public static float MusicVolume => MusicStep / (float)MaxStep;
        public static float SfxVolume => SfxStep / (float)MaxStep;
        public static float SpeechRate => SpeechRates[Math.Clamp(SpeechStep, 0, SpeechRates.Length - 1)];

        public static void Save()
        {
            Load();
            var err = Cfg.Save(Path);
            if (err != Error.Ok) GD.PushWarning("Ayarlar yazılamadı: " + err);
        }
    }
}
