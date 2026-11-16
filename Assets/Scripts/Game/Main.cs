using Godot;

namespace Maden.Game
{
    /// <summary>
    /// Tek sahne, kodla kurulum: siyah ekran, büyük "Maden" yazısı, son konuşma satırı.
    /// Pil: düşük kare hızı, düşük işlemci modu; ekran açık kalır.
    /// </summary>
    public partial class Main : Node
    {
        Label _status;
        Label _title;
        Speech _speech;
        string _shown = "";
        GameController _controller;

        public override void _Ready()
        {
            Engine.MaxFps = 20;
            OS.LowProcessorUsageMode = true;
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

            if (System.Array.IndexOf(OS.GetCmdlineUserArgs(), "--selftest") >= 0) SelfTest(router);
        }

        // Masaüstü duman testi: godot --headless --path . -- --selftest
        async void SelfTest(InputRouter r)
        {
            await ToSignal(GetTree().CreateTimer(0.5), SceneTreeTimer.SignalName.Timeout);
            r.Inject(Maden.Core.Gesture.Tap); // öğreticiyi atla
            for (int i = 0; i < 40; i++) { r.Inject(Maden.Core.Gesture.Tap); await ToSignal(GetTree().CreateTimer(0.05), SceneTreeTimer.SignalName.Timeout); }
            r.Inject(Maden.Core.Gesture.SwipeDown);
            r.Inject(Maden.Core.Gesture.SwipeUp);
            r.Inject(Maden.Core.Gesture.SwipeRight);
            r.Inject(Maden.Core.Gesture.DoubleTap);
            r.Inject(Maden.Core.Gesture.TwoFingerTap);
            r.Inject(Maden.Core.Gesture.ThreeFingerTap);
            r.Inject(Maden.Core.Gesture.ThreeFingerTap);
            await ToSignal(GetTree().CreateTimer(0.5), SceneTreeTimer.SignalName.Timeout);
            GD.Print("SELFTEST TAMAM");
            _controller.SaveNow();
            GetTree().Quit();
        }

        void BuildUi()
        {
            var layer = new CanvasLayer();
            AddChild(layer);
            var bg = new ColorRect { Color = Colors.Black, MouseFilter = Control.MouseFilterEnum.Ignore };
            bg.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            layer.AddChild(bg);

            int h = DisplayServer.WindowGetSize().Y;
            if (h <= 0) h = 960;

            _title = new Label
            {
                Text = "Maden",
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                MouseFilter = Control.MouseFilterEnum.Ignore
            };
            _title.AddThemeFontSizeOverride("font_size", (int)(h * 0.12f));
            _title.AddThemeColorOverride("font_color", Colors.White);
            _title.AnchorLeft = 0; _title.AnchorRight = 1; _title.AnchorTop = 0.2f; _title.AnchorBottom = 0.4f;
            layer.AddChild(_title);

            _status = new Label
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Top,
                AutowrapMode = TextServer.AutowrapMode.WordSmart,
                MouseFilter = Control.MouseFilterEnum.Ignore
            };
            _status.AddThemeFontSizeOverride("font_size", (int)(h * 0.035f));
            _status.AddThemeColorOverride("font_color", new Color(0.85f, 0.85f, 0.85f));
            _status.AnchorLeft = 0.08f; _status.AnchorRight = 0.92f; _status.AnchorTop = 0.5f; _status.AnchorBottom = 0.8f;
            layer.AddChild(_status);
        }

        public override void _Process(double delta)
        {
            string t = _speech?.LastText ?? "";
            if (t == _shown) return;
            _shown = t;
            if (t.Length > 140) t = t.Substring(0, 140) + "…";
            _status.Text = t;
        }
    }
}
