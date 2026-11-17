using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Kazi.Core;
using Xunit;

namespace Kazi.Core.Tests
{
    public class SaveTests
    {
        private static GameState Rich()
        {
            var s = T.NewSession(123456, 77);
            var st = s.State;
            st.Upgrades["guc"] = 3; st.Upgrades["oto_kaziyici"] = 1; st.Nodes["n_luck1"] = 1;
            st.UnlockedCatalog = 3; st.AB = 12; st.ABTotal = 40; st.PrestigeCount = 2; st.PrestigeOffer = true;
            st.RunEarned = 999; st.TotalEarned = 5000; st.TotalCards = 42; st.PlaySeconds = 321.5; st.RunSeconds = 100.25;
            st.Pity["mahalle_sonsans"] = 3; st.AutoType = "simitci"; st.AutoBuyOn = false; st.AutoDiscardTrap = true;
            st.LastTypeId = "simitci"; st.LastSaveUnix = 1700000000; st.NotifyAffordable = false;
            st.Detail = SpeechDetail.Silent; st.Mode = ScratchMode.Drag; st.SpeechRate = 2.25;
            s.BuyCard("simitci"); s.ScratchTap(); s.ScratchTap();
            s.BuyCard("cay_bardagi");
            st.Current.Scratch[5] = 0.4;
            return st;
        }

        [Fact]
        public void RoundTripKeepsEverything()
        {
            var a = Rich();
            string json = SaveSystem.Serialize(a);
            GameState b;
            Assert.True(SaveSystem.TryDeserialize(json, T.Cfg, out b));
            Assert.Equal(a.Money, b.Money);
            Assert.Equal(a.Upgrades, b.Upgrades);
            Assert.Equal(a.Nodes, b.Nodes);
            Assert.Equal(a.UnlockedCatalog, b.UnlockedCatalog);
            Assert.Equal(a.AB, b.AB); Assert.Equal(a.ABTotal, b.ABTotal); Assert.Equal(a.PrestigeCount, b.PrestigeCount); Assert.True(b.PrestigeOffer);
            Assert.Equal(a.RunEarned, b.RunEarned); Assert.Equal(a.TotalCards, b.TotalCards);
            Assert.Equal(a.PlaySeconds, b.PlaySeconds); Assert.Equal(a.RngState, b.RngState);
            Assert.Equal(a.Pity, b.Pity);
            Assert.Equal("simitci", b.AutoType); Assert.False(b.AutoBuyOn); Assert.True(b.AutoDiscardTrap);
            Assert.Equal(1700000000, b.LastSaveUnix);
            Assert.False(b.NotifyAffordable);
            Assert.Equal(SpeechDetail.Silent, b.Detail); Assert.Equal(ScratchMode.Drag, b.Mode); Assert.Equal(2.25, b.SpeechRate);
            Assert.Equal(a.Current.Cells, b.Current.Cells);
            Assert.Equal(a.Current.Open, b.Current.Open);
            Assert.Equal(a.Current.Type.Id, b.Current.Type.Id);
            Assert.Equal(a.Current.Seed, b.Current.Seed);
            Assert.Equal(0.4, b.Current.Scratch[5], 3);
            Assert.Equal(a.Bag.Count, b.Bag.Count);
            Assert.Equal(a.Bag[0].Cells, b.Bag[0].Cells);
            Assert.Equal(a.Bag[0].Paid, b.Bag[0].Paid);
        }

        [Fact]
        public void RngStateSurvivesSoNextCardIsIdentical()
        {
            var s1 = T.NewSession(1000, 5);
            s1.BuyCard("simitci");
            var json = SaveSystem.Serialize(s1.State);
            var s2 = new GameSession(T.Cfg, SaveSystem.LoadOrNew(json, T.Cfg));
            s1.State.Current = null; s2.State.Current = null;
            s1.BuyCard("simitci"); s2.BuyCard("simitci");
            Assert.Equal(s1.State.Current.Cells, s2.State.Current.Cells);
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("not json at all")]
        [InlineData("{")]
        [InlineData("[]")]
        [InlineData("null")]
        [InlineData("{\"money\":\"abc\",\"upgrades\":5,\"bag\":\"x\",\"current\":7}")]
        [InlineData("{\"money\":-500,\"catalog\":99,\"upgrades\":{\"guc\":99999,\"yok\":3,\"sans1\":-4},\"nodes\":{\"n_luck1\":7,\"yok\":1}}")]
        [InlineData("{\"money\":1e300,\"ab\":-5,\"autoAccum\":999,\"speechRate\":99,\"detail\":77,\"mode\":-3}")]
        public void CorruptSavesNeverThrowAndYieldSaneState(string json)
        {
            GameState st = null;
            var ex = Record.Exception(() => SaveSystem.TryDeserialize(json, T.Cfg, out st));
            Assert.Null(ex);
            Assert.NotNull(st);
            Assert.True(st.Money >= 0);
            Assert.InRange(st.UnlockedCatalog, 1, T.Cfg.CatalogCount);
            foreach (var kv in st.Upgrades) { Assert.True(T.Cfg.UpgradeById.ContainsKey(kv.Key)); Assert.InRange(kv.Value, 1, T.Cfg.UpgradeById[kv.Key].MaxLevel); }
            foreach (var kv in st.Nodes) { Assert.True(T.Cfg.NodeById.ContainsKey(kv.Key)); Assert.Equal(1, kv.Value); }
            Assert.True(st.AB >= 0);
            Assert.InRange(st.SpeechRate, 0.5, 3.0);
            Assert.InRange(st.AutoAccum, 0, 1);
            var s = new GameSession(T.Cfg, st); // oyun açılabilmeli
            s.Tick(1); s.ReadStatus();
        }

