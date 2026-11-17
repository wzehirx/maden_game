using Godot;

namespace Kazi.Game
{
    public enum HapticKind { Tick, Soft, Hard, Peek, Buy, WinSmall, WinMid, WinBig, Jackpot, Lose, Trap, Error }

    /// <summary>
    /// Titreşim desenleri (Input.VibrateHandheld). İlk darbe anında; sonrakiler SceneTreeTimer ile zamanlanır
    /// (kare hızından bağımsız). Yeni desen eskisini keser.
    /// </summary>
    public partial class Haptics : Node
    {
        public static Haptics Instance { get; private set; }

        readonly struct Pulse
        {
            public readonly int Gap, Ms; public readonly float Amp;
            public Pulse(int gap, int ms, float amp) { Gap = gap; Ms = ms; Amp = amp; }
        }

        static Pulse[] Get(HapticKind k)
        {
            switch (k)
            {
                case HapticKind.Soft:     return new[] { new Pulse(0, 22, 0.30f) };
                case HapticKind.Hard:     return new[] { new Pulse(0, 40, 1f) };
                case HapticKind.Peek:     return new[] { new Pulse(0, 18, 0.5f), new Pulse(40, 18, 0.5f) };
                case HapticKind.Buy:      return new[] { new Pulse(0, 20, 0.5f), new Pulse(35, 28, 0.7f) };
                case HapticKind.WinSmall: return new[] { new Pulse(0, 28, 0.55f), new Pulse(40, 28, 0.55f) };
                case HapticKind.WinMid:   return new[] { new Pulse(0, 30, 0.6f), new Pulse(35, 30, 0.75f), new Pulse(35, 50, 0.9f) };
                case HapticKind.WinBig:   return new[] { new Pulse(0, 50, 0.8f), new Pulse(45, 50, 0.9f), new Pulse(45, 90, 1f) };
                case HapticKind.Jackpot:  return new[] { new Pulse(0, 70, 1f), new Pulse(50, 70, 1f), new Pulse(50, 220, 1f) };
                case HapticKind.Lose:     return new[] { new Pulse(0, 45, 0.28f) };
                case HapticKind.Trap:     return new[] { new Pulse(0, 90, 0.85f), new Pulse(70, 140, 0.85f) };
                case HapticKind.Error:    return new[] { new Pulse(0, 120, 0.6f), new Pulse(80, 120, 0.6f) };
                default:                  return new[] { new Pulse(0, 12, 0.32f) }; // Tick: kazıma "tık"
            }
        }

        int _gen; // yeni desen eskisini keser

        public override void _Ready() { Instance = this; ProcessMode = ProcessModeEnum.Always; }

        public static void Play(HapticKind kind)
        {
            if (Instance == null || !Settings.HapticsOn) return;
            Instance.Start(kind);
        }

        void Start(HapticKind kind)
        {
            var pulses = Get(kind);
            int gen = ++_gen;
            Input.VibrateHandheld(pulses[0].Ms, pulses[0].Amp); // ilk darbe dokunma anında, kareyi beklemeden
            if (pulses.Length == 1) return;
            double delay = pulses[0].Ms / 1000.0;
            for (int i = 1; i < pulses.Length; i++)
            {
                delay += pulses[i].Gap / 1000.0;
                var p = pulses[i];
                GetTree().CreateTimer(delay, true, false, true).Timeout += () => { if (gen == _gen) Input.VibrateHandheld(p.Ms, p.Amp); };
                delay += p.Ms / 1000.0;
            }
        }
    }
}
