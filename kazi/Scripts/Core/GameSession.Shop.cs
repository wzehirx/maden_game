using System;
using System.Collections.Generic;

namespace Kazi.Core
{
    public enum ShopItemKind { Card, Upgrade, Node, PrestigeReset, Toggle, Info }

    public sealed class ShopItem
    {
        public ShopItemKind Kind;
        public string Id, Name, Desc;
        public long Price;
        public bool UsesAB;
    }

    public sealed partial class GameSession
    {
        public static readonly string[] Categories = { "Kartlar", "Kazıma", "Otomasyon", "Gadget", "Prestij" };
        public bool ShopOpen { get; private set; }
        private int shopCat, shopIndex;
        private bool confirmPrestige;
        private int autoDiscardCount;

        public int ShopCategory { get { return shopCat; } }
        public string CategoryName { get { return Categories[shopCat]; } }
        internal void CountAutoDiscard() { autoDiscardCount++; autoCards++; }

        // ---- dükkân listeleri ----
        public List<ShopItem> BuildItems(int cat)
        {
            var l = new List<ShopItem>();
            switch (cat)
            {
                case 0:
                    foreach (var t in Config.CardTypes)
                        if (t.Catalog <= State.UnlockedCatalog)
                            l.Add(new ShopItem { Kind = ShopItemKind.Card, Id = t.Id, Name = t.Name, Price = Stats.TicketPrice(t), Desc = RuleText(t) + (GateLevelsLeft(t) > 0 ? " Kilitli, " + GateLevelsLeft(t) + " yükseltme daha lazım." : "") });
                    break;
                case 1: AddUpgrades(l, "scratch"); break;
                case 2:
                    AddUpgrades(l, "auto");
                    if (Stats.AutoBuyOwned) l.Add(new ShopItem { Kind = ShopItemKind.Toggle, Id = "autoBuyOn", Name = "Otomatik alım: " + (State.AutoBuyOn ? "açık" : "kapalı"), Desc = "Açıp kapatmak için satın al." });
                    if (Stats.AutoTypeLevel > 0)
                    {
                        var tt = Auto.TargetType();
                        l.Add(new ShopItem { Kind = ShopItemKind.Toggle, Id = "autoType", Name = "Otomatik kart türü: " + (tt != null ? tt.Name : "yok"), Desc = "Değiştirmek için satın al." });
                    }
                    if (Stats.AutoOwned) l.Add(new ShopItem { Kind = ShopItemKind.Toggle, Id = "autoDiscard", Name = "Tuzakta çöpe at: " + (State.AutoDiscardTrap ? "açık" : "kapalı"), Desc = "Açıkken otomatik kazıyıcı tuzak görünce kartı bırakır." });
                    break;
                case 3: AddUpgrades(l, "gadget"); break;
                case 4:
                    if (State.PrestigeOffer)
                        l.Add(new ShopItem { Kind = ShopItemKind.PrestigeReset, Id = "reset", Name = "Sıfırla ve " + PrestigeGain() + " Altın Bilet puanı kazan", Desc = "Para, kartlar ve yükseltmeler sıfırlanır. Kalıcı ağaç kalır." });
                    else
                        l.Add(new ShopItem { Kind = ShopItemKind.Info, Id = "info", Name = "Altın Bilet Son Şansı", Desc = "Beşinci katalogdaki Altın Bilet Son Şansı kartını kazan, sıfırlama teklifi gelsin. Puanın " + State.AB + "." });
                    foreach (var n in Config.Nodes)
                        if (UpgradeSystem.NodeAvailable(Config, State, n))
                            l.Add(new ShopItem { Kind = ShopItemKind.Node, Id = n.Id, Name = n.Name, Desc = n.Desc, Price = n.Cost, UsesAB = true });
                    break;
            }
            return l;
        }

        private void AddUpgrades(List<ShopItem> l, string category)
        {
            foreach (var u in Config.Upgrades)
            {
                if (u.Category != category || !UpgradeSystem.IsAvailable(Config, State, u)) continue;
                l.Add(new ShopItem { Kind = ShopItemKind.Upgrade, Id = u.Id, Name = u.Name, Desc = u.Desc, Price = UpgradeSystem.NextCost(Config, State, Stats, u) });
            }
        }

