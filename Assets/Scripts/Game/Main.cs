using System;
using Godot;
using Maden.Core;

namespace Maden.Game
{
    /// <summary>
    /// Tek sahne, kodla kurulum: siyah ekran, büyük yüksek kontrastlı yazılar. Yazı boyutu ekrana göre otomatik
    /// sığdırılır (kaydırma yok). Dükkân açıkken ekranda: kategori, öğe adı, etki, fiyat, sıra, para.
    /// Kare hızı: dokunma sırasında ve sonrasında 60, boştayken 30 (düşük işlemci modu).
    /// </summary>
    public partial class Main : Node
    {
        const int ActiveFps = 60, IdleFps = 30;
        const double ActiveHoldSeconds = 5.0;

        static double _lastActivity = -100;
        /// <summary>Girişte çağrılır: ekranı hızlı kare hızına alır.</summary>
        public static void Poke() { _lastActivity = Time.GetTicksMsec() / 1000.0; }

        Speech _speech;
        GameController _controller;
        bool _fast;
        double _nextHud;

        // Normal ekran
        Label _title, _last, _hud;
        // Dükkân ekranı
        Label _cat, _name, _desc, _price, _pos, _money;
        string _lastKey = "";
        Vector2 _lastViewSize;

        static readonly Color Yellow = new Color(1f, 0.92f, 0.25f);
        static readonly Color Green = new Color(0.45f, 1f, 0.5f);
        static readonly Color Red = new Color(1f, 0.5f, 0.45f);
        static readonly Color Cyan = new Color(0.55f, 0.92f, 1f);

        public override void _Ready()
        {
            Poke();
            SetFast(true);
            DisplayServer.ScreenSetKeepOn(true);
            RenderingServer.SetDefaultClearColor(Colors.Black);
            GetTree().AutoAcceptQuit = true;

            _speech = new Speech { Name = "Speech" };
            var sound = new SoundPlayer { Name = "Sound" };
            var haptics = new Haptics { Name = "Haptics" };
            var router = new InputRouter { Name = "Input" };
            AddChild(_speech); AddChild(sound); AddChild(haptics); AddChild(router);

            BuildUi();

            _controller = new GameController { Name = "Game", Speech = _speech, Sound = sound, Router = router };
            AddChild(_controller);
            _lastKey = "";
            Render();

            if (System.Array.IndexOf(OS.GetCmdlineUserArgs(), "--selftest") >= 0) SelfTest(router);
        }

        void SetFast(bool fast)
        {
            _fast = fast;
            Engine.MaxFps = fast ? ActiveFps : IdleFps;
            OS.LowProcessorUsageMode = !fast;
        }

        // Masaüstü duman testi: godot --headless --path . -- --selftest
        async void SelfTest(InputRouter r)
        {
            await ToSignal(GetTree().CreateTimer(0.5), SceneTreeTimer.SignalName.Timeout);
            r.Inject(Gesture.Tap); // öğreticiyi atla
            for (int i = 0; i < 40; i++) { r.Inject(Gesture.Tap); await ToSignal(GetTree().CreateTimer(0.05), SceneTreeTimer.SignalName.Timeout); }
            r.Inject(Gesture.SwipeDown);
            r.Inject(Gesture.SwipeUp);        // dükkân aç
            r.Inject(Gesture.SwipeRight);
            r.Inject(Gesture.TwoFingerSwipeRight); // kategori
            r.Inject(Gesture.TwoFingerSwipeLeft);
            r.Inject(Gesture.DoubleTap);
            r.Inject(Gesture.SwipeUp);        // dükkân kapat
            r.Inject(Gesture.TwoFingerTap);
            r.Inject(Gesture.ThreeFingerTap); // ayarlar
            for (int i = 0; i < 4; i++) { r.Inject(Gesture.SwipeRight); r.Inject(Gesture.SwipeUp); }
            r.Inject(Gesture.ThreeFingerTap);
            await ToSignal(GetTree().CreateTimer(0.5), SceneTreeTimer.SignalName.Timeout);
            GD.Print("SELFTEST TAMAM");
            _controller.SaveNow();
            GetTree().Quit();
        }

        // ---------- Arayüz ----------

        Label MakeLabel(CanvasLayer layer, Color color, float top, float bottom, float left = 0.04f, float right = 0.96f)
        {
            var l = new Label
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                AutowrapMode = TextServer.AutowrapMode.WordSmart,
                MouseFilter = Control.MouseFilterEnum.Ignore,
                ClipText = false
            };
            l.AddThemeColorOverride("font_color", color);
            l.AddThemeColorOverride("font_outline_color", Colors.Black);
            l.AddThemeConstantOverride("outline_size", 0);
            l.AnchorLeft = left; l.AnchorRight = right; l.AnchorTop = top; l.AnchorBottom = bottom;
            l.OffsetLeft = l.OffsetRight = l.OffsetTop = l.OffsetBottom = 0;
            layer.AddChild(l);
            return l;
        }

