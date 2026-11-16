using System;
using System.Collections.Generic;

namespace Maden.Core
{
    /// <summary>Tanınan hareketler.</summary>
    public enum Gesture
    {
        Tap, DoubleTap,
        SwipeUp, SwipeDown, SwipeLeft, SwipeRight,
        TwoFingerTap, ThreeFingerTap,
        /// <summary>İki parmakla yatay kaydırma (dükkânda kategori değiştirme).</summary>
        TwoFingerSwipeLeft, TwoFingerSwipeRight
    }

    public enum TouchPhase { Down, Move, Up, Cancel }

    /// <summary>Motordan bağımsız ham dokunma olayı. X/Y piksel, Time saniye (tek yönlü artan).</summary>
    public struct RawTouch
    {
        public int Id;
        public TouchPhase Phase;
        public float X, Y;
        public double Time;

        public RawTouch(int id, TouchPhase phase, float x, float y, double time)
        {
            Id = id; Phase = phase; X = x; Y = y; Time = time;
        }
    }

    /// <summary>
    /// Saf C# hareket tanıyıcı. Eşikler ekran dpi'sına ve ekran boyuna göre hesaplanır.
    /// Dokunma bırakılınca (son parmak kalkınca) hareket sınıflandırılır.
    /// Tek dokunuş hemen Tap yayar; ikinci hızlı dokunuş ayrıca DoubleTap yayar.
    /// </summary>
    public sealed class GestureRecognizer
    {
        public event Action<Gesture> OnGesture;

        // Saniye cinsinden eşikler
        public double TapMaxSeconds = 0.45;
        public double DoubleTapGapSeconds = 0.35;
        public double MultiTapMaxSeconds = 0.6;

        // Piksel cinsinden eşikler (Configure ile hesaplanır)
        public float TapSlopPx { get; private set; }
        public float SwipeMinPx { get; private set; }

        private sealed class Pointer { public float StartX, StartY, X, Y; }

        private readonly Dictionary<int, Pointer> pointers = new Dictionary<int, Pointer>();
        private bool active;
        private double startT, lastT;
        private float lastX, lastY, startX, startY;
        private float maxDisp; // herhangi bir parmağın başlangıcından en büyük uzaklık
        private int maxCount;
        private float sumDx, sumDy; private int upCount; // kalkan parmakların ortalama net yer değiştirmesi
        private bool lastTapValid;
        private double lastTapT;

        public GestureRecognizer() { Configure(1080, 1920, 160f); }

        /// <summary>dpi bilinmiyorsa 160 ver. Eşikler: dokunuş payı 18dp, kaydırma en az max(56dp, ekran uzun kenarının %7'si).</summary>
        public void Configure(float screenWidthPx, float screenHeightPx, float dpi)
        {
            if (dpi < 40f) dpi = 160f;
            float dp = dpi / 160f;
            float longSide = Math.Max(screenWidthPx, screenHeightPx);
            TapSlopPx = 18f * dp;
            SwipeMinPx = Math.Max(56f * dp, longSide * 0.07f);
        }

        public void Reset()
        {
            pointers.Clear();
            active = false; lastTapValid = false; maxCount = 0;
        }

        public void Feed(RawTouch t)
        {
            switch (t.Phase)
            {
                case TouchPhase.Down:
                    pointers[t.Id] = new Pointer { StartX = t.X, StartY = t.Y, X = t.X, Y = t.Y };
                    if (!active)
                    {
                        active = true; maxCount = 1; maxDisp = 0f; sumDx = 0f; sumDy = 0f; upCount = 0;
                        startT = t.Time;
                        startX = t.X; startY = t.Y;
                        lastX = t.X; lastY = t.Y;
                        lastT = t.Time;
                    }
                    else
                    {
                        if (pointers.Count > maxCount) maxCount = pointers.Count;
                        lastT = t.Time;
                    }
                    break;

                case TouchPhase.Move:
                    if (!active || !pointers.TryGetValue(t.Id, out Pointer p)) return;
                    Track(p, t);
                    break;

                case TouchPhase.Up:
                    if (!active || !pointers.TryGetValue(t.Id, out Pointer q)) return;
                    Track(q, t);
                    sumDx += q.X - q.StartX; sumDy += q.Y - q.StartY; upCount++;
                    pointers.Remove(t.Id);
                    if (pointers.Count == 0)
                    {
                        active = false;
                        Recognize();
                    }
                    break;

                case TouchPhase.Cancel:
                    pointers.Remove(t.Id);
                    if (pointers.Count == 0) { active = false; lastTapValid = false; }
                    break;
            }
        }

        private void Track(Pointer p, RawTouch t)
        {
            p.X = t.X; p.Y = t.Y;
            lastT = t.Time;
            float ddx = p.X - p.StartX, ddy = p.Y - p.StartY;
            float d = (float)Math.Sqrt(ddx * ddx + ddy * ddy);
            if (d > maxDisp) maxDisp = d;
            if (maxCount == 1) { lastX = p.X; lastY = p.Y; }
        }

        private void Recognize()
        {
            double dur = lastT - startT;
            float dx = lastX - startX, dy = lastY - startY;
            float mdx = upCount > 0 ? sumDx / upCount : 0f, mdy = upCount > 0 ? sumDy / upCount : 0f;
            float dist = maxCount == 1 ? (float)Math.Sqrt(dx * dx + dy * dy) : maxDisp;

            if (maxCount >= 2)
            {
                lastTapValid = false;
                if (maxCount == 2 && dist > SwipeMinPx && dur <= 1.2)
                {
                    // İki parmakla kaydırma: yalnızca belirgin yatay hareket sayılır; dikey yok sayılır.
                    if (Math.Abs(mdx) > Math.Abs(mdy) * 1.5f && Math.Abs(mdx) >= SwipeMinPx)
                        Emit(mdx < 0f ? Gesture.TwoFingerSwipeLeft : Gesture.TwoFingerSwipeRight);
                    return;
                }
                if (dur > MultiTapMaxSeconds || dist > SwipeMinPx) return;
                Emit(maxCount >= 3 ? Gesture.ThreeFingerTap : Gesture.TwoFingerTap);
                return;
            }

            if (dist <= TapSlopPx)
            {
                if (dur > TapMaxSeconds) { lastTapValid = false; return; } // uzun basış: yok say
                bool dbl = lastTapValid && (lastT - lastTapT) <= DoubleTapGapSeconds;
                Emit(Gesture.Tap);
                if (dbl) { Emit(Gesture.DoubleTap); lastTapValid = false; }
                else { lastTapValid = true; lastTapT = lastT; }
                return;
            }

            lastTapValid = false;
            if (dist < SwipeMinPx) return; // ölü bölge
            if (Math.Abs(dy) > Math.Abs(dx)) Emit(dy < 0f ? Gesture.SwipeUp : Gesture.SwipeDown);
            else Emit(dx < 0f ? Gesture.SwipeLeft : Gesture.SwipeRight);
        }

        private void Emit(Gesture g)
        {
            var h = OnGesture;
            if (h != null) h(g);
        }
    }
}