        public static string RuleText(CardTypeDef t)
        {
            switch (t.Rule)
            {
                case CardRule.ThreeOfAKind: return "Üç aynı sembolü bul.";
                case CardRule.NumberHunt: return "Sayılarını kazanan sayılarla eşleştir.";
                case CardRule.Multiplier: return "Üçlü eşleşme ve çarpan.";
                case CardRule.Trap: return "Tuzak var, yarıda bırakabilirsin.";
                case CardRule.Joker: return "Joker her sembolün yerine geçer.";
                case CardRule.Sequence: return "Bir sıra tamamla.";
                case CardRule.Jackpot: return "Çok nadir büyük ödül.";
                case CardRule.SonSans: return "Son Şans: yeni katalog kapısı.";
                case CardRule.AltinBilet: return "Altın Bilet: kazanırsan sıfırlama teklifi.";
            }
            return "";
        }

        private List<ShopItem> CurrentItems()
        {
            var l = BuildItems(shopCat);
            if (shopIndex >= l.Count) shopIndex = Math.Max(0, l.Count - 1);
            return l;
        }

        private string ReadItem(ShopItem it)
        {
            if (it == null) return "Bu bölümde şimdilik bir şey yok.";
            switch (it.Kind)
            {
                case ShopItemKind.Card:
                    return it.Name + ". " + it.Desc + " Fiyat " + NumberFormat.Lira(it.Price) + "." + Afford(it);
                case ShopItemKind.Upgrade:
                    {
                        var u = Config.UpgradeById[it.Id];
                        return it.Name + ", seviye " + State.Level(u.Id) + " dan " + (State.Level(u.Id) + 1) + " e. " + it.Desc + " Fiyat " + NumberFormat.Lira(it.Price) + "." + Afford(it);
                    }
                case ShopItemKind.Node:
                    return it.Name + ". " + it.Desc + " " + it.Price + " Altın Bilet puanı." + (State.AB >= it.Price ? " Puanın yetiyor." : " " + (it.Price - State.AB) + " puan eksik.");
                default:
                    return it.Name + ". " + it.Desc;
            }
        }

        private string Afford(ShopItem it)
        {
            return State.Money >= it.Price ? " Paran yetiyor." : " " + NumberFormat.Lira(it.Price - State.Money) + " eksik.";
        }

        public string ReadShopItem()
        {
            var l = CurrentItems();
            return ReadItem(l.Count == 0 ? null : l[shopIndex]);
        }

        public void ToggleShop()
        {
            ShopOpen = !ShopOpen;
            confirmPrestige = false;
            if (ShopOpen) { shopIndex = 0; Emit(EventKind.ShopOpened, "Dükkân. " + CategoryName + ". " + ReadShopItem(), SpeechLevel.Menu); }
            else Emit(EventKind.ShopClosed, "Dükkân kapandı.", SpeechLevel.Menu);
        }

        public void ShopNext() { MoveItem(1); }
        public void ShopPrev() { MoveItem(-1); }

        private void MoveItem(int d)
        {
            var l = CurrentItems();
            confirmPrestige = false;
            if (l.Count > 0) shopIndex = (shopIndex + d + l.Count) % l.Count;
            Emit(EventKind.ShopItemChanged, ReadShopItem(), SpeechLevel.Menu);
        }

        public void ShopNextCategory() { MoveCategory(1); }
        public void ShopPrevCategory() { MoveCategory(-1); }

        private void MoveCategory(int d)
        {
            shopCat = (shopCat + d + Categories.Length) % Categories.Length;
            shopIndex = 0; confirmPrestige = false;
            Emit(EventKind.ShopCategoryChanged, CategoryName + ". " + ReadShopItem(), SpeechLevel.Menu);
        }