        [Fact]
        public void BrokenCardsAreDropped()
        {
            string json = "{\"money\":5,\"current\":{\"type\":\"simitci\",\"cells\":[1,2,3],\"open\":[]},\"bag\":[{\"type\":\"yok\"},{\"type\":\"simitci\",\"cells\":[0,0,0,0,0,99]}]}";
            GameState st;
            Assert.True(SaveSystem.TryDeserialize(json, T.Cfg, out st));
            Assert.Null(st.Current);
            Assert.Empty(st.Bag);
            Assert.Equal(5, st.Money);
        }

        [Fact]
        public void TruncatedRealSaveFallsBackToNewGame()
        {
            string json = SaveSystem.Serialize(Rich());
            GameState st;
            Assert.False(SaveSystem.TryDeserialize(json.Substring(0, json.Length / 2), T.Cfg, out st));
            Assert.Equal(T.Cfg.Settings.StartMoney, st.Money);
        }

        [Fact]
        public void FileSaveAndLoad_MissingOrGarbageFile()
        {
            string dir = Path.Combine(Path.GetTempPath(), "kazi_test_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            try
            {
                string p = Path.Combine(dir, "save.json");
                Assert.Equal(T.Cfg.Settings.StartMoney, SaveSystem.LoadFromFile(p, T.Cfg).Money);
                var a = Rich();
                SaveSystem.SaveToFile(a, p);
                SaveSystem.SaveToFile(a, p); // üzerine yazma
                Assert.Equal(a.Money, SaveSystem.LoadFromFile(p, T.Cfg).Money);
                File.WriteAllText(p, "çöp\0\0");
                Assert.Equal(T.Cfg.Settings.StartMoney, SaveSystem.LoadFromFile(p, T.Cfg).Money);
            }
            finally { Directory.Delete(dir, true); }
        }

        [Fact]
        public void VersionIsWritten()
        {
            var o = MiniJson.AsObject(MiniJson.Parse(SaveSystem.Serialize(new GameState())));
            Assert.Equal(GameState.CurrentVersion, MiniJson.GetInt(o, "version", 0));
        }
    }

    public class SpeechTests
    {
        private static List<GameEvent> PlayCard(SpeechDetail d, out GameSession s)
        {
            s = T.NewSession(100);
            s.Detail = d;
            var c = T.Make("simitci", "simit", "simit", "simit", "cay", "cay", "guvercin"); c.Paid = 10;
            s.State.Current = c;
            var ev = T.Capture(s);
            for (int i = 0; i < 6; i++) s.ScratchTap();
            return ev;
        }

        [Fact]
        public void DefaultsAreShortDetailAndRate150()
        {
            var st = new GameState();
            Assert.Equal(SpeechDetail.Short, st.Detail);
            Assert.Equal(1.5, st.SpeechRate);
        }

        [Fact]
        public void FullSpeaksEverySymbolAndResult()
        {
            GameSession s;
            var ev = PlayCard(SpeechDetail.Full, out s);
            var cells = ev.Where(e => e.Kind == EventKind.CellOpened).ToList();
            Assert.Equal(6, cells.Count);
            Assert.All(cells, e => Assert.False(string.IsNullOrEmpty(e.Speech)));
            Assert.Equal("Simit", cells[0].Speech);
            Assert.StartsWith("Kazandın", ev.Single(e => e.Kind == EventKind.CardResult).Speech);
        }

