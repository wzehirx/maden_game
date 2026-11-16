using System;
using System.Collections.Generic;
using System.IO;
using Godot;
using Maden.Core;

namespace Maden.Game
{
    /// <summary>
    /// Oyun denetleyicisi: ekonomi + kayıt yükleme, GameSession kurulumu, Tick, otomatik kayıt,
    /// çevrimdışı kazanç, öğretici, hareket -> komut eşlemesi ve olay -> ses/konuşma/titreşim çevirisi.
    /// </summary>
    public partial class GameController : Node
    {
        public InputRouter Router;
        public Speech Speech;
        public SoundPlayer Sound;

        public GameSession Session => _session;
        /// <summary>Ekran (HUD) yeniden çizilmeli mi: hareket/olay sonrası true olur, Main temizler.</summary>
        public bool HudDirty;

        const double AutoSaveSeconds = 30.0;
        const double OfflineMinSeconds = 20.0;
        const double ResumeMinSeconds = 5.0;
        public const string EconomyPath = "res://Assets/StreamingAssets/economy.json";
        public const string SaveUserPath = "user://save.json";

        EconomyConfig _config;
        GameSession _session;
        SettingsMenu _settings;
        double _nextSave;
        bool _tutorialPlaying;
        double _tutorialEndsAt;
        bool _failed;
        bool _booted;

        readonly Dictionary<EventKind, double> _lastSpoken = new Dictionary<EventKind, double>();
        static readonly Dictionary<EventKind, double> Cooldown = new Dictionary<EventKind, double>
        {
            { EventKind.BagFull, 25.0 },
            { EventKind.NotEnoughMoney, 2.0 },
            { EventKind.MachineBlast, 6.0 },
            { EventKind.MachineDrill, 6.0 },
            { EventKind.Error, 4.0 },
        };

        static double NowSec() => Time.GetTicksMsec() / 1000.0;
        static string SavePath => ProjectSettings.GlobalizePath(SaveUserPath);

        public override void _Ready()
        {
            ProcessMode = ProcessModeEnum.Always;
            _settings = new SettingsMenu(Speech, Sound, StartTutorial);
            if (Router != null) Router.OnGesture += HandleGesture;
            Boot();
        }

        public override void _ExitTree()
        {
            if (Router != null) Router.OnGesture -= HandleGesture;
            if (_session != null) _session.OnEvent -= HandleEvent;
        }

        // ==================== Açılış ====================

        void Boot()
        {
            string json = null;
            try
            {
                using var f = Godot.FileAccess.Open(EconomyPath, Godot.FileAccess.ModeFlags.Read);
                if (f != null) json = f.GetAsText();
            }
            catch (Exception e) { GD.PushError("Ekonomi dosyası okunamadı: " + e.Message); }

            if (string.IsNullOrEmpty(json)) { Fail("Ekonomi dosyası okunamadı. Oyun başlatılamıyor."); return; }

            GameState state;
            try
            {
                _config = EconomyConfig.FromJson(json);
                state = LoadState();
                _session = new GameSession(_config, state, new SystemRandom());
                _session.OnEvent += HandleEvent;
            }
            catch (Exception e)
            {
                GD.PushError(e.ToString());
                Fail("Oyun kurulamadı. Hata: " + e.Message);
                return;
            }

            Sound.PickaxeTier = state.PickaxeTier;
            Sound.SetLayer(state.LayerIndex);
            Sound.StartMusic();
            _nextSave = NowSec() + AutoSaveSeconds;
            _booted = true;

            string offlineSpeech = ApplyAwayTime(OfflineMinSeconds);
            if (!Speech.VoiceAvailable)
            {
                // Türkçe/herhangi ses yok: titreşim ve ses efektiyle haber ver.
                Haptics.Play(HapticKind.Error);
                Sound.PlaySfx("error");
            }
            if (!Settings.TutorialDone) StartTutorial();
            else
            {
                string welcome = "Maden. Devam ediyorsun.";
                if (!string.IsNullOrEmpty(offlineSpeech)) welcome += " " + offlineSpeech;
                Speech.Say(welcome, SpeechPriority.Critical);
            }
        }

        void Fail(string message)
        {
            _failed = true;
            GD.PushError(message);
            Speech.Say(message, SpeechPriority.Critical);
        }

        // ==================== Kayıt ====================

        GameState LoadState()
        {
            try
            {
                string text = File.Exists(SavePath) ? File.ReadAllText(SavePath) : null;
                if (SaveSystem.TryDeserialize(text, _config, out GameState s)) return s;
                string bak = SavePath + ".bak";
                if (File.Exists(bak) && SaveSystem.TryDeserialize(File.ReadAllText(bak), _config, out s)) return s;
            }
            catch (Exception e) { GD.PushWarning("Kayıt okunamadı: " + e.Message); }
            return new GameState(); // yeni oyun
        }

