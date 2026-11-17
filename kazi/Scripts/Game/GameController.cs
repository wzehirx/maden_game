using System;
using System.Collections.Generic;
using System.IO;
using Godot;
using Kazi.Core;

namespace Kazi.Game
{
    /// <summary>Dükkân ekranı için hazır görünüm (Main çizer).</summary>
    public sealed class ShopView
    {
        public string CategoryName = "";
        public int CategoryPos, CategoryCount, Pos, Total;
        public ShopItem Entry;
        public string Desc = "", PriceLine = "";
        public bool Affordable = true;
        public long Shortfall;
        public bool UsesAB;
    }

    /// <summary>
    /// Oyun denetleyicisi: yapılandırma + kayıt yükleme, GameSession kurulumu, Tick, otomatik kayıt, çevrimdışı kazanç,
    /// öğretici, hareket -> komut eşlemesi, sürükleme kazıma ses/dokunsal tasarımı ve olay -> ses/konuşma/titreşim eşlemesi.
    /// </summary>
    public partial class GameController : Node
    {
        public InputRouter Router;
        public Speech Speech;
        public SoundPlayer Sound;
        /// <summary>Kendini sınama: yeni oyun, ayrı kayıt dosyası.</summary>
        public bool SelfTest;

        public GameSession Session => _session;
        public SettingsMenu Menu { get; private set; }
        /// <summary>Ekran yeniden çizilmeli mi: hareket/olay sonrası true olur, Main temizler.</summary>
        public bool HudDirty;
        public bool TutorialPlaying { get; private set; }
        public bool PrestigePrompt { get; private set; }
        public string LastResultText = "";
        public bool Booted => _booted;

        // Kazıma alanı (Main yerleşimi hesaplar; ekran pikseli).
        public static Rect2 GridRect = new Rect2(10, 200, 520, 740);

        const double AutoSaveSeconds = 30.0;
        const double OfflineMinSeconds = 20.0;
        const double ResumeMinSeconds = 5.0;
        public const string SaveUserPath = "user://save.json";
        public const string SelfTestSavePath = "user://selftest_save.json";

        GameConfig _config;
        GameSession _session;
        double _nextSave;
        double _tutorialEndsAt;
        bool _failed, _booted;
        bool _quietShopClose;

        // Ses sayaçları (selftest ve tanı için)
        public int SoundsTriggered;

        readonly Dictionary<EventKind, double> _lastSpoken = new Dictionary<EventKind, double>();
        static readonly Dictionary<EventKind, double> Cooldown = new Dictionary<EventKind, double>
        {
            { EventKind.BagFull, 25.0 }, { EventKind.NotEnoughMoney, 1.5 }, { EventKind.NoCard, 1.5 }, { EventKind.Error, 1.5 },
        };

        static double NowSec() => Time.GetTicksMsec() / 1000.0;
        string SavePath => ProjectSettings.GlobalizePath(SelfTest ? SelfTestSavePath : SaveUserPath);

        // ==================== Açılış ====================

        public override void _Ready()
        {
            ProcessMode = ProcessModeEnum.Always;
            Menu = new SettingsMenu(Speech, Sound, () => _session, StartTutorial, OnSettingsChanged);
            if (Router != null)
            {
                Router.OnGesture += HandleGesture;
                Router.BeforeTouch = UpdateDragMode;
                Router.Recognizer.OnDragMove += OnDragMove;
            }
            Boot();
        }

        public override void _ExitTree()
        {
            if (Router != null) { Router.OnGesture -= HandleGesture; Router.Recognizer.OnDragMove -= OnDragMove; }
            if (_session != null) _session.OnEvent -= HandleEvent;
        }

        static string ReadRes(string path)
        {
            using var f = Godot.FileAccess.Open(path, Godot.FileAccess.ModeFlags.Read);
            if (f == null) throw new FileNotFoundException(path + " okunamadı: " + Godot.FileAccess.GetOpenError());
            return f.GetAsText();
        }