        [Fact]
        public void ShortSpeaksOnlyTheResult()
        {
            GameSession s;
            var ev = PlayCard(SpeechDetail.Short, out s);
            Assert.All(ev.Where(e => e.Kind == EventKind.CellOpened), e => Assert.Equal("", e.Speech));
            Assert.Equal(6, ev.Count(e => e.Kind == EventKind.CellOpened)); // olay yine var (ses için)
            Assert.StartsWith("Kazandın", ev.Single(e => e.Kind == EventKind.CardResult).Speech);
        }

        [Fact]
        public void SilentSpeaksNothingButEventsStillFire()
        {
            GameSession s;
            var ev = PlayCard(SpeechDetail.Silent, out s);
            Assert.True(ev.Count >= 7);
            Assert.All(ev, e => Assert.Equal("", e.Speech));
            var res = ev.Single(e => e.Kind == EventKind.CardResult);
            Assert.Equal(Rarity.Small, res.Rarity);
            Assert.True(res.Amount > 0);
        }

        [Fact]
        public void MenuReadingAlwaysWorksEvenWhenSilent()
        {
            var s = T.NewSession(100);
            s.Detail = SpeechDetail.Silent;
            var ev = T.Capture(s);
            s.ToggleShop(); s.ShopNextCategory();
            Assert.All(ev, e => Assert.False(string.IsNullOrEmpty(e.Speech)));
            Assert.False(string.IsNullOrEmpty(s.ReadStatus()));
        }

        [Fact]
        public void ResultSentencesAreShort()
        {
            GameSession s;
            var win = PlayCard(SpeechDetail.Short, out s).Single(e => e.Kind == EventKind.CardResult);
            Assert.True(win.Speech.Length <= 40, win.Speech);
            var s2 = T.NewSession(100);
            var c = T.Make("simitci", "simit", "simit", "cay", "cay", "guvercin", "guvercin");
            s2.State.Current = c;
            var ev = T.Capture(s2);
            for (int i = 0; i < 6; i++) s2.ScratchTap();
            Assert.True(ev.Single(e => e.Kind == EventKind.CardResult).Speech.Length <= 40);
        }

        [Theory]
        [InlineData(0.1, 0.5)]
        [InlineData(0.5, 0.5)]
        [InlineData(0.6, 0.5)]
        [InlineData(0.63, 0.75)]
        [InlineData(1.4, 1.5)]
        [InlineData(1.5, 1.5)]
        [InlineData(2.9, 3.0)]
        [InlineData(3.0, 3.0)]
        [InlineData(10, 3.0)]
        [InlineData(double.NaN, 1.5)]
        public void SpeechRateClampsToHalfStepsBetween05And30(double input, double expected)
        {
            var s = T.NewSession(1);
            s.SpeechRate = input;
            Assert.Equal(expected, s.SpeechRate);
        }

        [Fact]
        public void EstimatedSecondsIsCharsOverRate()
        {
            Assert.Equal(0, GameEvent.Estimate("", 1.5));
            Assert.Equal(0, GameEvent.Estimate(null, 1.5));
            string text = "Kazandın, 250 lira.";
            double at1 = GameEvent.Estimate(text, 1.0);
            Assert.Equal(text.Length / GameEvent.CharsPerSecond, at1, 6);
            Assert.Equal(at1 / 2, GameEvent.Estimate(text, 2.0), 6);
            Assert.True(GameEvent.Estimate(text, 3.0) < GameEvent.Estimate(text, 0.5));
        }

        [Fact]
        public void EveryEventWithSpeechCarriesEstimate()
        {
            var s = T.NewSession(100);
            s.Detail = SpeechDetail.Full; s.SpeechRate = 2.0;
            var ev = T.Capture(s);
            s.BuyCard("simitci"); for (int i = 0; i < 6; i++) s.ScratchTap(); s.ReadStatus();
            Assert.NotEmpty(ev);
            foreach (var e in ev)
            {
                if (e.Speech != "") Assert.Equal(e.Speech.Length / (GameEvent.CharsPerSecond * 2.0), e.EstimatedSeconds, 6);
                else Assert.Equal(0, e.EstimatedSeconds);
            }
            Assert.Contains(ev, e => e.EstimatedSeconds > 0);
        }

        [Fact]
        public void HigherRateShortensEstimate()
        {
            var s = T.NewSession(100);
            var ev = T.Capture(s);
            s.SpeechRate = 1.0; s.BuyCard("simitci"); double slow = ev.Last().EstimatedSeconds;
            s.State.Current = null;
            s.SpeechRate = 3.0; s.BuyCard("simitci"); double fast = ev.Last().EstimatedSeconds;
            Assert.True(fast < slow);
        }
    }

