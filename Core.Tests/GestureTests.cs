using System.Collections.Generic;
using Maden.Core;
using Xunit;

namespace Maden.Core.Tests
{
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
        public void SingleTap()
        {
            var g = Make(out var log);
            Down(g, 1, 500, 900, 0); Up(g, 1, 505, 903, 0.08);
            Assert.Equal(new[] { Gesture.Tap }, log);
        }

        [Fact]
        public void DoubleTap_EmitsTapTapDouble()
        {
            var g = Make(out var log);
            Down(g, 1, 500, 900, 0); Up(g, 1, 500, 900, 0.07);
            Down(g, 1, 502, 902, 0.25); Up(g, 1, 502, 902, 0.31);
            Assert.Equal(new[] { Gesture.Tap, Gesture.Tap, Gesture.DoubleTap }, log);
        }

        [Fact]
        public void TwoTapsTooFarApartInTime_NoDoubleTap()
        {
            var g = Make(out var log);
            Down(g, 1, 500, 900, 0); Up(g, 1, 500, 900, 0.07);
            Down(g, 1, 500, 900, 1.0); Up(g, 1, 500, 900, 1.07);
            Assert.Equal(new[] { Gesture.Tap, Gesture.Tap }, log);
        }

        [Fact]
        public void TripleTap_DoubleTapOnlyOnce()
        {
            var g = Make(out var log);
            for (int i = 0; i < 3; i++) { Down(g, 1, 500, 900, i * 0.2); Up(g, 1, 500, 900, i * 0.2 + 0.05); }
            // 1: Tap, 2: Tap+Double, 3: Tap (yeni zincir)
            Assert.Equal(new[] { Gesture.Tap, Gesture.Tap, Gesture.DoubleTap, Gesture.Tap }, log);
        }

        [Fact]
        public void LongPress_Ignored()
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
        [InlineData(300, 900, 700, 800, Gesture.SwipeRight)] // hafif eğik: baskın eksen
        public void FourDirectionSwipes(float x0, float y0, float x1, float y1, Gesture expected)
        {
            var g = Make(out var log);
            Swipe(g, x0, y0, x1, y1, 0);
            Assert.Equal(new[] { expected }, log);
        }

        [Fact]
        public void DeadZone_MovementBetweenSlopAndSwipeMin_NoGesture()
        {
            var g = Make(out var log);
            // 80px: tap payı (36) üstünde, kaydırma eşiği (134) altında
            Swipe(g, 500, 900, 500, 820, 0);
            Assert.Empty(log);
        }

        [Fact]
        public void SmallJitterWithinSlop_IsTap()
        {
            var g = Make(out var log);
            Swipe(g, 500, 900, 520, 915, 0);
            Assert.Equal(new[] { Gesture.Tap }, log);
        }

        [Fact]
        public void Swipe_BreaksDoubleTapChain()
        {
            var g = Make(out var log);
            Down(g, 1, 500, 900, 0); Up(g, 1, 500, 900, 0.05);
            Swipe(g, 500, 1000, 500, 600, 0.1);
            Down(g, 1, 500, 900, 0.3); Up(g, 1, 500, 900, 0.35);
            Assert.Equal(new[] { Gesture.Tap, Gesture.SwipeUp, Gesture.Tap }, log);
        }

        [Fact]
        public void TwoFingerTap()
        {
            var g = Make(out var log);
            Down(g, 1, 300, 900, 0); Down(g, 2, 700, 900, 0.02);
            Up(g, 1, 300, 900, 0.1); Up(g, 2, 700, 900, 0.11);
            Assert.Equal(new[] { Gesture.TwoFingerTap }, log);
        }

        [Fact]
        public void ThreeFingerTap()
        {
            var g = Make(out var log);
            Down(g, 1, 300, 900, 0); Down(g, 2, 500, 900, 0.02); Down(g, 3, 700, 900, 0.03);
            Up(g, 3, 700, 900, 0.1); Up(g, 1, 300, 900, 0.11); Up(g, 2, 500, 900, 0.12);
            Assert.Equal(new[] { Gesture.ThreeFingerTap }, log);
        }

        [Fact]
        public void TwoFingerLongPress_Ignored()
        {
            var g = Make(out var log);
            Down(g, 1, 300, 900, 0); Down(g, 2, 700, 900, 0.02);
            Up(g, 1, 300, 900, 1.5); Up(g, 2, 700, 900, 1.51);
            Assert.Empty(log);
        }