        void Boot()
        {
            GameState state;
            try
            {
                _config = GameConfig.FromJson(ReadRes("res://data/cards.json"), ReadRes("res://data/upgrades.json"),
                    ReadRes("res://data/prestige.json"), ReadRes("res://data/settings.json"));
                state = SelfTest ? SaveSystem.NewGame(_config) : LoadState();
                _session = new GameSession(_config, state);
                _session.OnEvent += HandleEvent;
            }
            catch (Exception e)
            {
                GD.PushError(e.ToString());
                Fail("Oyun kurulamadı. Hata: " + e.Message);
                return;
            }

            Settings.SpeechRate = (float)_session.SpeechRate;
            Sound.StartMusic();
            RefreshLoops();
            _nextSave = NowSec() + AutoSaveSeconds;
            _booted = true;

            string offlineSpeech = ApplyAwayTime(OfflineMinSeconds);
            if (!Speech.VoiceAvailable)
            {
                Haptics.Play(HapticKind.Error);
                Sound.PlaySfx("error_wood");
            }
            if (!Settings.TutorialDone) StartTutorial();
            else
            {
                string welcome = "Kazı Kazan. Devam ediyorsun.";
                if (!string.IsNullOrEmpty(offlineSpeech)) welcome += " " + offlineSpeech;
                Speech.Say(welcome, SpeechPriority.Critical);
            }
            HudDirty = true;
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
                if (text != null && SaveSystem.TryDeserialize(text, _config, out GameState s)) return s;
                string bak = SavePath + ".bak";
                if (File.Exists(bak) && SaveSystem.TryDeserialize(File.ReadAllText(bak), _config, out s)) return s;
            }
            catch (Exception e) { GD.PushWarning("Kayıt okunamadı: " + e.Message); }
            return SaveSystem.NewGame(_config); // yeni oyun
        }

        public void SaveNow()
        {
            if (_session == null) return;
            try
            {
                _session.FlushAutoReport();
                _session.State.LastSaveUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                string path = SavePath;
                if (File.Exists(path)) { try { File.Copy(path, path + ".bak", true); } catch { } }
                SaveSystem.SaveToFile(_session.State, path);
            }
            catch (Exception e) { GD.PushWarning("Kayıt yazılamadı: " + e.Message); }
            Settings.Save();
        }

        /// <summary>Uygulamaya dönüşte / açılışta geçen süre için çevrimdışı kazanç; konuşma cümlesi döner.</summary>
        string ApplyAwayTime(double minSeconds)
        {
            string result = "";
            try
            {
                long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                long last = _session.State.LastSaveUnix;
                if (last > 0 && now - last >= minSeconds)
                {
                    var off = _session.ApplyOfflineSince(now);
                    result = off.Speech ?? "";
                    RefreshLoops();
                }
                _session.State.LastSaveUnix = now;
            }
            catch (Exception e) { GD.PushWarning("Çevrimdışı kazanç: " + e.Message); }
            return result;
        }

        public override void _Notification(int what)
        {
            if (what == NotificationApplicationPaused || what == NotificationApplicationFocusOut)
            {
                if (_booted && !SelfTest) SaveNow();
            }
            else if (what == NotificationApplicationResumed || what == NotificationApplicationFocusIn)
            {
                if (!_booted || SelfTest) return;
                string s = ApplyAwayTime(ResumeMinSeconds);
                if (!string.IsNullOrEmpty(s)) Speech.Say(s, SpeechPriority.Critical);
                HudDirty = true;
            }
            else if (what == NotificationWMCloseRequest)
            {
                if (_booted) SaveNow();
            }
        }

        // ==================== Döngü ====================

        public override void _Process(double delta)
        {
            if (_session == null || _failed) return;
            try { _session.Tick(Math.Min(delta, 5.0)); }
            catch (Exception e) { GD.PushError(e.ToString()); }

            double now = NowSec();
            if (now >= _nextSave)
            {
                _nextSave = now + AutoSaveSeconds;
                if (!SelfTest) SaveNow();
            }
            if (TutorialPlaying && now >= _tutorialEndsAt && !Speech.IsBusy) { TutorialPlaying = false; HudDirty = true; }
        }

        // ==================== Öğretici ====================

        public void StartTutorial()
        {
            Settings.TutorialDone = true;
            TutorialPlaying = true;
            Speech.Say(string.Join(" ", Tutorial.Lines), SpeechPriority.Critical);
            _tutorialEndsAt = NowSec() + 1.0; // konuşma bitince (IsBusy düşünce) öğretici biter
            HudDirty = true;
        }

        void StopTutorial()
        {
            TutorialPlaying = false;
            Speech.Stop();
            Speech.Say(Tutorial.Skipped, SpeechPriority.Critical);
            HudDirty = true;
        }

        void OnSettingsChanged()
        {
            if (_session != null) Settings.SpeechRate = (float)_session.SpeechRate;
            HudDirty = true;
        }

        // ==================== Hareket -> komut ====================