        void BuildUi()
        {
            var layer = new CanvasLayer();
            AddChild(layer);
            var bg = new ColorRect { Color = Colors.Black, MouseFilter = Control.MouseFilterEnum.Ignore };
            bg.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            layer.AddChild(bg);

            // Normal ekran
            _title = MakeLabel(layer, Colors.White, 0.03f, 0.18f);
            _last = MakeLabel(layer, new Color(1f, 1f, 1f), 0.20f, 0.60f);
            _hud = MakeLabel(layer, Yellow, 0.63f, 0.97f);

            // Dükkân ekranı (ekranın tamamını doldurur)
            _cat = MakeLabel(layer, Cyan, 0.02f, 0.12f);
            _name = MakeLabel(layer, Yellow, 0.13f, 0.38f);
            _desc = MakeLabel(layer, Colors.White, 0.39f, 0.56f);
            _price = MakeLabel(layer, Green, 0.57f, 0.74f);
            _pos = MakeLabel(layer, Cyan, 0.75f, 0.85f);
            _money = MakeLabel(layer, Colors.White, 0.86f, 0.98f);
            SetShopVisible(false);
        }

        void SetShopVisible(bool shop)
        {
            _title.Visible = _last.Visible = _hud.Visible = !shop;
            _cat.Visible = _name.Visible = _desc.Visible = _price.Visible = _pos.Visible = _money.Visible = shop;
        }

        /// <summary>Metni kutuya (genişlik x yükseklik) sığacak en büyük yazı boyutuyla yazar.</summary>
        void Fit(Label l, string text, int maxSize, int minSize)
        {
            l.Text = text;
            var view = GetViewport().GetVisibleRect().Size;
            float boxW = (l.AnchorRight - l.AnchorLeft) * view.X * 0.98f;
            float boxH = (l.AnchorBottom - l.AnchorTop) * view.Y * 0.96f;
            var font = l.GetThemeDefaultFont();
            int size = maxSize;
            if (font != null)
            {
                while (size > minSize)
                {
                    var sz = font.GetMultilineStringSize(text, HorizontalAlignment.Center, boxW, size);
                    if (sz.Y <= boxH && sz.X <= boxW + 1f) break;
                    size = Math.Max(minSize, (int)(size * 0.92f));
                }
            }
            l.AddThemeFontSizeOverride("font_size", size);
        }

        // ---------- Çizim ----------

        void Render()
        {
            var view = GetViewport().GetVisibleRect().Size;
            if (view.X <= 0 || view.Y <= 0) return;
            float h = view.Y, w = view.X;
            var session = _controller?.Session;
            bool shop = session != null && session.ShopOpen;

            string key;
            if (shop)
            {
                var v = session.GetShopView();
                var e = v.Entry;
                string cat = "KATEGORİ " + v.CategoryPos + " / " + v.CategoryCount + ": " + v.CategoryName.ToUpper(new System.Globalization.CultureInfo("tr-TR"));
                string name = e != null ? e.Name : "Dükkân boş";
                string desc = e != null ? e.Desc + (e.Kind == ShopKind.Upgrade ? "\nSeviye " + e.Level + " / " + e.MaxLevel : "") : "Alınacak bir şey kalmadı";
                string price = e != null ? "Fiyat: " + NumberFormat.Lira(e.Cost) + (v.Affordable ? "" : "\nEksik: " + NumberFormat.Lira(v.Shortfall)) : "";
                string pos = e != null ? "Sıra " + v.Pos + " / " + v.Total : "";
                string money = "Paran: " + NumberFormat.Lira(session.State.Money);
                key = "S|" + cat + name + desc + price + pos + money + view;
                if (key == _lastKey) return;
                _lastKey = key;
                SetShopVisible(true);
                Fit(_cat, cat, (int)(h * 0.045f), 16);
                Fit(_name, name, (int)(h * 0.11f), 24);
                Fit(_desc, desc, (int)(h * 0.06f), 20);
                Fit(_price, price, (int)(h * 0.08f), 20);
                _price.AddThemeColorOverride("font_color", e == null || v.Affordable ? Green : Red);
                Fit(_pos, pos, (int)(h * 0.07f), 18);
                Fit(_money, money, (int)(h * 0.05f), 16);
                return;
            }

            string last = _speech?.LastText ?? "";
            if (last.Length > 220) last = last.Substring(0, 220) + "…";
            string hud = "";
            if (session != null)
            {
                var st = session.State;
                hud = "Para: " + NumberFormat.Lira(st.Money) + "\nKatman: " + session.CurrentLayer.Name +
                      "\nÇanta: " + st.BagCount + " / " + session.BagCapacity;
            }
            key = "N|" + last + hud + view;
            if (key == _lastKey) return;
            _lastKey = key;
            SetShopVisible(false);
            Fit(_title, "MADEN", (int)(h * 0.12f), 32);
            Fit(_last, last, (int)(h * 0.065f), 22);
            Fit(_hud, hud, (int)(h * 0.07f), 20);
        }

        public override void _Process(double delta)
        {
            double now = Time.GetTicksMsec() / 1000.0;
            bool wantFast = now - _lastActivity < ActiveHoldSeconds;
            if (wantFast != _fast) SetFast(wantFast);

            bool dirty = _controller != null && _controller.HudDirty;
            if (dirty || now >= _nextHud)
            {
                if (_controller != null) _controller.HudDirty = false;
                _nextHud = now + 0.5;
                Render();
            }
        }
    }
}