        [Fact]
        public void TwoFingerSwipe_NotATap()
        {
            var g = Make(out var log);
            Down(g, 1, 300, 1000, 0); Down(g, 2, 700, 1000, 0.01);
            Move(g, 1, 300, 600, 0.1); Move(g, 2, 700, 600, 0.1);
            Up(g, 1, 300, 600, 0.15); Up(g, 2, 700, 600, 0.16);
            Assert.Empty(log);
        }

        [Fact]
        public void MultiTouch_DoesNotLeakIntoNextSingleTap()
        {
            var g = Make(out var log);
            Down(g, 1, 300, 900, 0); Down(g, 2, 700, 900, 0.02);
            Up(g, 1, 300, 900, 0.1); Up(g, 2, 700, 900, 0.11);
            Down(g, 1, 500, 900, 0.5); Up(g, 1, 500, 900, 0.55);
            Assert.Equal(new[] { Gesture.TwoFingerTap, Gesture.Tap }, log);
        }

        [Fact]
        public void Cancel_ProducesNothing()
        {
            var g = Make(out var log);
            Down(g, 1, 500, 900, 0);
            g.Feed(new RawTouch(1, TouchPhase.Cancel, 500, 900, 0.05));
            Assert.Empty(log);
        }

        [Fact]
        public void Thresholds_ScaleWithDpiAndScreen()
        {
            var g = new GestureRecognizer();
            g.Configure(720, 1280, 160f);
            Assert.Equal(18f, g.TapSlopPx, 3);
            Assert.Equal(89.6f, g.SwipeMinPx, 2); // max(56, 89.6)
            g.Configure(1440, 3200, 560f);
            Assert.Equal(63f, g.TapSlopPx, 3);
            Assert.Equal(224f, g.SwipeMinPx, 2); // max(196, 224)
        }

        [Fact]
        public void TwoFingerSwipeLeftRight_IsNowTwoFingerSwipe()
        {
            var g = Make(out var log);
            // iki parmak birlikte sola kaydır
            Down(g, 1, 700, 900, 0); Down(g, 2, 760, 900, 0.01);
            Move(g, 1, 500, 900, 0.1); Move(g, 2, 560, 900, 0.1);
            Up(g, 1, 400, 900, 0.2); Up(g, 2, 460, 900, 0.2);
            Assert.Equal(new[] { Gesture.TwoFingerSwipeLeft }, log);
            log.Clear();
            Down(g, 1, 300, 900, 1.0); Down(g, 2, 360, 900, 1.01);
            Move(g, 1, 500, 900, 1.1); Move(g, 2, 560, 900, 1.1);
            Up(g, 1, 600, 900, 1.2); Up(g, 2, 660, 900, 1.2);
            Assert.Equal(new[] { Gesture.TwoFingerSwipeRight }, log);
        }

        [Fact]
        public void TwoFingerVerticalSwipe_IsIgnored()
        {
            var g = Make(out var log);
            Down(g, 1, 500, 1200, 0); Down(g, 2, 560, 1200, 0.01);
            Move(g, 1, 500, 900, 0.1); Move(g, 2, 560, 900, 0.1);
            Up(g, 1, 500, 700, 0.2); Up(g, 2, 560, 700, 0.2);
            Assert.Empty(log);
        }

        [Fact]
        public void TwoFingerTapStillWorks_AndSingleFingerSwipeUnchanged()
        {
            var g = Make(out var log);
            Down(g, 1, 500, 900, 0); Down(g, 2, 600, 900, 0.01);
            Up(g, 1, 500, 900, 0.1); Up(g, 2, 600, 900, 0.11);
            Swipe(g, 700, 900, 300, 900, 1.0);
            Assert.Equal(new[] { Gesture.TwoFingerTap, Gesture.SwipeLeft }, log);
        }

        [Fact]
        public void SingleTapEmitsImmediately_NoWaitingForDoubleTapWindow()
        {
            var g = Make(out var log);
            Down(g, 1, 500, 900, 0);
            Up(g, 1, 500, 900, 0.05);
            // Tap, parmak kalkar kalkmaz (başka olay beklemeden) yayılmış olmalı.
            Assert.Equal(new[] { Gesture.Tap }, log);
        }
    }
}