        bool DragCardActive()
        {
            if (_session == null) return false;
            var c = _session.State.Current;
            return _session.Mode == ScratchMode.Drag && c != null && !c.Done && !_session.ShopOpen
                && !Menu.IsOpen && !PrestigePrompt && !TutorialPlaying;
        }

        /// <summary>Dokunuş başlarken: sürükleme kazıma yalnızca kazıma alanında başlayan dokunuşlarda açık; üst bölgeden
        /// başlayan kaydırmalar (dükkân, kartı bırak) normal hareket sayılır.</summary>
        void UpdateDragMode(float x, float y)
        {
            Router.Recognizer.DragMode = DragCardActive() && GridRect.HasPoint(new Vector2(x, y));
            _lastCell = -2; _speedEma = 0; _lastMoveT = -10;
        }

        void HandleGesture(Gesture g)
        {
            HudDirty = true;
            try { Route(g); }
            catch (Exception e) { GD.PushError("Hareket: " + e); }
        }

        void Route(Gesture g)
        {
            if (_session == null || _failed) return;
            if (PrestigePrompt && !_session.PrestigeOffered) PrestigePrompt = false;

            if (TutorialPlaying)
            {
                if (g == Gesture.ThreeFingerTap) { TutorialPlaying = false; Speech.Stop(); Menu.Open(); }
                else if (g != Gesture.DoubleTap && g != Gesture.TwoFingerTap) StopTutorial();
                return;
            }
            if (Menu.IsOpen)
            {
                if (g == Gesture.ThreeFingerTap) Menu.Close();
                else if (g != Gesture.DoubleTap) Menu.Handle(g);
                return;
            }
            if (g == Gesture.ThreeFingerTap) { Menu.Open(); return; }
            if (g == Gesture.TwoFingerTap) { _session.ReadStatus(); return; }

            if (PrestigePrompt)
            {
                switch (g)
                {
                    case Gesture.DoubleTap: PrestigePrompt = false; _session.AcceptPrestige(); break;
                    case Gesture.SwipeDown: PrestigePrompt = false; _session.DeclinePrestige(); break;
                    case Gesture.Tap: Speech.Say(PrestigeText(_session.PrestigeGain()), SpeechPriority.Critical); break;
                }
                return;
            }

            if (_session.ShopOpen) RouteShop(g); else RouteCard(g);
        }

        void RouteCard(Gesture g)
        {
            switch (g)
            {
                case Gesture.Tap:
                    // Kart az önce bittiyse (parmak kalkarken gelen artçı dokunuş) hata sesi çıkarma.
                    if (NowSec() - _lastResultAt < 0.6) break;
                    if (_session.Mode == ScratchMode.Drag && DragCardActive()) DragTap();
                    else _session.ScratchTap();
                    break;
                case Gesture.SwipeUp: _session.ToggleShop(); break;
                case Gesture.SwipeDown:
                    {
                        var c = _session.State.Current;
                        if (c != null && !c.Done && c.Type.IsTrapType) _session.DiscardCard();
                        else _session.NextCard();
                        break;
                    }
            }
        }

        void RouteShop(Gesture g)
        {
            switch (g)
            {
                case Gesture.SwipeRight: _session.ShopNext(); break;
                case Gesture.SwipeLeft: _session.ShopPrev(); break;
                case Gesture.TwoFingerSwipeRight: _session.ShopNextCategory(); break;
                case Gesture.TwoFingerSwipeLeft: _session.ShopPrevCategory(); break;
                case Gesture.SwipeUp: case Gesture.SwipeDown: _session.ToggleShop(); break;
                case Gesture.DoubleTap:
                    {
                        var cur = _session.State.Current;
                        bool hadActive = cur != null && !cur.Done;
                        bool cardCat = _session.ShopCategory == 0;
                        bool ok = _session.ShopBuy();
                        // Kart alınıp eline geçtiyse dükkân kapanır: hemen kazımaya geçilir.
                        if (ok && cardCat && !hadActive && _session.ShopOpen)
                        {
                            _quietShopClose = true;
                            _session.ToggleShop();
                            _quietShopClose = false;
                        }
                        break;
                    }
            }
        }

        // ==================== Sürükleme kazıma ====================

        int _lastCell = -2;
        double _lastMoveT = -10, _speedEma;
        double _lastScratchSnd = -10, _lastBoundary = -10, _lastDragHaptic = -10;

