using System.Collections.Generic;
using Godot;

namespace Maden.Game
{
    public enum HapticKind { Soft, Hard, Metal, Rare, Sell, Blast, Tick, Error }

    /// <summary>
    /// Titreşim desenleri (Input.VibrateHandheld). Çok darbeli desenler _Process ile zamanlanır
    /// (düşük kare hızında aralıklar kareye yuvarlanır).
    /// </summary>
    public partial class Haptics : Node
    {
        public static Haptics Instance { get; private set; }

        // Darbe: bekleme (ms, önceki darbenin bitişinden), süre (ms), genlik 0..1
        readonly struct Pulse
        {
            public readonly int Gap, Ms; public readonly float Amp;
            public Pulse(int gap, int ms, float amp) { Gap = gap; Ms = ms; Amp = amp; }
        }

        static Pulse[] Get(HapticKind k)
        {
            switch (k)
            {
                case HapticKind.Soft:  return new[] { new Pulse(0, 22, 0.27f) };
                case HapticKind.Hard:  return new[] { new Pulse(0, 40, 1f) };
                case HapticKind.Metal: return new[] { new Pulse(0, 30, 0.82f), new Pulse(45, 30, 0.82f) };
                case HapticKind.Rare:  return new[] { new Pulse(0, 70, 1f), new Pulse(50, 70, 1f), new Pulse(50, 220, 1f) };
                case HapticKind.Sell:  return new[] { new Pulse(0, 30, 0.47f), new Pulse(30, 30, 0.67f), new Pulse(30, 60, 0.86f) };
                case HapticKind.Blast: return new[] { new Pulse(0, 140, 1f), new Pulse(40, 380, 1f) };
                case HapticKind.Error: return new[] { new Pulse(0, 120, 0.6f), new Pulse(80, 120, 0.6f) };
                default:               return new[] { new Pulse(0, 15, 0.35f) };
            }
        }

        /// <summary>Katmana göre kazma titreşimi: toprak yumuşak, taş/kömür sert, metal çift, kristal sert.</summary>
        public static HapticKind ForHitGroup(HitGroup g)
        {
            switch (g)
            {
                case HitGroup.Dirt: return HapticKind.Soft;
                case HitGroup.Metal: return HapticKind.Metal;
                default: return HapticKind.Hard;
            }
        }

        readonly Queue<(double at, Pulse p)> _queue = new Queue<(double, Pulse)>();

        public override void _Ready() { Instance = this; }

        public static void Play(HapticKind kind)
        {
            if (Instance == null || !Settings.HapticsOn) return;
            Instance.Start(kind);
        }

        void Start(HapticKind kind)
        {
            var pulses = Get(kind);
            _queue.Clear(); // yeni desen eskisini keser
            double t = Time.GetTicksMsec() / 1000.0;
            Fire(pulses[0]);
            double end = t + pulses[0].Ms / 1000.0;
            for (int i = 1; i < pulses.Length; i++)
            {
                double at = end + pulses[i].Gap / 1000.0;
                _queue.Enqueue((at, pulses[i]));
                end = at + pulses[i].Ms / 1000.0;
            }
        }

        public override void _Process(double delta)
        {
            if (_queue.Count == 0) return;
            double now = Time.GetTicksMsec() / 1000.0;
            while (_queue.Count > 0 && _queue.Peek().at <= now)
                Fire(_queue.Dequeue().p);
        }

        static void Fire(Pulse p)
        {
            // Masaüstünde VibrateHandheld işlem yapmaz.
            Input.VibrateHandheld(p.Ms, p.Amp);
        }
    }
}