        /// <summary>Seçili öğeyi satın alır / uygular.</summary>
        public bool ShopBuy()
        {
            var l = CurrentItems();
            if (l.Count == 0) { Emit(EventKind.Error, "Burada alınacak bir şey yok.", SpeechLevel.Menu); return false; }
            var it = l[shopIndex];
            bool ok = false;
            switch (it.Kind)
            {
                case ShopItemKind.Card: ok = BuyCard(it.Id); break;
                case ShopItemKind.Upgrade: ok = BuyUpgrade(it.Id); break;
                case ShopItemKind.Node: ok = BuyNode(it.Id); break;
                case ShopItemKind.Toggle: ok = DoToggle(it.Id); break;
                case ShopItemKind.PrestigeReset:
                    if (!confirmPrestige)
                    {
                        confirmPrestige = true;
                        Emit(EventKind.Info, "Emin misin? Her şey sıfırlanır. Onaylamak için bir kez daha satın al.", SpeechLevel.Menu);
                    }
                    else { confirmPrestige = false; ok = AcceptPrestige(); }
                    break;
                default: Emit(EventKind.Info, ReadItem(it), SpeechLevel.Menu); break;
            }
            if (ok && it.Kind != ShopItemKind.Card && it.Kind != ShopItemKind.PrestigeReset)
            {
                var nl = CurrentItems();
                if (nl.Count > 0) Emit(EventKind.ShopItemChanged, ReadShopItem(), SpeechLevel.Menu);
            }
            return ok;
        }

        private bool DoToggle(string id)
        {
            switch (id)
            {
                case "autoBuyOn": State.AutoBuyOn = !State.AutoBuyOn; Emit(EventKind.Info, "Otomatik alım " + (State.AutoBuyOn ? "açıldı." : "kapandı."), SpeechLevel.Menu); return true;
                case "autoDiscard": State.AutoDiscardTrap = !State.AutoDiscardTrap; Emit(EventKind.Info, "Tuzakta çöpe atma " + (State.AutoDiscardTrap ? "açıldı." : "kapandı."), SpeechLevel.Menu); return true;
                case "autoType": CycleAutoType(); return true;
            }
            return false;
        }

        /// <summary>Otomatik alım türünü sıradakine geçirir (açık katalog ve tür seçici seviyesiyle sınırlı).</summary>
        public void CycleAutoType()
        {
            int maxCat = Math.Min(State.UnlockedCatalog, Stats.AutoTypeLevel);
            var opts = new List<CardTypeDef>();
            foreach (var t in Config.CardTypes) if (t.Catalog <= maxCat) opts.Add(t);
            if (opts.Count == 0) return;
            var cur = Auto.TargetType();
            int i = cur == null ? -1 : opts.IndexOf(cur);
            var nt = opts[(i + 1) % opts.Count];
            State.AutoType = nt.Id;
            Emit(EventKind.Info, "Otomatik alım: " + nt.Name + ".", SpeechLevel.Menu, 0, nt.Id);
        }

        public bool SetAutoType(string typeId)
        {
            CardTypeDef t;
            if (!Config.TypeById.TryGetValue(typeId ?? "", out t)) return false;
            if (t.Catalog > Math.Min(State.UnlockedCatalog, Stats.AutoTypeLevel)) return false;
            State.AutoType = t.Id;
            return true;
        }

        // ---- yükseltme ----
        public bool BuyUpgrade(string id)
        {
            UpgradeDef u;
            if (!Config.UpgradeById.TryGetValue(id ?? "", out u)) { Emit(EventKind.Error, "Böyle bir yükseltme yok.", SpeechLevel.Info); return false; }
            if (!UpgradeSystem.IsAvailable(Config, State, u)) { Emit(EventKind.Error, "Bu yükseltme şimdilik alınamaz.", SpeechLevel.Info); return false; }
            long cost = UpgradeSystem.NextCost(Config, State, Stats, u);
            if (State.Money < cost)
            {
                Emit(EventKind.NotEnoughMoney, "Para yetmiyor, " + NumberFormat.Lira(cost - State.Money) + " eksik.", SpeechLevel.Info, cost - State.Money, u.Id);
                return false;
            }
            State.Wallet.TrySpend(cost);
            State.Upgrades[u.Id] = State.Level(u.Id) + 1;
            RecalcStats();
            EventKind k = u.Category == "gadget" ? EventKind.GadgetBought
                : (u.Kind == EffectKind.AutoUnlock || u.Kind == EffectKind.AutoBuy ? EventKind.AutomationBought : EventKind.UpgradeBought);
            Emit(k, u.Name + " alındı, seviye " + State.Level(u.Id) + ".", SpeechLevel.Info, cost, u.Id);
            return true;
        }