        /// <summary>Ekran noktasının kazıma ızgarasındaki hücre numarası (-1: alan dışı).</summary>
        public int CellAt(float x, float y)
        {
            var c = _session?.State.Current;
            if (c == null) return -1;
            int cols = Math.Max(1, c.Type.Cols);
            int rows = Math.Max(1, (c.Type.Cells + cols - 1) / cols);
            var g = GridRect;
            if (!g.HasPoint(new Vector2(x, y))) return -1;
            int col = Math.Clamp((int)((x - g.Position.X) / (g.Size.X / cols)), 0, cols - 1);
            int row = Math.Clamp((int)((y - g.Position.Y) / (g.Size.Y / rows)), 0, rows - 1);
            int idx = row * cols + col;
            return idx < c.Type.Cells ? idx : -1;
        }

        float CellDiameter()
        {
            var c = _session.State.Current;
            int cols = Math.Max(1, c.Type.Cols);
            int rows = Math.Max(1, (c.Type.Cells + cols - 1) / cols);
            return Math.Max(20f, Math.Min(GridRect.Size.X / cols, GridRect.Size.Y / rows));
        }

        float Pan01(float x) => Mathf.Clamp((x - GridRect.Position.X) / Math.Max(1f, GridRect.Size.X), 0f, 1f);

        void DragTap()
        {
            var c = _session.State.Current;
            var p = Router.LastPos;
            int cell = CellAt(p.X, p.Y);
            if (cell < 0 || c == null) return;
            if (c.Open[cell]) { Sound.PlayBoundary(Pan01(p.X)); return; }
            Sound.PlayScratch(0.5f, Pan01(p.X));
            Haptics.Play(HapticKind.Tick);
            _session.ScratchDrag(cell, 0.3);
        }

        /// <summary>Parmak hareketi: yol, hücre sınırlarına bölünerek ilgili hücreyi kazır; ses/titreşim burada tetiklenir.</summary>
        void OnDragMove(DragMove m)
        {
            if (!DragCardActive()) return;
            var card = _session.State.Current;
            double now = NowSec();

            // Parmak hızı (px/sn), üstel ortalama; uzun duraksamadan sonra sıfırdan.
            double dt = Math.Max(0.004, m.Time - _lastMoveT);
            if (_lastMoveT < -5 || m.Time - _lastMoveT > 0.15) _speedEma = 0;
            double v = m.Distance / dt;
            _speedEma = _speedEma <= 0 ? v : _speedEma * 0.6 + v * 0.4;
            _lastMoveT = m.Time;
            float diam = CellDiameter();
            float speed01 = Mathf.Clamp((float)(_speedEma / (diam * 7.0)), 0f, 1f);

            // Yolu küçük adımlara böl (hızlı hareket hücre atlamasın).
            float segLen = m.Distance;
            int steps = Math.Max(1, (int)Math.Ceiling(segLen / (diam * 0.25f)));
            float stepLen = segLen / steps;
            bool scratchedClosed = false;
            for (int i = 1; i <= steps; i++)
            {
                if (card.Done) break;
                float t = i / (float)steps;
                float x = m.PrevX + (m.X - m.PrevX) * t, y = m.PrevY + (m.Y - m.PrevY) * t;
                int cell = CellAt(x, y);
                if (cell != _lastCell)
                {
                    if (_lastCell != -2 && now - _lastBoundary >= 0.03)
                    {
                        _lastBoundary = now;
                        Sound.PlayBoundary(Pan01(x));
                        if (now - _lastDragHaptic >= 0.05) { _lastDragHaptic = now; Haptics.Play(HapticKind.Soft); }
                    }
                    _lastCell = cell;
                }
                if (cell >= 0 && !card.Open[cell])
                {
                    scratchedClosed = true;
                    // Bir hücreyi yaklaşık 2,2 hücre çapı yolla (iki üç sürtme) kazımak eşiği geçirir.
                    _session.ScratchDrag(cell, stepLen / (diam * 2.2f));
                }
            }

            if (scratchedClosed)
            {
                double gap = 0.030 + (1.0 - speed01) * 0.045; // hızlı: 30 ms, yavaş: 75 ms
                if (now - _lastScratchSnd >= gap)
                {
                    _lastScratchSnd = now;
                    Sound.PlayScratch(speed01, Pan01(m.X));
                    SoundsTriggered++;
                }
                if (now - _lastDragHaptic >= 0.06) { _lastDragHaptic = now; Haptics.Play(HapticKind.Tick); }
            }
            HudDirty = true;
        }

        // ==================== Olay -> ses / konuşma / titreşim ====================

        double _lastCellAt = -10, _lastAutoSnd = -10, _lastResultAt = -10;
        int _cellStep;
        readonly Dictionary<string, double> _flavorAt = new Dictionary<string, double>();