        public void SaveNow()
        {
            if (_session == null) return;
            try
            {
                _session.State.LastSaveUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                string json = SaveSystem.Serialize(_session.State);
                string path = SavePath;
                string tmp = path + ".tmp";
                File.WriteAllText(tmp, json);
                if (File.Exists(path))
                {
                    File.Copy(path, path + ".bak", true);
                    File.Delete(path);
                }
                File.Move(tmp, path);
                Settings.Save();
            }
            catch (Exception e) { GD.PushWarning("Kayıt yazılamadı: " + e.Message); }
        }

        /// <summary>Son görülmeden bu yana geçen süreyi çevrimdışı kazanç olarak uygular; söylenecek metni döndürür.</summary>
        string ApplyAwayTime(double minSeconds)
        {
            string result = null;
            try
            {
                long last = _session != null ? _session.State.LastSaveUnix : 0;
                long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                if (last > 0)
                {
                    double secs = now - last; // saat geri alınırsa negatif: ApplyOffline 0 sayar
                    if (secs >= minSeconds)
                    {
                        var r = _session.ApplyOffline(secs);
                        if (r != null && !string.IsNullOrEmpty(r.Speech)) result = r.Speech;
                    }
                }
                if (_session != null) _session.State.LastSaveUnix = now;
            }
            catch (Exception e) { GD.PushWarning("Çevrimdışı kazanç: " + e.Message); }
            return result;
        }

        public override void _Notification(int what)
        {
            if (what == NotificationApplicationPaused || what == NotificationApplicationFocusOut)
            {
                if (_booted) SaveNow();
            }
            else if (what == NotificationApplicationResumed || what == NotificationApplicationFocusIn)
            {
                if (!_booted) return;
                string s = ApplyAwayTime(ResumeMinSeconds);
                if (!string.IsNullOrEmpty(s)) Speech.Say(s, SpeechPriority.Critical);
            }
            else if (what == NotificationWMCloseRequest)
            {
                if (_booted) { SaveNow(); Settings.Save(); }
            }
        }

        // ==================== Döngü ====================

        public override void _Process(double delta)
        {
            if (_session == null || _failed) return;
            try { _session.Tick(Math.Min(delta, 1.0)); }
            catch (Exception e) { GD.PushError(e.ToString()); }

            double now = NowSec();
            if (now >= _nextSave)
            {
                _nextSave = now + AutoSaveSeconds;
                SaveNow();
            }
            if (_tutorialPlaying && now >= _tutorialEndsAt && !Speech.IsBusy) _tutorialPlaying = false;
        }

        // ==================== Öğretici ====================

        void StartTutorial()
        {
            Settings.TutorialDone = true;
            _tutorialPlaying = true;
            Speech.Say(string.Join(" ", Tutorial.Lines), SpeechPriority.Critical);
            _tutorialEndsAt = NowSec() + 1.0; // konuşma süre tahmini dolunca IsBusy düşer ve öğretici biter
        }

        // ==================== Hareket -> komut ====================

        void HandleGesture(Gesture g)
        {
            if (_session == null || _failed) return;
            HudDirty = true;
            try
            {
                if (_tutorialPlaying)
                {
                    // Herhangi bir hareket öğreticiyi atlar (o hareket tüketilir).
                    _tutorialPlaying = false;
                    Speech.Stop();
                    Speech.Say(Tutorial.Skipped, SpeechPriority.Critical);
                    return;
                }

                if (_settings.IsOpen)
                {
                    if (g == Gesture.ThreeFingerTap) _settings.Close();
                    else _settings.Handle(g);
                    return;
                }

                bool shop = _session.ShopOpen;
                switch (g)
                {
                    case Gesture.Tap:
                        if (shop) SpeakResult(_session.ReadShopItem());
                        else _session.Tap();
                        break;
                    case Gesture.DoubleTap:
                        // Dükkân dışında gecikme/ek eylem yok: iki dokunuş zaten iki kez kazdı (Tap anında tetiklenir).
                        // Dükkânda: önceki okumayı kes, satın alma sonucu hemen söylensin.
                        if (shop) { Speech.Stop(); _session.ShopBuy(); }
                        break;
                    case Gesture.SwipeDown: _session.SellAll(); break;
                    case Gesture.SwipeUp: _session.ToggleShop(); break; // dükkân içinde de: kapatır
                    case Gesture.SwipeLeft: if (shop) _session.ShopPrev(); break;
                    case Gesture.SwipeRight: if (shop) _session.ShopNext(); break;
                    case Gesture.TwoFingerTap: SpeakResult(_session.ReadStatus()); break;
                    case Gesture.TwoFingerSwipeLeft: if (shop) _session.ShopPrevCategory(); break;
                    case Gesture.TwoFingerSwipeRight: if (shop) _session.ShopNextCategory(); break;
                    case Gesture.ThreeFingerTap: _settings.Open(); break;
                }
            }
            catch (Exception e)
            {
                GD.PushError(e.ToString());
                Speech.Say("Bir hata oluştu.", SpeechPriority.Normal);
            }
        }

