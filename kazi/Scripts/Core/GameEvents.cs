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