        // Otomasyon/gadget kimliği -> ayırt edilebilir tek seferlik ses (sürekli döngüler RefreshLoops'ta).
        static readonly string[][] Flavor =
        {
            new[] { "oto_kuyruk", "gadget_steam_gear" }, new[] { "oto_kuyruk2", "gadget_steam_gear" },
            new[] { "oto_alim", "gadget_register" }, new[] { "oto_tur", "gadget_sewing_motor" },
            new[] { "oto_hiz2", "gadget_fan" }, new[] { "oto_hiz3", "gadget_drill" },
            new[] { "pres", "gadget_press" }, new[] { "pres2", "gadget_press" },
            new[] { "cikrik", "gadget_crank_gears" }, new[] { "yonca", "gadget_crank_wood" },
            new[] { "zil", "counter_bell" }, new[] { "zil2", "counter_bell" },
            new[] { "mercek", "gadget_typewriter" },
        };

        static string FlavorOf(string id)
        {
            foreach (var f in Flavor) if (f[0] == id) return f[1];
            switch (id)
            {
                case "oto_kaziyici": return "card_riffle";
                case "oto_hiz": return "gadget_steam_gear";
                case "sayac": return "gadget_typewriter";
            }
            return null;
        }

        /// <summary>Sürekli döngü sesleri: otomatik kazıyıcı, bant (hız), mekanik sayaç. Her biri ayrı ses.</summary>
        public void RefreshLoops()
        {
            if (_session == null || Sound == null) return;
            var st = _session.State;
            bool auto = _session.Stats.AutoOwned;
            Sound.SetLoop("auto_scratch_loop", auto);
            Sound.SetLoop("conveyor_loop", auto && st.Level("oto_hiz") > 0);
            Sound.SetLoop("counter_ticks_loop", st.Level("sayac") > 0);
        }

        void AutoFlavor(bool win)
        {
            double now = NowSec();
            var played = new HashSet<string>();
            foreach (var f in Flavor)
            {
                if (_session.State.Level(f[0]) <= 0 || played.Contains(f[1])) continue;
                if (f[1] == "counter_bell" && !win) continue;
                played.Add(f[1]);
                if (_flavorAt.TryGetValue(f[1], out double at) && now - at < 6.5) continue;
                _flavorAt[f[1]] = now;
                Sound.PlaySfxLater(f[1], 0.05 + played.Count * 0.12, 0.5f);
                break; // her otomatik sonuçta en fazla bir gadget aromasi
            }
        }

        void Say(GameEvent e, SpeechPriority p)
        {
            if (!string.IsNullOrEmpty(e.Speech)) Speech.Say(e.Speech, p);
        }

        bool Cooled(EventKind k)
        {
            double now = NowSec();
            if (Cooldown.TryGetValue(k, out double cd) && _lastSpoken.TryGetValue(k, out double at) && now - at < cd) return false;
            _lastSpoken[k] = now;
            return true;
        }

        string PrestigeText(long gain) =>
            "Altın Bilet tuttu! Sıfırlarsan " + gain + " Altın Bilet puanı kazanırsın. Her şey sıfırlanır, kalıcı ağaç kalır. " +
            "Kabul etmek için çift dokunuş. Reddetmek için aşağı kaydır.";

        void HandleEvent(GameEvent e)
        {
            HudDirty = true;
            SoundsTriggered++;
            try { Dispatch(e); }
            catch (Exception ex) { GD.PushError("Olay işleyicisi " + e.Kind + ": " + ex); }
        }

