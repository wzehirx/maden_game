using System;
using System.Collections.Generic;
using Godot;
using Kazi.Core;

namespace Kazi.Game
{
    /// <summary>
    /// Tek sahne, kodla kurulum: siyah ekran, BÜYÜK yüksek kontrastlı yazılar; yazı boyutu kutuya otomatik sığdırılır
    /// (kaydırma yok). Kart ekranında kazıma ızgarası ekranı doldurur. Kare hızı: dokunma sırasında ve sonrasında
    /// ~5 sn 60, boştayken 30 (düşük işlemci modu). Otomatik kayıt GameController'dadır (30 sn + arka plana geçerken).
    /// --selftest: hareketleri enjekte edip tüm akışı sınar ve "SELFTEST TAMAM" yazar.
    /// </summary>
    public partial class Main : Node
    {
        const int ActiveFps = 60, IdleFps = 30;
        const double ActiveHoldSeconds = 5.0;

        static double _lastActivity = -100;
        /// <summary>Girişte çağrılır: ekranı hızlı kare hızına alır.</summary>
        public static void Poke() { _lastActivity = Time.GetTicksMsec() / 1000.0; }

        Speech _speech;
        SoundPlayer _sound;
        GameController _controller;
        InputRouter _router;
        bool _fast, _selftest;
        double _nextHud;

        // Kart ekranı
        Label _title, _money, _msg, _center;
        readonly List<ColorRect> _cellBg = new List<ColorRect>();
        readonly List<Label> _cellLbl = new List<Label>();
        // Dükkân / ayarlar ekranı
        Label _cat, _name, _desc, _price, _pos, _moneyShop;
        string _lastKey = "";
        CanvasLayer _layer;

        static readonly Color Yellow = new Color(1f, 0.92f, 0.25f);
        static readonly Color Green = new Color(0.45f, 1f, 0.5f);
        static readonly Color Red = new Color(1f, 0.5f, 0.45f);
        static readonly Color Cyan = new Color(0.55f, 0.92f, 1f);
        static readonly Color White = Colors.White;

        public override void _Ready()
        {
            _selftest = Array.IndexOf(OS.GetCmdlineUserArgs(), "--selftest") >= 0;
            if (_selftest) Settings.Dry = true;
            Poke();
            SetFast(true);
            DisplayServer.ScreenSetKeepOn(true);
            RenderingServer.SetDefaultClearColor(Colors.Black);
            GetTree().AutoAcceptQuit = true;

            _speech = new Speech { Name = "Speech" };
            _sound = new SoundPlayer { Name = "Sound" };
            var haptics = new Haptics { Name = "Haptics" };
            _router = new InputRouter { Name = "Input" };
            AddChild(_speech); AddChild(_sound); AddChild(haptics); AddChild(_router);

            BuildUi();

            _controller = new GameController { Name = "Game", Speech = _speech, Sound = _sound, Router = _router, SelfTest = _selftest };
            AddChild(_controller);
            _lastKey = "";
            Render();

            if (_selftest) RunSelfTest();
        }

        void SetFast(bool fast)
        {
            _fast = fast;
            Engine.MaxFps = fast ? ActiveFps : IdleFps;
            OS.LowProcessorUsageMode = !fast;
        }

        // ---------- Arayüz kurulumu ----------

        Label MakeLabel(Color color, float top, float bottom, float left = 0.04f, float right = 0.96f)
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
            l.AnchorLeft = left; l.AnchorRight = right; l.AnchorTop = top; l.AnchorBottom = bottom;
            l.OffsetLeft = l.OffsetRight = l.OffsetTop = l.OffsetBottom = 0;
            _layer.AddChild(l);
            return l;
        }

        void BuildUi()
        {
            _layer = new CanvasLayer();
            AddChild(_layer);
            var bg = new ColorRect { Color = Colors.Black, MouseFilter = Control.MouseFilterEnum.Ignore };
            bg.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            _layer.AddChild(bg);

            // Kazıma hücreleri (en çok 9)
            for (int i = 0; i < 9; i++)
            {
                var r = new ColorRect { MouseFilter = Control.MouseFilterEnum.Ignore, Visible = false };
                _layer.AddChild(r);
                var l = new Label
                {
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    AutowrapMode = TextServer.AutowrapMode.WordSmart,
                    MouseFilter = Control.MouseFilterEnum.Ignore,
                    Visible = false
                };
                l.AddThemeColorOverride("font_color", White);
                _layer.AddChild(l);
                _cellBg.Add(r); _cellLbl.Add(l);
            }

            // Kart ekranı başlığı
            _title = MakeLabel(Yellow, 0.010f, 0.095f);
            _money = MakeLabel(Green, 0.095f, 0.155f);
            _msg = MakeLabel(White, 0.155f, 0.220f);
            // Ortadaki büyük mesaj (kart yok, öğretici, prestij teklifi)
            _center = MakeLabel(White, 0.225f, 0.985f);

            // Dükkân / ayarlar ekranı (ekranın tamamını doldurur)
            _cat = MakeLabel(Cyan, 0.02f, 0.12f);
            _name = MakeLabel(Yellow, 0.13f, 0.38f);
            _desc = MakeLabel(White, 0.39f, 0.56f);
            _price = MakeLabel(Green, 0.57f, 0.74f);
            _pos = MakeLabel(Cyan, 0.75f, 0.85f);
            _moneyShop = MakeLabel(White, 0.86f, 0.98f);
            SetMode(0);
        }