        void SpeakResult(string text)
        {
            if (!string.IsNullOrEmpty(text)) Speech.Say(text, SpeechPriority.Critical);
        }

        // ==================== Olay -> ses / konuşma / titreşim ====================

        void HandleEvent(GameEvent ev)
        {
            if (ev == null) return;
            HudDirty = true;
            try { Dispatch(ev); }
            catch (Exception e) { GD.PushError(e.ToString()); }
        }

        bool CooldownOk(EventKind kind)
        {
            if (!Cooldown.TryGetValue(kind, out double cd)) return true;
            double now = NowSec();
            if (_lastSpoken.TryGetValue(kind, out double last) && now - last < cd) return false;
            _lastSpoken[kind] = now;
            return true;
        }

        void Dispatch(GameEvent ev)
        {
            switch (ev.Kind)
            {
                case EventKind.Hit:
                    bool crit = ev.Rarity != Rarity.Common; // Core: kritik vuruş = Uncommon
                    Sound.PlayHit(ev.Layer, crit ? 1.08f : 1f);
                    Haptics.Play(crit ? HapticKind.Hard : Haptics.ForHitGroup(SoundPlayer.GroupOf(ev.Layer)));
                    return; // kazma vuruşlarında konuşma yok

                case EventKind.ItemFound:
                    // Titreşim yok: vuruş titreşimini ezmesin. Konuşma yalnızca kuyruk boşken (Speech kuralı).
                    Speech.Say(ev.Speech, SpeechPriority.Normal);
                    return;

                case EventKind.RareFound:
                    Sound.PlaySfx("rare_find");
                    Haptics.Play(HapticKind.Rare);
                    Speech.Say(ev.Speech, SpeechPriority.High);
                    return;

                case EventKind.AncientTreasureFound:
                    Sound.PlaySfx("legend_find");
                    Sound.PlaySfx("fanfare");
                    Haptics.Play(HapticKind.Rare);
                    Speech.Say(ev.Speech, SpeechPriority.Critical);
                    return;

                case EventKind.Sold:
                    Sound.PlaySfx("sell");
                    Haptics.Play(HapticKind.Sell);
                    Speech.Say(ev.Speech, SpeechPriority.High);
                    return;

                case EventKind.BagFull:
                    if (!CooldownOk(ev.Kind)) return;
                    Sound.PlaySfx("error", 0.7f, 0.9f);
                    Haptics.Play(HapticKind.Error);
                    Speech.Say(ev.Speech, SpeechPriority.Normal);
                    return;

                case EventKind.LayerChanged:
                    Sound.SetLayer(ev.Layer);
                    Sound.PlaySfx("fanfare");
                    Speech.Say(ev.Speech, SpeechPriority.Critical);
                    return;

                case EventKind.UpgradeBought:
                    bool tierUp = _session.State.PickaxeTier != Sound.PickaxeTier;
                    Sound.PickaxeTier = _session.State.PickaxeTier;
                    Sound.PlaySfx(tierUp ? "upgrade" : "buy");
                    Haptics.Play(HapticKind.Sell);
                    Speech.Say(ev.Speech, SpeechPriority.High);
                    return;

                case EventKind.NotEnoughMoney:
                    if (!CooldownOk(ev.Kind)) return;
                    Sound.PlaySfx("error");
                    Haptics.Play(HapticKind.Error);
                    Speech.Say(ev.Speech, SpeechPriority.High);
                    return;

                case EventKind.MachineBlast:
                    Sound.PlayDynamite();
                    Haptics.Play(HapticKind.Blast);
                    if (CooldownOk(ev.Kind)) Speech.Say(ev.Speech, SpeechPriority.Low);
                    return;

                case EventKind.MachineDrill:
                    Sound.PlayDrill();
                    Haptics.Play(HapticKind.Metal);
                    if (CooldownOk(ev.Kind)) Speech.Say(ev.Speech, SpeechPriority.Low);
                    return;

                case EventKind.ShopOpened:
                    Sound.PlaySfx("menu_open");
                    Speech.Say(ev.Speech, SpeechPriority.Critical);
                    return;

                case EventKind.ShopClosed:
                    Sound.PlaySfx("menu_close");
                    Speech.Say(ev.Speech, SpeechPriority.Critical);
                    return;

                case EventKind.ShopItemChanged:
                    Haptics.Play(HapticKind.Tick);
                    Speech.Say(ev.Speech, SpeechPriority.Critical);
                    return;

                case EventKind.Error:
                    if (!CooldownOk(ev.Kind)) return;
                    Sound.PlaySfx("error");
                    Haptics.Play(HapticKind.Error);
                    Speech.Say(ev.Speech, SpeechPriority.Normal);
                    return;

                default:
                    Speech.Say(ev.Speech, SpeechPriority.Normal);
                    return;
            }
        }
    }
}