    public class GestureTests
    {
        // 1080x1920, 320 dpi -> dp = 2: tapSlop 36px, swipeMin = max(112, 134.4) = 134.4px
        private static GestureRecognizer Make(out List<Gesture> log)
        {
            var g = new GestureRecognizer();
            g.Configure(1080, 1920, 320f);
            var l = new List<Gesture>();
            g.OnGesture += x => l.Add(x);
            log = l;
            return g;
        }

        private static void Down(GestureRecognizer g, int id, float x, float y, double t) { g.Feed(new RawTouch(id, TouchPhase.Down, x, y, t)); }
        private static void Move(GestureRecognizer g, int id, float x, float y, double t) { g.Feed(new RawTouch(id, TouchPhase.Move, x, y, t)); }
        private static void Up(GestureRecognizer g, int id, float x, float y, double t) { g.Feed(new RawTouch(id, TouchPhase.Up, x, y, t)); }

        private static void Swipe(GestureRecognizer g, float x0, float y0, float x1, float y1, double t0)
        {
            Down(g, 1, x0, y0, t0);
            Move(g, 1, (x0 + x1) / 2, (y0 + y1) / 2, t0 + 0.05);
            Up(g, 1, x1, y1, t0 + 0.1);
        }

        [Fact]
        public void SingleTapEmitsImmediatelyOnRelease()
        {
            var g = Make(out var log);
            Down(g, 1, 500, 900, 0);
            Assert.Empty(log);
            Up(g, 1, 505, 903, 0.05);
            Assert.Equal(new[] { Gesture.Tap }, log); // çift dokunuş penceresi beklenmez
        }

        [Fact]
        public void DoubleTapEmitsTapTapDouble()
        {
            var g = Make(out var log);
            Down(g, 1, 500, 900, 0); Up(g, 1, 500, 900, 0.07);
            Down(g, 1, 502, 902, 0.25); Up(g, 1, 502, 902, 0.31);
            Assert.Equal(new[] { Gesture.Tap, Gesture.Tap, Gesture.DoubleTap }, log);
        }

        [Fact]
        public void SlowTapsAreNotDouble_TripleTapGivesOneDouble()
        {
            var g = Make(out var log);
            Down(g, 1, 500, 900, 0); Up(g, 1, 500, 900, 0.07);
            Down(g, 1, 500, 900, 1.0); Up(g, 1, 500, 900, 1.07);
            Assert.Equal(new[] { Gesture.Tap, Gesture.Tap }, log);
            var g2 = Make(out var log2);
            for (int i = 0; i < 3; i++) { Down(g2, 1, 500, 900, i * 0.2); Up(g2, 1, 500, 900, i * 0.2 + 0.05); }
            Assert.Equal(new[] { Gesture.Tap, Gesture.Tap, Gesture.DoubleTap, Gesture.Tap }, log2);
        }

        [Fact]
        public void LongPressIgnored()
        {
            var g = Make(out var log);
            Down(g, 1, 500, 900, 0); Up(g, 1, 500, 900, 1.2);
            Assert.Empty(log);
        }

        [Theory]
        [InlineData(500, 1000, 500, 600, Gesture.SwipeUp)]
        [InlineData(500, 600, 500, 1000, Gesture.SwipeDown)]
        [InlineData(700, 900, 300, 900, Gesture.SwipeLeft)]
        [InlineData(300, 900, 700, 900, Gesture.SwipeRight)]
        [InlineData(300, 900, 700, 800, Gesture.SwipeRight)]
        public void FourDirectionSwipes(float x0, float y0, float x1, float y1, Gesture expected)
        {
            var g = Make(out var log);
            Swipe(g, x0, y0, x1, y1, 0);
            Assert.Equal(new[] { expected }, log);
        }

        [Fact]
        public void DeadZoneAndJitter()
        {
            var g = Make(out var log);
            Swipe(g, 500, 900, 500, 820, 0);
            Assert.Empty(log);
            var g2 = Make(out var log2);
            Swipe(g2, 500, 900, 520, 915, 0);
            Assert.Equal(new[] { Gesture.Tap }, log2);
        }

        [Fact]
        public void TwoAndThreeFingerTaps()
        {
            var g = Make(out var log);
            Down(g, 1, 300, 900, 0); Down(g, 2, 700, 900, 0.02);
            Up(g, 1, 300, 900, 0.1); Up(g, 2, 700, 900, 0.11);
            Down(g, 1, 300, 900, 1); Down(g, 2, 500, 900, 1.02); Down(g, 3, 700, 900, 1.03);
            Up(g, 3, 700, 900, 1.1); Up(g, 1, 300, 900, 1.11); Up(g, 2, 500, 900, 1.12);
            Assert.Equal(new[] { Gesture.TwoFingerTap, Gesture.ThreeFingerTap }, log);
        }

