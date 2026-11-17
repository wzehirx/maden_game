using System;
using Godot;
using Kazi.Core;

namespace Kazi.Game
{
    /// <summary>
    /// Godot dokunma/fare/klavye olaylarını ham RawTouch'a çevirip saf C# GestureRecognizer'a besler.
    /// Giriş _Input'ta ANINDA işlenir (kare beklenmez). Masaüstü yedeği: sol fare = parmak (sürükleme dahil),
    /// sağ fare = 2 parmak dokunuş, orta fare = 3 parmak dokunuş; Space = kaz, B/Yukarı = dükkân, S/Aşağı = bırak/sıradaki,
    /// Sol/Sağ = gez, Enter = çift dokunuş, 2 / 3 = çok parmak dokunuşu, Q / E = 2 parmakla sol/sağ kaydırma.
    /// </summary>
    public partial class InputRouter : Node
    {
        public event Action<Gesture> OnGesture;
        /// <summary>Yeni bir dokunuş başlarken (x, y px): denetleyici kazıma modunu bu noktaya göre ayarlar.</summary>
        public Action<float, float> BeforeTouch;

        readonly GestureRecognizer _rec = new GestureRecognizer();
        const int MouseId = 1000;
        Vector2I _lastSize;

        public GestureRecognizer Recognizer => _rec;
        /// <summary>Son dokunuşun konumu (Tap'in hangi hücreye düştüğünü bulmak için).</summary>
        public Vector2 LastPos { get; private set; }

        public override void _Ready()
        {
            _rec.OnGesture += g =>
            {
                try { OnGesture?.Invoke(g); }
                catch (Exception e) { GD.PushError("Hareket işleyicisi: " + e); }
            };
            ProcessMode = ProcessModeEnum.Always;
            Reconfigure();
        }

        void Reconfigure()
        {
            var size = DisplayServer.WindowGetSize();
            if (size == _lastSize && size != Vector2I.Zero) return;
            _lastSize = size;
            float dpi = DisplayServer.ScreenGetDpi();
            _rec.Configure(size.X, size.Y, dpi);
        }

        static double Now() => Time.GetTicksMsec() / 1000.0;

        public override void _Input(InputEvent ev)
        {
            // Giriş anında ekranı hızlı kare hızına al (boştayken düşük kare hızı gecikme yaratmasın).
            if (!(ev is InputEventMouseMotion mm0 && mm0.ButtonMask == 0)) Main.Poke();
            switch (ev)
            {
                case InputEventScreenTouch st:
                    Reconfigure();
                    LastPos = st.Position;
                    if (st.Pressed) BeforeTouch?.Invoke(st.Position.X, st.Position.Y);
                    _rec.Feed(new RawTouch(st.Index, st.Pressed ? TouchPhase.Down : TouchPhase.Up, st.Position.X, st.Position.Y, Now()));
                    break;

                case InputEventScreenDrag sd:
                    LastPos = sd.Position;
                    _rec.Feed(new RawTouch(sd.Index, TouchPhase.Move, sd.Position.X, sd.Position.Y, Now()));
                    break;

                case InputEventMouseButton mb:
                    Reconfigure();
                    if (mb.ButtonIndex == MouseButton.Left)
                    {
                        LastPos = mb.Position;
                        if (mb.Pressed) BeforeTouch?.Invoke(mb.Position.X, mb.Position.Y);
                        _rec.Feed(new RawTouch(MouseId, mb.Pressed ? TouchPhase.Down : TouchPhase.Up, mb.Position.X, mb.Position.Y, Now()));
                    }
                    else if ((mb.ButtonIndex == MouseButton.Right || mb.ButtonIndex == MouseButton.Middle) && mb.Pressed)
                        SyntheticMultiTap(mb.ButtonIndex == MouseButton.Right ? 2 : 3, mb.Position);
                    break;

                case InputEventMouseMotion mm:
                    if ((mm.ButtonMask & MouseButtonMask.Left) != 0)
                    {
                        LastPos = mm.Position;
                        _rec.Feed(new RawTouch(MouseId, TouchPhase.Move, mm.Position.X, mm.Position.Y, Now()));
                    }
                    break;

                case InputEventKey k when k.Pressed && !k.Echo:
                    HandleKey(k.Keycode);
                    break;
            }
        }

        void SyntheticMultiTap(int fingers, Vector2 pos)
        {
            double t = Now();
            for (int i = 0; i < fingers; i++) _rec.Feed(new RawTouch(2000 + i, TouchPhase.Down, pos.X + i * 40, pos.Y, t));
            for (int i = 0; i < fingers; i++) _rec.Feed(new RawTouch(2000 + i, TouchPhase.Up, pos.X + i * 40, pos.Y, t + 0.05));
        }

        void HandleKey(Key key)
        {
            switch (key)
            {
                case Key.Space: Emit(Gesture.Tap); break;
                case Key.Enter: case Key.KpEnter: Emit(Gesture.DoubleTap); break;
                case Key.S: case Key.Down: Emit(Gesture.SwipeDown); break;
                case Key.B: case Key.Up: Emit(Gesture.SwipeUp); break;
                case Key.Left: Emit(Gesture.SwipeLeft); break;
                case Key.Right: Emit(Gesture.SwipeRight); break;
                case Key.Key2: Emit(Gesture.TwoFingerTap); break;
                case Key.Key3: Emit(Gesture.ThreeFingerTap); break;
                case Key.Q: Emit(Gesture.TwoFingerSwipeLeft); break;
                case Key.E: Emit(Gesture.TwoFingerSwipeRight); break;
            }
        }

        /// <summary>Kendini sınama / test için hareket enjekte eder.</summary>
        public void Inject(Gesture g) { Emit(g); }

        /// <summary>Kendini sınama: ham dokunma enjekte eder (sürükleme akışını gerçek tanıyıcıdan geçirir).</summary>
        public void InjectTouch(RawTouch t)
        {
            LastPos = new Vector2(t.X, t.Y);
            if (t.Phase == TouchPhase.Down) BeforeTouch?.Invoke(t.X, t.Y);
            _rec.Feed(t);
        }

        void Emit(Gesture g)
        {
            try { OnGesture?.Invoke(g); }
            catch (Exception e) { GD.PushError("Hareket işleyicisi: " + e); }
        }
    }
}