        void Dispatch(GameEvent e)
        {
            double now = NowSec();
            switch (e.Kind)
            {
                case EventKind.CellOpened:
                    {
                        var card = _session.State.Current;
                        if (now - _lastCellAt < 0.12) _cellStep = Math.Min(_cellStep + 1, 5); else _cellStep = 0;
                        _lastCellAt = now;
                        double delay = _cellStep * 0.07;
                        Sound.PlaySfxLater("cell_reveal", delay, 1f, 1f + 0.04f * _cellStep);
                        bool trap = card != null && e.CellIndex >= 0 && card.IsTrapCell(e.CellIndex);
                        if (trap)
                        {
                            Sound.PlaySfxLater("trap_crack", delay + 0.05, 0.8f);
                            Haptics.Play(HapticKind.Peek);
                        }
                        else
                        {
                            if (card != null && e.CellIndex >= 0 && card.SymbolAt(e.CellIndex) != null)
                            {
                                int same = 0;
                                for (int i = 0; i < card.Cells.Length; i++)
                                    if (i != e.CellIndex && card.Open[i] && card.Cells[i] == card.Cells[e.CellIndex] && card.SymbolAt(i) != null) same++;
                                if (same > 0) Sound.PlaySfxLater("match_tick", delay + 0.08, 1f, 1f + 0.12f * (same - 1));
                            }
                            if (_cellStep == 0) Haptics.Play(_session.Mode == ScratchMode.Drag ? HapticKind.Soft : HapticKind.Tick);
                        }
                        if (!string.IsNullOrEmpty(e.Speech))
                            Speech.Say("Hücre " + (e.CellIndex + 1) + ", " + e.Speech, SpeechPriority.Normal);
                        break;
                    }
                case EventKind.CellPeek:
                    if (e.Symbol == "tuzak") { Sound.PlaySfx("trap_glass", 0.5f, 0.9f); Haptics.Play(HapticKind.Peek); }
                    Say(e, SpeechPriority.High);
                    break;
                case EventKind.CardBought:
                    _lastResultAt = -10;
                    Sound.PlaySfx("ticket_tear");
                    Sound.PlaySfxLater("coin_slide", 0.12, 0.8f);
                    Haptics.Play(HapticKind.Buy);
                    Say(e, SpeechPriority.High);
                    break;
                case EventKind.TrapHit:
                    Sound.PlaySfxLater("trap_hit", 0.1);
                    Haptics.Play(HapticKind.Trap);
                    break;
                case EventKind.CardResult:
                    _lastResultAt = now;
                    PlayResult(e);
                    LastResultText = e.Speech;
                    Say(e, SpeechPriority.Critical);
                    break;
                case EventKind.CardDiscarded:
                    Sound.PlaySfx("paper_crumple");
                    Sound.PlaySfxLater("paper_bin_drop", 0.3, 0.8f);
                    Haptics.Play(HapticKind.Soft);
                    Say(e, SpeechPriority.High);
                    break;
                case EventKind.AutoResult:
                    PlayAutoResult(e, now);
                    break;
                case EventKind.AutoReport:
                    Say(e, SpeechPriority.Low);
                    break;
                case EventKind.NotEnoughMoney:
                    if (!Cooled(e.Kind)) break;
                    Sound.PlaySfx("error_wood");
                    Haptics.Play(HapticKind.Error);
                    Say(e, SpeechPriority.High);
                    break;
                case EventKind.NoCard: case EventKind.Error: case EventKind.BagFull:
                    if (!Cooled(e.Kind)) break;
                    Sound.PlaySfx("error_wood", 0.7f);
                    Haptics.Play(HapticKind.Error);
                    Say(e, SpeechPriority.High);
                    break;
                case EventKind.UpgradeBought:
                    Sound.PlaySfx("buy_unlock");
                    Sound.PlaySfxLater("coin_single", 0.15, 0.8f);
                    Haptics.Play(HapticKind.Buy);
                    RefreshLoops();
                    Say(e, SpeechPriority.High);
                    break;
                case EventKind.GadgetBought: case EventKind.AutomationBought:
                    {
                        string fl = FlavorOf(e.Symbol);
                        Sound.PlaySfx("buy_lock");
                        if (fl != null) Sound.PlaySfxLater(fl, 0.2, 0.9f);
                        Haptics.Play(HapticKind.WinMid);
                        RefreshLoops();
                        Say(e, SpeechPriority.High);
                        break;
                    }
                case EventKind.UpgradeAffordable:
                    Sound.PlaySfx("cash_drawer", 0.8f);
                    Haptics.Play(HapticKind.Soft);
                    Say(e, SpeechPriority.High);
                    break;
                case EventKind.HelpMoney:
                    Sound.PlaySfx("help_money_chime");
                    Sound.PlaySfxLater("coins_drop", 0.2, 0.8f);
                    Haptics.Play(HapticKind.WinSmall);
                    Say(e, SpeechPriority.High);
                    break;
                case EventKind.CatalogUnlocked:
                    Sound.PlaySfx("catalog_unlock_fanfare");
                    Haptics.Play(HapticKind.WinBig);
                    Say(e, SpeechPriority.High);
                    break;
                case EventKind.PrestigeOffer:
                    Sound.PlaySfxLater("prestige_swell", 0.4);
                    Haptics.Play(HapticKind.Jackpot);
                    PrestigePrompt = true;
                    Speech.Say(PrestigeText(e.Amount), SpeechPriority.High);
                    break;
                case EventKind.PrestigeDone:
                    PrestigePrompt = false;
                    Sound.PlaySfx("catalog_unlock_fanfare");
                    Sound.PlaySfxLater("coins_drop", 0.3);
                    Haptics.Play(HapticKind.Jackpot);
                    RefreshLoops();
                    Say(e, SpeechPriority.Critical);
                    break;
                case EventKind.NodeBought:
                    Sound.PlaySfx("buy_unlock");
                    Sound.PlaySfxLater("buy_lock", 0.2, 0.8f);
                    Haptics.Play(HapticKind.Buy);
                    RefreshLoops();
                    Say(e, SpeechPriority.High);
                    break;
                case EventKind.OfflineReport:
                    Sound.PlaySfx("coin_count", 0.8f);
                    break; // cümle açılış/dönüş konuşmasında okunur
                case EventKind.ShopOpened:
                    Sound.PlaySfx("menu_paper_open");
                    SayShop(e, "Dükkân. ");
                    break;
                case EventKind.ShopClosed:
                    if (_quietShopClose) break;
                    Sound.PlaySfx("page_slide");
                    Say(e, SpeechPriority.Critical);
                    break;
                case EventKind.ShopItemChanged:
                    Sound.PlaySfx("page_slide", 0.7f);
                    Haptics.Play(HapticKind.Tick);
                    SayShop(e, "");
                    break;
                case EventKind.ShopCategoryChanged:
                    Sound.PlaySfx("menu_whoosh");
                    SayShop(e, null);
                    break;
                case EventKind.Status:
                    Sound.PlaySfx("page_turn_2");
                    Say(e, SpeechPriority.Critical);
                    break;
                case EventKind.Info:
                    Say(e, SpeechPriority.Critical);
                    break;
            }
        }

