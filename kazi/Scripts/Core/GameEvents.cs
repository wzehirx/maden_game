using System;

namespace Kazi.Core
{
    /// <summary>Ödül büyüklüğü (earcon seçimi için).</summary>
    public enum Rarity { None = 0, Small = 1, Medium = 2, Big = 3, Jackpot = 4 }

    public enum EventKind
    {
        CellOpened, CellPeek,
        CardBought, CardResult, CardDiscarded, TrapHit,
        NotEnoughMoney, NoCard, BagFull,
        UpgradeBought, UpgradeAffordable, GadgetBought, AutomationBought,
        HelpMoney, CatalogUnlocked,
        AutoResult, AutoReport,
        PrestigeOffer, PrestigeDone, NodeBought,
        OfflineReport,
        ShopOpened, ShopClosed, ShopItemChanged, ShopCategoryChanged,
        Status, Info, Error
    }

    /// <summary>Çekirdekten oyun katmanına giden olay. Speech okunacak Türkçe cümledir (ayrıntı ayarına göre boş olabilir).</summary>
    public sealed class GameEvent
    {
        public EventKind Kind { get; set; }
        public string Speech { get; set; }
        public long Amount { get; set; }
        /// <summary>Sembol/gadget/yükseltme/kart kimliği (ses seçimi için).</summary>
        public string Symbol { get; set; }
        public int CellIndex { get; set; }
        public Rarity Rarity { get; set; }
        /// <summary>Konuşma süresi tahmini (sn): karakter sayısı / (CharsPerSecond × hız). Konuşma kuyruğu kuralları için.</summary>
        public double EstimatedSeconds { get; set; }

        public const double CharsPerSecond = 13.0;

        public static double Estimate(string speech, double rate)
        {
            if (string.IsNullOrEmpty(speech)) return 0;
            if (rate < 0.1) rate = 0.1;
            return speech.Length / (CharsPerSecond * rate);
        }

        public GameEvent(EventKind kind, string speech, long amount = 0, string symbol = "", int cell = -1, Rarity rarity = Rarity.None)
        {
            Kind = kind; Speech = speech ?? ""; Amount = amount; Symbol = symbol ?? ""; CellIndex = cell; Rarity = rarity;
        }

        public override string ToString() { return Kind + " " + Rarity + " " + Amount + " [" + Symbol + "] \"" + Speech + "\""; }
    }

    public sealed class OfflineResult
    {
        public double SecondsCounted;
        public long Money;
        public int Cards;
        public string Speech = "";
    }
}
