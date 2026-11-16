using System;
using System.Linq;
using Godot;

namespace Maden.Game
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
    /// DisplayServer TTS sarmalayıcısı (Android'de sistemin Türkçe motoru). Kuyruk, kendi süre tahminiyle
    /// sınırlanır; böylece saatlerce dinlerken konuşma yığılmaz. Ses yoksa sessizce günlüğe düşer.
    /// </summary>
    public partial class Speech : Node
    {
        public static Speech Instance { get; private set; }

        public string LastText { get; private set; } = "";

        /// <summary>Çalışan bir Türkçe (veya yedek) ses bulundu mu.</summary>
        public bool VoiceAvailable { get; private set; }

        /// <summary>Seçilen sesin Türkçe olup olmadığı.</summary>
        public bool VoiceIsTurkish { get; private set; }

        /// <summary>Konuşma sürüyor mu (müzik kısmak için).</summary>
        public bool IsBusy => NowSec() < _busyUntil;

        const float SecondsPerChar = 0.075f;

        string _voiceId = "";
        double _busyUntil;
        string _lastDedupText = "";
        double _lastDedupTime = -10;
        long _utteranceId;
        double _nextPoll;
        double _lastSayTime = -10;

        static double NowSec() => Time.GetTicksMsec() / 1000.0;

        public override void _Ready()
        {
            Instance = this;
            PickVoice();
        }

        void PickVoice()
        {
            try
            {
                var tr = DisplayServer.TtsGetVoicesForLanguage("tr");
                if (tr != null && tr.Length > 0)
                {
                    _voiceId = tr[0]; VoiceAvailable = true; VoiceIsTurkish = true;
                    GD.Print("TTS Türkçe ses: " + _voiceId);
                    return;
                }
                var all = DisplayServer.TtsGetVoices();
                if (all != null && all.Count > 0)
                {
                    string id = null;
                    foreach (var v in all)
                    {
                        var lang = v.ContainsKey("language") ? v["language"].AsString() : "";
                        if (lang.StartsWith("tr", StringComparison.OrdinalIgnoreCase)) { id = v["id"].AsString(); VoiceIsTurkish = true; break; }
                    }
                    if (id == null)
                    {
                        id = all[0]["id"].AsString();
                        GD.PushWarning("Türkçe TTS sesi yok; varsayılan ses kullanılıyor: " + id);
                    }
                    _voiceId = id; VoiceAvailable = true;
                    return;
                }
                GD.PushWarning("Hiç TTS sesi yok; konuşma günlüğe yazılacak, titreşim ve ses efektiyle haber verilecek.");
            }
            catch (Exception e) { GD.PushWarning("TTS başlatılamadı: " + e.Message); }
            VoiceAvailable = false;
        }

        public override void _Process(double delta)
        {
            double now = NowSec();
            if (!VoiceAvailable || now < _nextPoll) return;
            _nextPoll = now + 0.25;
            // Tahmini süre gerçekte bittiyse düzelt (kısa bir ilk-başlama toleransıyla).
            if (now - _lastSayTime > 0.8 && now < _busyUntil)
            {
                try { if (!DisplayServer.TtsIsSpeaking()) _busyUntil = now; } catch { }
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
                case SpeechPriority.Normal: if (backlog > 2.5) return; break;
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
                // Ses yoksa en az titreşimle haber ver (önemli mesajlarda).
                if (priority >= SpeechPriority.High) Haptics.Play(HapticKind.Tick);
                return;
            }
            try
            {
                _utteranceId++;
                // volume 0..100, pitch 1, rate = konuşma hızı ayarı
                DisplayServer.TtsSpeak(text, _voiceId, 100, 1f, Settings.SpeechRate, _utteranceId, flush);
            }
            catch (Exception e) { GD.PushWarning("TTS speak: " + e.Message); }
        }

        /// <summary>Konuşmayı hemen keser.</summary>
        public void Stop()
        {
            _busyUntil = 0;
            if (!VoiceAvailable) return;
            try { DisplayServer.TtsStop(); } catch { }
        }
    }
}