        // ---------- Sonuç sesleri ----------

        void PlayResult(GameEvent e)
        {
            switch (e.Rarity)
            {
                case Rarity.Small:
                    Sound.PlaySfx("win_chime_1");
                    Sound.PlaySfxLater("coin_single", 0.2, 0.8f);
                    Haptics.Play(HapticKind.WinSmall);
                    break;
                case Rarity.Medium:
                    Sound.PlaySfx("win_chime_2");
                    Sound.PlaySfxLater("coins_drop", 0.2, 0.8f);
                    Haptics.Play(HapticKind.WinMid);
                    break;
                case Rarity.Big:
                    Sound.PlaySfx("win_chime_3");
                    Sound.PlaySfxLater("win_big_bell", 0.25, 0.8f);
                    Sound.PlaySfxLater("coins_drop", 0.5, 0.8f);
                    Haptics.Play(HapticKind.WinBig);
                    break;
                case Rarity.Jackpot:
                    Sound.PlaySfx("jackpot_gong");
                    Sound.PlaySfxLater("jackpot_cheer", 0.45);
                    Sound.PlaySfxLater("coins_drop", 1.0, 0.8f);
                    Haptics.Play(HapticKind.Jackpot);
                    break;
                default:
                    if (e.Amount >= 0) { Sound.PlaySfx("lose_thud", 0.8f); Haptics.Play(HapticKind.Lose); }
                    break; // net kayıp: tuzak sesi zaten çaldı
            }
        }

        void PlayAutoResult(GameEvent e, double now)
        {
            bool win = e.Rarity != Rarity.None;
            if (now - _lastAutoSnd < 0.4) return;
            _lastAutoSnd = now;
            switch (e.Rarity)
            {
                case Rarity.Small: Sound.PlaySfx("coin_single", 0.45f); break;
                case Rarity.Medium: Sound.PlaySfx("win_small", 0.5f); break;
                case Rarity.Big: Sound.PlaySfx("win_mid", 0.55f); break;
                case Rarity.Jackpot: Sound.PlaySfx("win_chime_3", 0.6f); Sound.PlaySfxLater("jackpot_gong", 0.2, 0.5f); break;
            }
            AutoFlavor(win);
        }

        // ---------- Dükkân konuşması / görünümü ----------