        // 0 = kart, 1 = dükkân/ayarlar, 2 = ortada büyük mesaj
        void SetMode(int mode)
        {
            bool card = mode == 0, shop = mode == 1, msg = mode == 2;
            _title.Visible = _money.Visible = _msg.Visible = card;
            _center.Visible = msg;
            _cat.Visible = _name.Visible = _desc.Visible = _price.Visible = _pos.Visible = _moneyShop.Visible = shop;
            if (!card) for (int i = 0; i < _cellBg.Count; i++) { _cellBg[i].Visible = false; _cellLbl[i].Visible = false; }
        }

        /// <summary>Metni kutuya (genişlik x yükseklik) sığacak en büyük yazı boyutuyla yazar.</summary>
        void Fit(Label l, string text, int maxSize, int minSize, float boxW = -1f, float boxH = -1f)
        {
            l.Text = text;
            var view = GetViewport().GetVisibleRect().Size;
            if (boxW < 0) boxW = (l.AnchorRight - l.AnchorLeft) * view.X * 0.98f;
            if (boxH < 0) boxH = (l.AnchorBottom - l.AnchorTop) * view.Y * 0.96f;
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

        static string Tr(string s) => s.ToUpper(new System.Globalization.CultureInfo("tr-TR"));

        void Render()
        {
            var view = GetViewport().GetVisibleRect().Size;
            if (view.X <= 0 || view.Y <= 0) return;
            float h = view.Y, w = view.X;
            GameController.GridRect = new Rect2(w * 0.02f, h * 0.225f, w * 0.96f, h * 0.76f);
            var session = _controller?.Session;
            if (session == null)
            {
                string key0 = "F|" + view;
                if (key0 == _lastKey) return;
                _lastKey = key0;
                SetMode(2);
                Fit(_center, "Oyun yüklenemedi", (int)(h * 0.08f), 24);
                return;
            }
            var menu = _controller.Menu;

            if (menu.IsOpen)
            {
                string cat = "AYARLAR " + (menu.Index + 1) + " / " + SettingsMenu.ItemCount;
                string name = menu.ItemName, val = menu.Value();
                string key1 = "M|" + cat + name + val + view;
                if (key1 == _lastKey) return;
                _lastKey = key1;
                SetMode(1);
                Fit(_cat, cat, (int)(h * 0.045f), 16);
                Fit(_name, name, (int)(h * 0.09f), 24);
                Fit(_desc, val, (int)(h * 0.11f), 24);
                _desc.AddThemeColorOverride("font_color", Yellow);
                Fit(_price, "Yukarı / aşağı kaydır: değiştir\nSola / sağa kaydır: ayar seç", (int)(h * 0.04f), 16);
                _price.AddThemeColorOverride("font_color", White);
                Fit(_pos, "", 18, 18);
                Fit(_moneyShop, "Kapatmak için üç parmakla dokun", (int)(h * 0.04f), 16);
                return;
            }

            if (_controller.TutorialPlaying)
            {
                string key2 = "T|" + view;
                if (key2 == _lastKey) return;
                _lastKey = key2;
                SetMode(2);
                _center.AddThemeColorOverride("font_color", Yellow);
                Fit(_center, "ÖĞRETİCİ\n\nAtlamak için ekrana dokun", (int)(h * 0.08f), 24);
                return;
            }

            if (session.ShopOpen)
            {
                var v = _controller.GetShopView();
                var e = v.Entry;
                string cat = "KATEGORİ " + v.CategoryPos + " / " + v.CategoryCount + ": " + Tr(v.CategoryName);
                string name = e != null ? e.Name : "Dükkân boş";
                string desc = e != null ? v.Desc : "Alınacak bir şey kalmadı";
                string price = e != null ? v.PriceLine : "";
                string pos = e != null ? "Sıra " + v.Pos + " / " + v.Total : "";
                string money = "Paran: " + NumberFormat.Lira(session.State.Money) + (session.State.AB > 0 ? "\nAltın Bilet puanı: " + session.State.AB : "");
                string key3 = "S|" + cat + name + desc + price + pos + money + view;
                if (key3 == _lastKey) return;
                _lastKey = key3;
                SetMode(1);
                Fit(_cat, cat, (int)(h * 0.045f), 16);
                Fit(_name, name, (int)(h * 0.10f), 24);
                _desc.AddThemeColorOverride("font_color", White);
                Fit(_desc, desc, (int)(h * 0.055f), 18);
                bool afford = e == null || v.Affordable;
                _price.AddThemeColorOverride("font_color", afford ? Green : Red);
                Fit(_price, price, (int)(h * 0.075f), 18);
                Fit(_pos, pos, (int)(h * 0.07f), 18);
                Fit(_moneyShop, money, (int)(h * 0.05f), 16);
                return;
            }

            if (_controller.PrestigePrompt)
            {
                string key4 = "P|" + session.PrestigeGain() + view;
                if (key4 == _lastKey) return;
                _lastKey = key4;
                SetMode(2);
                _center.AddThemeColorOverride("font_color", Yellow);
                Fit(_center, "ALTIN BİLET!\n+" + session.PrestigeGain() + " puan\n\nÇift dokun: sıfırla ve puanı al\nAşağı kaydır: reddet", (int)(h * 0.08f), 22);
                return;
            }

            // Kart ekranı
            var st = session.State;
            var c = st.Current;
            string title, money2;
            if (c == null) title = "Kart yok";
            else if (c.Done) title = c.Type.Name + " - bitti";
            else title = c.Type.Name + " - " + NumberFormat.Lira(c.Paid);
            money2 = "Para: " + NumberFormat.Lira(st.Money) + (st.Bag.Count > 0 || session.Stats.BagCap > 1 ? "   Çanta: " + st.Bag.Count + " / " + session.Stats.BagCap : "");
            string last = _speech?.LastText ?? "";
            if (c != null && c.Done && !string.IsNullOrEmpty(_controller.LastResultText)) last = _controller.LastResultText + "  Aşağı kaydır: sıradaki";
            if (last.Length > 160) last = last.Substring(0, 160) + "…";

            // Hücre durum anahtarı
            var sb = new System.Text.StringBuilder();
            if (c != null) for (int i = 0; i < c.Cells.Length; i++) sb.Append(c.Open[i] ? 'o' : (char)('a' + (int)(c.Scratch[i] * 10)));
            string key5 = "C|" + title + money2 + last + sb + (c != null ? c.Type.Cols : 0) + "|" + view;
            if (key5 == _lastKey) return;
            _lastKey = key5;
            SetMode(0);
            Fit(_title, title, (int)(h * 0.06f), 20);
            Fit(_money, money2, (int)(h * 0.045f), 16);
            Fit(_msg, last, (int)(h * 0.035f), 14);

            if (c == null)
            {
                SetMode(2);
                _center.AddThemeColorOverride("font_color", White);
                Fit(_center, "Yukarı kaydır:\ndükkândan kart al", (int)(h * 0.08f), 24);
                _title.Visible = _money.Visible = _msg.Visible = true;
                return;
            }
            LayoutCells(c);
        }

        void LayoutCells(Card c)
        {
            int cols = Math.Max(1, c.Type.Cols);
            int n = Math.Min(c.Cells.Length, _cellBg.Count);
            int rows = Math.Max(1, (c.Cells.Length + cols - 1) / cols);
            var g = GameController.GridRect;
            float cw = g.Size.X / cols, ch = g.Size.Y / rows;
            float gap = Math.Min(cw, ch) * 0.04f;
            for (int i = 0; i < _cellBg.Count; i++)
            {
                bool vis = i < n;
                _cellBg[i].Visible = vis; _cellLbl[i].Visible = vis;
                if (!vis) continue;
                int row = i / cols, col = i % cols;
                var pos = new Vector2(g.Position.X + col * cw + gap, g.Position.Y + row * ch + gap);
                var size = new Vector2(cw - 2 * gap, ch - 2 * gap);
                _cellBg[i].Position = pos; _cellBg[i].Size = size;
                _cellLbl[i].Position = pos; _cellLbl[i].Size = size;
                string text; Color bg, fg;
                if (c.Open[i])
                {
                    text = c.DescribeCell(i);
                    if (string.IsNullOrEmpty(text)) text = "?";
                    bool trap = c.IsTrapCell(i);
                    bg = trap ? new Color(0.55f, 0.05f, 0.05f) : new Color(0.04f, 0.18f, 0.10f);
                    fg = trap ? new Color(1f, 0.85f, 0.8f) : White;
                }
                else
                {
                    float s = (float)Math.Min(1.0, c.Scratch[i] / 0.55);
                    text = (i + 1).ToString();
                    bg = new Color(0.72f - 0.45f * s, 0.66f - 0.42f * s, 0.30f - 0.15f * s); // altın sıyrık -> kararır
                    fg = s > 0.5f ? new Color(1f, 1f, 1f) : new Color(0.1f, 0.08f, 0.02f);
                }
                _cellBg[i].Color = bg;
                _cellLbl[i].AddThemeColorOverride("font_color", fg);
                Fit(_cellLbl[i], text, (int)(Math.Min(cw, ch) * 0.34f), 14, size.X * 0.92f, size.Y * 0.9f);
            }
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

        // ---------- Kendini sınama ----------

        readonly List<string> _fails = new List<string>();
        readonly List<EventKind> _seen = new List<EventKind>();
        int _checks;

        void Check(bool ok, string what)
        {
            _checks++;
            if (!ok) { _fails.Add(what); GD.PrintErr("SELFTEST BAŞARISIZ: " + what); }
            else GD.Print("  tamam: " + what);
        }

        async System.Threading.Tasks.Task Wait(double sec)
        {
            await ToSignal(GetTree().CreateTimer(sec), SceneTreeTimer.SignalName.Timeout);
        }

        static double TNow() => Time.GetTicksMsec() / 1000.0;

        /// <summary>Hücrenin ortasında kısa bir sürtme: gerçek tanıyıcı üzerinden Down, Move..., Up.</summary>
        async System.Threading.Tasks.Task Stroke(int cell, int cols, int rows, bool crossToNext)
        {
            var g = GameController.GridRect;
            float cw = g.Size.X / cols, ch = g.Size.Y / rows;
            int r = cell / cols, cc = cell % cols;
            float cx = g.Position.X + cc * cw + cw * 0.5f, cy = g.Position.Y + r * ch + ch * 0.5f;
            _router.InjectTouch(new RawTouch(7, TouchPhase.Down, cx - cw * 0.3f, cy, TNow()));
            for (int pass = 0; pass < 3; pass++)
            {
                for (int k = 0; k <= 6; k++)
                {
                    float f = k / 6f; if (pass % 2 == 1) f = 1f - f;
                    float x = cx - cw * 0.3f + cw * 0.6f * f;
                    float y = cy + (k % 2 == 0 ? -ch * 0.15f : ch * 0.15f);
                    if (crossToNext && pass == 2 && k == 6 && cc + 1 < cols) x = cx + cw * 0.9f;
                    _router.InjectTouch(new RawTouch(7, TouchPhase.Move, x, y, TNow()));
                    await Wait(0.016);
                }
            }
            _router.InjectTouch(new RawTouch(7, TouchPhase.Up, cx, cy, TNow()));
            await Wait(0.05);
        }

        async System.Threading.Tasks.Task FinishCardSequential()
        {
            var s = _controller.Session;
            for (int i = 0; i < 30 && s.State.Current != null && !s.State.Current.Done; i++)
            { _router.Inject(Gesture.Tap); await Wait(0.03); }
        }

        async System.Threading.Tasks.Task BuyFromShop(string typeId)
        {
            var s = _controller.Session;
            if (!s.ShopOpen) { _router.Inject(Gesture.SwipeUp); await Wait(0.03); }
            while (s.ShopCategory != 0) { _router.Inject(Gesture.TwoFingerSwipeRight); await Wait(0.02); }
            for (int i = 0; i < 30 && !s.ReadShopItem().StartsWith(s.Config.TypeById[typeId].Name, StringComparison.Ordinal); i++)
            { _router.Inject(Gesture.SwipeRight); await Wait(0.02); }
            _router.Inject(Gesture.DoubleTap);
            await Wait(0.05);
            if (s.ShopOpen) { _router.Inject(Gesture.SwipeUp); await Wait(0.03); }
        }

        async void RunSelfTest()
        {
            try { await SelfTestBody(); }
            catch (Exception e) { _fails.Add("istisna: " + e); GD.PrintErr(e.ToString()); }
            if (SoundPlayer.MissingCount > 0) _fails.Add("eksik ses dosyası: " + string.Join(", ", SoundPlayer.Missing));
            if (_fails.Count == 0) GD.Print("SELFTEST TAMAM (" + _checks + " denetim, eksik ses uyarısı yok)");
            else { GD.PrintErr("SELFTEST HATA: " + string.Join(" | ", _fails)); }
            GetTree().Quit(_fails.Count == 0 ? 0 : 1);
        }

        async System.Threading.Tasks.Task SelfTestBody()
        {
            await Wait(0.6);
            var s = _controller.Session;
            Check(_controller.Booted && s != null, "oyun açıldı");
            s.OnEvent += e => _seen.Add(e.Kind);

            // 1) Öğretici: tek dokunuşla atlanır
            Check(_controller.TutorialPlaying, "öğretici başladı");
            _router.Inject(Gesture.Tap); await Wait(0.05);
            Check(!_controller.TutorialPlaying, "öğretici dokunuşla atlandı");

            // 2) Dükkân: gezin, kategori, kart satın al
            long m0 = s.State.Money;
            _router.Inject(Gesture.SwipeUp); await Wait(0.05);
            Check(s.ShopOpen, "yukarı kaydır dükkânı açtı");
            _router.Inject(Gesture.SwipeRight); await Wait(0.03);
            _router.Inject(Gesture.SwipeLeft); await Wait(0.03);
            _router.Inject(Gesture.TwoFingerSwipeRight); await Wait(0.03);
            Check(s.ShopCategory == 1, "iki parmak sağa: kategori 1");
            _router.Inject(Gesture.TwoFingerSwipeLeft); await Wait(0.03);
            Check(s.ShopCategory == 0, "iki parmak sola: kategori 0");
            var v = _controller.GetShopView();
            Check(v.Entry != null && v.Pos == 1 && v.Total >= 1 && v.PriceLine.StartsWith("Fiyat"), "dükkân görünümü: sıra/fiyat");
            Render();
            _router.Inject(Gesture.DoubleTap); await Wait(0.08);
            Check(s.State.Current != null && !s.State.Current.Done, "çift dokunuş kartı aldı");
            Check(!s.ShopOpen, "kart alınınca dükkân kapandı");
            Check(s.State.Money < m0, "kart parası düştü");

            // 3) Sıralı kazıma: kart bitene kadar dokun
            Render();
            await FinishCardSequential();
            Check(s.State.Current != null && s.State.Current.Done, "sıralı kazıma kartı bitirdi");
            Check(_seen.Contains(EventKind.CellOpened) && _seen.Contains(EventKind.CardResult), "hücre ve sonuç olayları geldi");
            Render();

            // 4) Sıradaki kart
            s.State.Money = 5_000_000;
            _router.Inject(Gesture.SwipeDown); await Wait(0.05);
            Check(s.State.Current != null && !s.State.Current.Done, "aşağı kaydır sıradaki kartı verdi");

            // 5) Sürükleme modu: gerçek dokunma akışı
            s.Mode = ScratchMode.Drag; Render();
            int cols = Math.Max(1, s.State.Current.Type.Cols), cells = s.State.Current.Type.Cells;
            int rows = (cells + cols - 1) / cols;
            long snd0 = _controller.SoundsTriggered;
            for (int round = 0; round < 6 && !s.State.Current.Done; round++)
                for (int i = 0; i < cells && !s.State.Current.Done; i++)
                    if (!s.State.Current.Open[i]) await Stroke(i, cols, rows, i % 2 == 0);
            Check(s.State.Current.Done, "sürükleme kazıma kartı bitirdi (hücre eşiği)");
            Check(_seen.Contains(EventKind.CellOpened), "sürüklemede hücre açıldı");
            Check(_controller.SoundsTriggered > snd0, "sürükleme ses/olay tetikledi");
            // Sürükleme modunda kart bitince kaydırma serbest
            _router.Inject(Gesture.SwipeDown); await Wait(0.05);
            Check(s.State.Current != null && !s.State.Current.Done, "sürükleme sonrası sıradaki kart");
            // Dokunma (Tap) bir hücreyi kazır
            int tapCell = 0;
            _router.InjectTouch(new RawTouch(9, TouchPhase.Down, GameController.GridRect.Position.X + 20, GameController.GridRect.Position.Y + 20, TNow()));
            _router.InjectTouch(new RawTouch(9, TouchPhase.Up, GameController.GridRect.Position.X + 20, GameController.GridRect.Position.Y + 20, TNow() + 0.04));
            await Wait(0.05);
            Check(s.State.Current.Scratch[tapCell] > 0 || s.State.Current.Open[tapCell], "sürükleme modunda dokunma hücreyi kazıdı");
            s.Mode = ScratchMode.Sequential;
            await FinishCardSequential();

            // 6) Tuzaklı kart: yarıda bırak
            CardTypeDef trap = null;
            foreach (var t in s.Config.CardTypes) if (t.IsTrapType) { trap = t; break; }
            Check(trap != null, "tuzaklı kart türü var");
            if (trap != null)
            {
                s.State.UnlockedCatalog = s.Config.CatalogCount; s.State.Money = 50_000_000;
                s.State.Current = null;
                Check(s.BuyCard(trap.Id), "tuzaklı kart alındı");
                _router.Inject(Gesture.Tap); await Wait(0.03);
                var tc = s.State.Current;
                long mm = s.State.Money;
                if (!tc.Done) { _router.Inject(Gesture.SwipeDown); await Wait(0.05); }
                Check(tc.Discarded || tc.Done, "aşağı kaydır tuzaklı kartı bıraktı");
                if (tc.Discarded) Check(s.State.Money >= mm, "çöpe atınca iade geldi");
            }

            // 7) Dükkân: yükseltme, otomasyon, gadget; ses döngüleri
            s.State.UnlockedCatalog = 1; s.State.Money = 50_000_000;
            int lv0 = s.State.TotalLevels;
            _router.Inject(Gesture.SwipeUp); await Wait(0.03);
            _router.Inject(Gesture.TwoFingerSwipeRight); await Wait(0.03); // Kazıma
            _router.Inject(Gesture.DoubleTap); await Wait(0.05);
            Check(s.State.TotalLevels > lv0, "yükseltme satın alındı");
            _router.Inject(Gesture.TwoFingerSwipeRight); await Wait(0.03); // Otomasyon
            _router.Inject(Gesture.DoubleTap); await Wait(0.05);
            Check(s.Stats.AutoOwned, "otomatik kazıyıcı satın alındı");
            _controller.RefreshLoops();
            Check(_sound.LoopOn("auto_scratch_loop"), "otomatik kazıyıcı döngü sesi açık");
            Render();
            for (int i = 0; i < 5; i++)
            {
                var vv = _controller.GetShopView();
                Check(vv.CategoryPos == s.ShopCategory + 1, "kategori " + vv.CategoryName + " görünümü");
                _router.Inject(Gesture.TwoFingerSwipeRight); await Wait(0.03);
                Render();
            }
            // Gadget kategorisi: sayaç
            while (s.ShopCategory != 3) { _router.Inject(Gesture.TwoFingerSwipeRight); await Wait(0.02); }
            for (int i = 0; i < 6; i++)
            {
                if (s.ReadShopItem().StartsWith("Mekanik", StringComparison.Ordinal)) break;
                _router.Inject(Gesture.SwipeRight); await Wait(0.02);
            }
            _router.Inject(Gesture.DoubleTap); await Wait(0.05);
            _router.Inject(Gesture.SwipeUp); await Wait(0.03); // kapat
            Check(!s.ShopOpen, "dükkân yukarı kaydırınca kapandı");
            var off = s.ApplyOffline(600);
            Check(off.SecondsCounted > 0, "çevrimdışı kazanç hesaplandı: " + off.Speech);
            s.Tick(70); // otomasyon özeti
            await Wait(0.1);

            // 8) Ayarlar menüsü
            _router.Inject(Gesture.ThreeFingerTap); await Wait(0.05);
            Check(_controller.Menu.IsOpen, "üç parmak: ayarlar açıldı");
            Render();
            var detail0 = s.Detail; double rate0 = s.SpeechRate;
            _router.Inject(Gesture.SwipeRight); await Wait(0.02); _router.Inject(Gesture.SwipeUp); await Wait(0.02);   // efekt sesi +
            _router.Inject(Gesture.SwipeRight); await Wait(0.02); _router.Inject(Gesture.SwipeUp); await Wait(0.02);   // konuşma hızı +
            Check(Math.Abs(s.SpeechRate - (rate0 + 0.25)) < 1e-6 && Math.Abs(Settings.SpeechRate - s.SpeechRate) < 1e-6, "konuşma hızı 0,25 arttı ve uygulandı");
            _router.Inject(Gesture.SwipeRight); await Wait(0.02); _router.Inject(Gesture.SwipeUp); await Wait(0.02);   // ayrıntı
            Check(s.Detail != detail0, "konuşma ayrıntısı değişti");
            for (int i = 0; i < 3; i++) { _router.Inject(Gesture.SwipeRight); await Wait(0.02); } // motor, ses, titreşim
            _router.Inject(Gesture.SwipeRight); await Wait(0.02);                                    // kazıma modu
            _router.Inject(Gesture.SwipeUp); await Wait(0.02);
            Check(s.Mode == ScratchMode.Drag, "kazıma modu sürükleme oldu");
            _router.Inject(Gesture.SwipeDown); await Wait(0.02);
            Check(s.Mode == ScratchMode.Sequential, "kazıma modu sıralıya döndü");
            _router.Inject(Gesture.SwipeRight); await Wait(0.02); _router.Inject(Gesture.SwipeUp); await Wait(0.02);   // çöpe at
            Check(s.State.AutoDiscardTrap, "tuzakta çöpe at açıldı");
            _router.Inject(Gesture.SwipeRight); await Wait(0.02); _router.Inject(Gesture.SwipeDown); await Wait(0.02); // bildirim kapat
            Check(!s.State.NotifyAffordable, "yükseltme bildirimi kapandı");
            _router.Inject(Gesture.SwipeDown); await Wait(0.02);
            Render();
            _router.Inject(Gesture.ThreeFingerTap); await Wait(0.05);
            Check(!_controller.Menu.IsOpen, "üç parmak: ayarlar kapandı");
            s.State.NotifyAffordable = true; s.Detail = SpeechDetail.Full;

            // 9) Durum okuma
            _router.Inject(Gesture.TwoFingerTap); await Wait(0.05);
            Check(_seen.Contains(EventKind.Status), "iki parmak dokunuş durum okudu");

            // 10) Prestij: Altın Bilet kartı -> teklif -> reddet; tekrar -> kabul -> ağaç
            CardTypeDef gold = null;
            foreach (var t in s.Config.CardTypes) if (t.Rule == CardRule.AltinBilet) { gold = t; break; }
            Check(gold != null, "Altın Bilet Son Şansı kartı var");
            if (gold != null)
            {
                for (int round = 0; round < 2; round++)
                {
                    s.State.UnlockedCatalog = s.Config.CatalogCount; s.State.Money = 900_000_000_000;
                    s.State.Upgrades["__selftest"] = 100000; // kapı yükseltme eşiği
                    s.State.Pity[gold.Id] = 1000;            // garanti kazanç
                    s.State.Current = null;
                    s.RecalcStats();
                    Check(s.BuyCard(gold.Id), "Altın Bilet kartı alındı (tur " + (round + 1) + ")");
                    await FinishCardSequential();
                    await Wait(0.05);
                    Check(s.PrestigeOffered && _controller.PrestigePrompt, "prestij teklifi sesli sunuldu");
                    Render();
                    if (round == 0)
                    {
                        _router.Inject(Gesture.SwipeDown); await Wait(0.05);
                        Check(!s.PrestigeOffered && !_controller.PrestigePrompt, "aşağı kaydır teklifi reddetti");
                    }
                    else
                    {
                        long ab0 = s.State.AB;
                        _router.Inject(Gesture.DoubleTap); await Wait(0.05);
                        Check(!s.PrestigeOffered && s.State.PrestigeCount >= 1, "çift dokunuş sıfırlamayı kabul etti");
                        Check(s.State.AB >= ab0, "Altın Bilet puanı verildi: " + s.State.AB);
                    }
                }
                // Prestij sonrası ağaç dükkânı
                s.State.AB += 1000;
                _router.Inject(Gesture.SwipeUp); await Wait(0.03);
                while (s.ShopCategory != 4) { _router.Inject(Gesture.TwoFingerSwipeRight); await Wait(0.02); }
                _router.Inject(Gesture.SwipeRight); await Wait(0.03);
                var nv = _controller.GetShopView();
                Check(nv.Entry != null && nv.Entry.Kind == ShopItemKind.Node, "prestij ağacı düğümü dükkânda: " + (nv.Entry != null ? nv.Entry.Name : "yok"));
                Render();
                int nodes0 = s.State.Nodes.Count;
                _router.Inject(Gesture.DoubleTap); await Wait(0.05);
                Check(s.State.Nodes.Count > nodes0, "ağaç düğümü satın alındı");
                _router.Inject(Gesture.SwipeUp); await Wait(0.03);
            }

            // 11) Kayıt/yükleme
            long money = s.State.Money;
            _controller.SaveNow();
            var re = SaveSystem.LoadFromFile(ProjectSettings.GlobalizePath(GameController.SelfTestSavePath), s.Config);
            Check(re.Money == money, "kayıt yazıldı ve yeniden okundu");

            // 12) Ses
            Check(SoundPlayer.MissingCount == 0, "eksik ses dosyası yok (" + SoundPlayer.MissingCount + ")");
            await Wait(0.3);

            // 13) GERÇEK SES ENERJİSİ: Master'a (limiter sonrası, donanıma giden nokta) AudioEffectCapture takılır;
            //     her yol çalınır ve yakalanan örneklerin RMS'i ölçülür. (Dummy sürücü karışımı yine çalıştırır.)
            await AudioEnergyChecks();
        }

        /// <summary>Bus üzerine geçici yakalama efekti ekler, tetikleyiciyi çalıştırır, bekler, RMS (lineer) döndürür.</summary>
        async System.Threading.Tasks.Task<double> MeasureRms(string bus, Action trigger, double waitSec = 0.35)
        {
            int idx = AudioServer.GetBusIndex(bus);
            if (idx < 0) return -1;
            var cap = new AudioEffectCapture { BufferLength = 1.0f };
            AudioServer.AddBusEffect(idx, cap);
            int eff = AudioServer.GetBusEffectCount(idx) - 1;
            cap.ClearBuffer();
            trigger();
            await Wait(waitSec);
            int n = cap.GetFramesAvailable();
            double sum = 0; int cnt = 0;
            if (n > 0)
            {
                var buf = cap.GetBuffer(n);
                foreach (var f in buf) { sum += f.X * f.X + f.Y * f.Y; cnt += 2; }
            }
            AudioServer.RemoveBusEffect(idx, eff);
            return cnt > 0 ? Math.Sqrt(sum / cnt) : 0.0;
        }

        async System.Threading.Tasks.Task AudioEnergyChecks()
        {
            const double Thr = 0.0005; // ~ -66 dBFS: sessizden açıkça ayrılır
            var snd = _sound;
            snd.StopAllForTest();
            await Wait(0.2);
            double Db(double r) => r <= 1e-9 ? -999 : 20 * Math.Log10(r);
            string Fmt(double r) => r.ToString("0.00000") + " (" + Db(r).ToString("0.0") + " dBFS)";

            double silence = await MeasureRms("Master", () => { }, 0.25);
            GD.Print("  ses ölçümü: sessizlik tabanı Master RMS=" + Fmt(silence));
            Check(silence < Thr, "ölçüm tabanı sessiz (hiçbir şey çalmazken Master RMS " + Fmt(silence) + ")");

            // Her satır: ad, kaynak bus (efekt öncesi), tetikleyici
            var cases = new List<(string name, string srcBus, Action act)>
            {
                ("efekt cell_reveal", "Sfx", () => snd.PlaySfx("cell_reveal")),
                ("efekt win_small", "Sfx", () => snd.PlaySfx("win_small")),
                ("efekt coin_slide", "Sfx", () => snd.PlaySfx("coin_slide")),
                ("kazıma (pan sol)", "ScratchPan0", () => snd.PlayScratch(1f, 0f)),
                ("kazıma (pan orta)", "ScratchPan2", () => snd.PlayScratch(1f, 0.5f)),
                ("kazıma (pan sağ)", "ScratchPan4", () => snd.PlayScratch(1f, 1f)),
                ("sınır tıkı", "ScratchPan2", () => snd.PlayBoundary(0.5f)),
                ("döngü auto_scratch_loop", "Loops", () => snd.SetLoop("auto_scratch_loop", true)),
                ("müzik", "Music", () => snd.StartMusic()),
                ("ortam", "Ambient", () => { }), // StartMusic ortamı da başlatır; aşağıda ayrıca ölçülür
            };
            foreach (var c in cases)
            {
                snd.StopAllForTest();
                await Wait(0.15);
                double src = -1;
                double master;
                if (c.name == "müzik" || c.name == "ortam")
                {
                    // müzik/ortam çapraz geçişle yumuşak açılır: daha uzun bekle
                    var t = c.name == "müzik" ? (Action)(() => snd.StartMusic()) : () => snd.StartMusic();
                    master = await MeasureRms("Master", t, 1.6);
                    src = -1;
                }
                else
                {
                    // kaynak bus ve Master'ı aynı tetiklemede ölç: önce Master'a yakalama ekle, sonra kaynak bus'a
                    int mi = AudioServer.GetBusIndex("Master"), si = AudioServer.GetBusIndex(c.srcBus);
                    var capM = new AudioEffectCapture { BufferLength = 1.0f };
                    var capS = new AudioEffectCapture { BufferLength = 1.0f };
                    AudioServer.AddBusEffect(mi, capM);
                    AudioServer.AddBusEffect(si, capS);
                    int em = AudioServer.GetBusEffectCount(mi) - 1, es = AudioServer.GetBusEffectCount(si) - 1;
                    capM.ClearBuffer(); capS.ClearBuffer();
                    c.act();
                    await Wait(0.4);
                    double Rms(AudioEffectCapture cp)
                    {
                        int n = cp.GetFramesAvailable(); if (n <= 0) return 0;
                        double sum = 0; int cnt = 0;
                        foreach (var f in cp.GetBuffer(n)) { sum += f.X * f.X + f.Y * f.Y; cnt += 2; }
                        return Math.Sqrt(sum / cnt);
                    }
                    master = Rms(capM); src = Rms(capS);
                    AudioServer.RemoveBusEffect(si, es);
                    AudioServer.RemoveBusEffect(mi, em);
                }
                GD.Print("  ses ölçümü: " + c.name + " -> Master RMS=" + Fmt(master) + (src >= 0 ? "  | kaynak bus " + c.srcBus + " RMS=" + Fmt(src) : ""));
                Check(master > Thr, "ses enerjisi Master'a ulaştı: " + c.name + " (RMS " + Fmt(master) + ")");
            }
            snd.StopAllForTest();
            snd.WriteDiag();
            GD.Print(snd.BuildDiag());
        }
    }
}