        // ---- prestij ----
        public bool PrestigeOffered { get { return State.PrestigeOffer; } }
        public long PrestigeGain() { return Prestige.ComputeGain(Config, State, Stats); }

        public bool AcceptPrestige()
        {
            if (!State.PrestigeOffer) { Emit(EventKind.Error, "Sıfırlama teklifi yok.", SpeechLevel.Info); return false; }
            long gain = Prestige.Reset(Config, State, Stats);
            RecalcStats();
            notified.Clear(); notifyInit = false;
            Emit(EventKind.PrestigeDone, "Yeni koşu başladı. " + gain + " Altın Bilet puanı kazandın, toplam " + State.AB + ".", SpeechLevel.Info, gain);
            return true;
        }

        public void DeclinePrestige()
        {
            if (!State.PrestigeOffer) return;
            State.PrestigeOffer = false;
            Emit(EventKind.Info, "Teklif reddedildi. Oynamaya devam.", SpeechLevel.Info);
        }

        public bool BuyNode(string id)
        {
            PrestigeNodeDef n;
            if (!Config.NodeById.TryGetValue(id ?? "", out n)) { Emit(EventKind.Error, "Böyle bir düğüm yok.", SpeechLevel.Info); return false; }
            if (!UpgradeSystem.NodeAvailable(Config, State, n)) { Emit(EventKind.Error, "Bu düğüm şimdilik alınamaz.", SpeechLevel.Info); return false; }
            if (State.AB < n.Cost)
            {
                Emit(EventKind.NotEnoughMoney, "Altın Bilet puanı yetmiyor, " + (n.Cost - State.AB) + " eksik.", SpeechLevel.Info, n.Cost - State.AB, n.Id);
                return false;
            }
            State.AB -= n.Cost;
            State.Nodes[n.Id] = 1;
            Prestige.GrantStartUnlocks(Config, State);
            RecalcStats();
            // kalıcı ağacın anında etkisi: koşu başı değerler bir sonraki sıfırlamada uygulanır; ancak otomasyon ve katalog hemen geçerli olsun
            if (n.Kind == EffectKind.StartCatalog && State.UnlockedCatalog < Stats.StartCatalog) State.UnlockedCatalog = Stats.StartCatalog;
            Emit(EventKind.NodeBought, n.Name + " alındı.", SpeechLevel.Info, n.Cost, n.Id);
            return true;
        }

        // ---- çevrimdışı ----
        /// <summary>Yalnızca otomasyon çalışır; en fazla Stats.OfflineCap sn sayılır.</summary>
        public OfflineResult ApplyOffline(double seconds)
        {
            var res = new OfflineResult();
            if (seconds <= 0 || double.IsNaN(seconds) || !Stats.AutoOwned) return res;
            double secs = Math.Min(seconds, Stats.OfflineCap);
            res.SecondsCounted = secs;
            long before = State.Money;
            int cardsBefore = (int)Math.Min(int.MaxValue, State.TotalCards);
            suppress = true;
            try
            {
                double left = secs;
                while (left > 0)
                {
                    double step = Math.Min(left, 60);
                    Auto.Advance(step);
                    left -= step;
                }
            }
            finally { suppress = false; }
            autoCards = 0; autoWins = 0; autoNet = 0;
            res.Money = State.Money - before;
            res.Cards = (int)Math.Min(int.MaxValue, State.TotalCards) - cardsBefore;
            if (res.Cards > 0)
            {
                res.Speech = "Sen yokken " + res.Cards + " kart kazındı, " + (res.Money >= 0 ? NumberFormat.Lira(res.Money) + " kazandın." : NumberFormat.Lira(-res.Money) + " kaybettin.");
                Emit(EventKind.OfflineReport, res.Speech, SpeechLevel.Info, res.Money);
            }
            return res;
        }

        public OfflineResult ApplyOfflineSince(long nowUnix)
        {
            if (State.LastSaveUnix <= 0) return new OfflineResult();
            return ApplyOffline(nowUnix - State.LastSaveUnix);
        }
    }
}