        public ShopView GetShopView()
        {
            var v = new ShopView { CategoryCount = GameSession.Categories.Length };
            var s = _session;
            v.CategoryName = s.CategoryName;
            v.CategoryPos = s.ShopCategory + 1;
            var items = s.BuildItems(s.ShopCategory);
            v.Total = items.Count;
            int idx = ShopIndexOf(items);
            if (items.Count == 0) { v.Desc = "Bu bölümde şimdilik bir şey yok."; return v; }
            var it = items[idx];
            v.Entry = it; v.Pos = idx + 1;
            v.Desc = it.Desc ?? "";
            if (it.Kind == ShopItemKind.Upgrade && s.Config.UpgradeById.TryGetValue(it.Id, out var u))
                v.Desc += "\nSeviye " + s.State.Level(u.Id) + (u.MaxLevel > 0 ? " / " + u.MaxLevel : "");
            if (it.Kind == ShopItemKind.Card && s.Config.TypeById.TryGetValue(it.Id, out var t))
                v.Desc = TypeName(t) + " kart. " + v.Desc + "\nEn yüksek ödül: " + NumberFormat.Lira(MaxPrize(t));
            bool priced = it.Kind == ShopItemKind.Card || it.Kind == ShopItemKind.Upgrade || it.Kind == ShopItemKind.Node;
            if (priced)
            {
                v.UsesAB = it.UsesAB;
                long have = it.UsesAB ? s.State.AB : s.State.Money;
                v.Affordable = have >= it.Price;
                v.Shortfall = Math.Max(0, it.Price - have);
                v.PriceLine = it.UsesAB ? "Fiyat: " + it.Price + " Altın Bilet puanı" : "Fiyat: " + NumberFormat.Lira(it.Price);
                if (!v.Affordable) v.PriceLine += "\nEksik: " + (it.UsesAB ? v.Shortfall + " puan" : NumberFormat.Lira(v.Shortfall));
            }
            return v;
        }

        /// <summary>Seçili öğe: ReadShopItem metni öğe adıyla başlar; en uzun eşleşen ad seçilir (Core'a dokunmadan).</summary>
        int ShopIndexOf(List<ShopItem> items)
        {
            if (items.Count <= 1) return 0;
            string text = _session.ReadShopItem();
            int best = 0, bestLen = -1;
            for (int i = 0; i < items.Count; i++)
            {
                string n = items[i].Name ?? "";
                if (n.Length > bestLen && text.StartsWith(n, StringComparison.Ordinal)) { best = i; bestLen = n.Length; }
            }
            return best;
        }

        static string TypeName(CardTypeDef t)
        {
            switch (t.Rule)
            {
                case CardRule.ThreeOfAKind: return "Üçlü eşleşme";
                case CardRule.NumberHunt: return "Sayı avı";
                case CardRule.Multiplier: return "Çarpanlı";
                case CardRule.Trap: return "Tuzaklı";
                case CardRule.Joker: return "Jokerli";
                case CardRule.Sequence: return "Sıra tamamlama";
                case CardRule.Jackpot: return "Jackpot";
                case CardRule.SonSans: return "Son Şans";
                case CardRule.AltinBilet: return "Altın Bilet";
            }
            return "";
        }

        long MaxPrize(CardTypeDef t)
        {
            double best = 0;
            foreach (var s in t.Symbols) if (s.Kind != SymbolKind.Trap) best = Math.Max(best, s.Pay);
            if (t.Rule == CardRule.Multiplier)
            {
                double m = 1;
                foreach (var mm in t.Multipliers) m = Math.Max(m, mm.Mult);
                best *= m;
            }
            double v = t.Price * best * _session.Stats.PayoutMult;
            return double.IsNaN(v) || v <= 0 ? 0 : v >= 4e18 ? 4000000000000000000L : (long)Math.Round(v);
        }

        /// <summary>Kart öğesinde kart tanıtımı: ad, tür, fiyat, kuralın özeti, en yüksek ödül, para durumu.</summary>
        void SayShop(GameEvent e, string prefix)
        {
            if (string.IsNullOrEmpty(e.Speech)) return;
            string text = e.Speech;
            if (_session.ShopCategory == 0)
            {
                var items = _session.BuildItems(0);
                if (items.Count > 0)
                {
                    var it = items[ShopIndexOf(items)];
                    if (it.Kind == ShopItemKind.Card && _session.Config.TypeById.TryGetValue(it.Id, out var t))
                    {
                        string intro = it.Name + ". " + TypeName(t) + " kart. Fiyat " + NumberFormat.Lira(it.Price) + ". " + it.Desc +
                            " En yüksek ödül " + NumberFormat.Lira(MaxPrize(t)) + "." +
                            (_session.State.Money >= it.Price ? " Paran yetiyor." : " " + NumberFormat.Lira(it.Price - _session.State.Money) + " eksik.");
                        text = (prefix == null ? _session.CategoryName + ". " : prefix == "Dükkân. " ? "Dükkân. " + _session.CategoryName + ". " : "") + intro;
                    }
                }
            }
            Speech.Say(text, SpeechPriority.Critical);
        }
    }
}
