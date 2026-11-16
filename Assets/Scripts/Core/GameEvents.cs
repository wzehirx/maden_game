using System;

namespace Maden.Core
{
    public enum Rarity { Common = 0, Uncommon = 1, Rare = 2 }

    public enum EventKind
    {
        Hit,
        ItemFound,
        RareFound,
        Sold,
        BagFull,
        LayerChanged,
        UpgradeBought,
        NotEnoughMoney,
        MachineBlast,
        MachineDrill,
        ShopOpened,
        ShopClosed,
        ShopItemChanged,
        AncientTreasureFound,
        Error
    }

    /// <summary>Oyundan Unity katmanına giden olay. Speech okunacak Türkçe cümledir (boş olabilir).</summary>
    public class GameEvent
    {
        public EventKind Kind { get; set; }
        public string Speech { get; set; }
        public int Layer { get; set; }
        public Rarity Rarity { get; set; }
        public long Amount { get; set; }

        public GameEvent(EventKind kind, string speech, int layer, Rarity rarity, long amount)
        {
            Kind = kind; Speech = speech ?? ""; Layer = layer; Rarity = rarity; Amount = amount;
        }

        public override string ToString()
        {
            return Kind + " L" + Layer + " " + Rarity + " " + Amount + " \"" + Speech + "\"";
        }
    }

    public class OfflineResult
    {
        public double SecondsCounted { get; set; }
        public long Money { get; set; }
        public int Blocks { get; set; }
        public int Items { get; set; }
        public bool TreasureFound { get; set; }
        public string Speech { get; set; }
    }

    public interface IRandom
    {
        double NextDouble();
    }

    public sealed class SystemRandom : IRandom
    {
        private readonly Random r;
        public SystemRandom() { r = new Random(); }
        public SystemRandom(int seed) { r = new Random(seed); }
        public double NextDouble() { return r.NextDouble(); }
    }

    /// <summary>Test için: verilen değerleri sırayla (döngüyle) döndürür.</summary>
    public sealed class FixedRandom : IRandom
    {
        private readonly double[] values;
        private int idx;
        public FixedRandom(params double[] values)
        {
            this.values = (values == null || values.Length == 0) ? new[] { 0.5 } : values;
        }
        public double NextDouble()
        {
            double v = values[idx % values.Length];
            idx++;
            if (v < 0) v = 0;
            if (v >= 1) v = 0.9999999;
            return v;
        }
    }
}