        [Fact]
        public void TwoFingerHorizontalSwipesChangeCategory_VerticalIgnored()
        {
            var g = Make(out var log);
            Down(g, 1, 700, 900, 0); Down(g, 2, 760, 900, 0.01);
            Move(g, 1, 500, 900, 0.1); Move(g, 2, 560, 900, 0.1);
            Up(g, 1, 400, 900, 0.2); Up(g, 2, 460, 900, 0.2);
            Down(g, 1, 300, 900, 1.0); Down(g, 2, 360, 900, 1.01);
            Move(g, 1, 500, 900, 1.1); Move(g, 2, 560, 900, 1.1);
            Up(g, 1, 600, 900, 1.2); Up(g, 2, 660, 900, 1.2);
            Down(g, 1, 500, 1200, 2); Down(g, 2, 560, 1200, 2.01);
            Move(g, 1, 500, 900, 2.1); Move(g, 2, 560, 900, 2.1);
            Up(g, 1, 500, 700, 2.2); Up(g, 2, 560, 700, 2.2);
            Assert.Equal(new[] { Gesture.TwoFingerSwipeLeft, Gesture.TwoFingerSwipeRight }, log);
        }

        [Fact]
        public void CancelAndMultiTouchDoNotLeak()
        {
            var g = Make(out var log);
            Down(g, 1, 500, 900, 0);
            g.Feed(new RawTouch(1, TouchPhase.Cancel, 500, 900, 0.05));
            Assert.Empty(log);
            Down(g, 1, 300, 900, 1); Down(g, 2, 700, 900, 1.02);
            Up(g, 1, 300, 900, 1.1); Up(g, 2, 700, 900, 1.11);
            Down(g, 1, 500, 900, 1.5); Up(g, 1, 500, 900, 1.55);
            Assert.Equal(new[] { Gesture.TwoFingerTap, Gesture.Tap }, log);
        }

        [Fact]
        public void ThresholdsScaleWithDpiAndScreen()
        {
            var g = new GestureRecognizer();
            g.Configure(720, 1280, 160f);
            Assert.Equal(18f, g.TapSlopPx, 3);
            Assert.Equal(89.6f, g.SwipeMinPx, 2);
            g.Configure(1440, 3200, 560f);
            Assert.Equal(63f, g.TapSlopPx, 3);
            Assert.Equal(224f, g.SwipeMinPx, 2);
        }

        // ---- kazıma sürüklemesi ----
        [Fact]
        public void DragMoveEventsOnlyInDragMode()
        {
            var g = Make(out var log);
            var moves = new List<DragMove>();
            g.OnDragMove += m => moves.Add(m);
            Down(g, 1, 500, 900, 0);
            Move(g, 1, 540, 900, 0.05); Move(g, 1, 600, 900, 0.1);
            Up(g, 1, 600, 900, 0.15);
            Assert.Empty(moves);

            g.DragMode = true;
            Down(g, 1, 500, 900, 1);
            Move(g, 1, 510, 900, 1.02);   // payın içinde: henüz sürükleme değil
            Move(g, 1, 560, 900, 1.05);
            Move(g, 1, 620, 920, 1.10);
            Up(g, 1, 620, 920, 1.15);
            Assert.Equal(2, moves.Count);
            Assert.Equal(560, moves[0].X); Assert.Equal(510, moves[0].PrevX);
            Assert.True(moves[1].Distance > 55);
        }

        [Fact]
        public void SlowDragIsNotASwipe_ButFlickStillIs()
        {
            var g = Make(out var log);
            g.DragMode = true;
            g.OnDragMove += _ => { };
            // yavaş, uzun sürükleme: kaydırma YOK
            Down(g, 1, 300, 800, 0);
            for (int i = 1; i <= 10; i++) Move(g, 1, 300, 800 + i * 40, i * 0.1);
            Up(g, 1, 300, 1200, 1.05);
            Assert.Empty(log);
            // hızlı fırlatma: kaydırma
            Down(g, 1, 300, 1200, 3);
            Move(g, 1, 300, 1000, 3.03);
            Up(g, 1, 300, 800, 3.06);
            Assert.Equal(new[] { Gesture.SwipeUp }, log);
        }

        [Fact]
        public void TapStaysInstantInDragMode()
        {
            var g = Make(out var log);
            g.DragMode = true;
            Down(g, 1, 500, 900, 0); Up(g, 1, 500, 900, 0.05);
            Assert.Equal(new[] { Gesture.Tap }, log);
        }
    }
}
