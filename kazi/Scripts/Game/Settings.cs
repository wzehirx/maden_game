using System;
using Godot;

namespace Kazi.Game
{
    /// <summary>
    /// Cihaz ayarları (user://settings.cfg): ses seviyeleri, konuşma motoru/sesi, titreşim, öğretici bayrağı.
    /// Oyunla ilgili ayarlar (konuşma hızı/ayrıntısı, kazıma modu, bildirim) GameState içindedir ve kayıtla saklanır;
    /// konuşma hızının çalışma anındaki kopyası burada (SpeechRate) tutulur, GameController eşler.
    /// </summary>
    public static class Settings
    {
        public static event Action Changed;

        /// <summary>Kendini sınama: dosyaya yazma.</summary>
        public static bool Dry;

        public const int MaxStep = 10;
        const string Path = "user://settings.cfg";
        const string Sec = "kazi";

        static readonly ConfigFile Cfg = new ConfigFile();
        static bool _loaded;

        static void Load()
        {
            if (_loaded) return;
            _loaded = true;
            if (Dry) return;
            try { Cfg.Load(Path); } catch (Exception e) { GD.PushWarning("Ayarlar okunamadı: " + e.Message); }
        }

        static int Get(string key, int def) { Load(); return (int)Cfg.GetValue(Sec, key, def); }
        static string GetStr(string key, string def) { Load(); return Cfg.GetValue(Sec, key, def).AsString(); }
        static void SetStr(string key, string value) { Load(); Cfg.SetValue(Sec, key, value); Changed?.Invoke(); }
        static void Set(string key, int value) { Load(); Cfg.SetValue(Sec, key, value); Changed?.Invoke(); }

        public static int MusicStep { get => Get("musicStep", 5); set => Set("musicStep", Math.Clamp(value, 0, MaxStep)); }
        public static int SfxStep { get => Get("sfxStep", 8); set => Set("sfxStep", Math.Clamp(value, 0, MaxStep)); }
        /// <summary>Seçilen konuşma motoru (Android paket adı). Boş = telefonun varsayılan motoru.</summary>
        public static string EngineId { get => GetStr("engineId", ""); set => SetStr("engineId", value ?? ""); }
        /// <summary>Seçilen konuşma sesi kimliği. Boş = seçili motorun varsayılan sesi.</summary>
        public static string VoiceId { get => GetStr("voiceId", ""); set => SetStr("voiceId", value ?? ""); }
        public static bool HapticsOn { get => Get("haptics", 1) == 1; set => Set("haptics", value ? 1 : 0); }
        public static bool TutorialDone { get => Get("tutorialDone", 0) == 1; set => Set("tutorialDone", value ? 1 : 0); }

        /// <summary>Konuşma hızı (0.5-3.0). Kaynağı GameState.SpeechRate; GameController açılışta ve değişimde eşler.</summary>
        public static float SpeechRate { get; set; } = 1.5f;

        public static float MusicVolume => MusicStep / (float)MaxStep;
        public static float SfxVolume => SfxStep / (float)MaxStep;

        public static void Save()
        {
            if (Dry) return;
            Load();
            var err = Cfg.Save(Path);
            if (err != Error.Ok) GD.PushWarning("Ayarlar yazılamadı: " + err);
        }
    }
}
