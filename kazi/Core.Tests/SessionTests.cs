using System;
using System.Collections.Generic;
using System.Linq;
using Kazi.Core;
using Xunit;

namespace Kazi.Core.Tests
{
    public class SessionTests
    {
        private static GameSession Hand(string typeId, long money, params string[] cells)
        {
            var s = T.NewSession(money);
            s.State.UnlockedCatalog = T.Type(typeId).Catalog;
            var c = T.Make(typeId, cells);
            c.Paid = T.Type(typeId).Price;
            s.State.Current = c;
            return s;
        }

        // ---- satın alma ----
        [Fact]
        public void BuyCardSpendsMoneyAndSetsCurrent()
        {
            var s = T.NewSession(100);
            var ev = T.Capture(s);
            Assert.True(s.BuyCard("simitci"));
            Assert.Equal(90, s.State.Money);
            Assert.NotNull(s.State.Current);
            Assert.Equal("simitci", s.State.Current.Type.Id);
            Assert.Contains(ev, e => e.Kind == EventKind.CardBought && e.Speech.Contains("Simitçi"));
        }

        [Fact]
        public void BuyCardWithoutMoneyDoesNothingAndExplains()
        {
            var s = T.NewSession(4);
            var ev = T.Capture(s);
            Assert.False(s.BuyCard("simitci"));
            Assert.Equal(4, s.State.Money);
            Assert.Null(s.State.Current);
            var e = ev.Single(x => x.Kind == EventKind.NotEnoughMoney);
            Assert.Contains("6 lira eksik", e.Speech);
        }

        [Fact]
        public void LockedCatalogAndUnknownTypeRejected()
        {
            var s = T.NewSession(1000000);
            Assert.False(s.BuyCard("lale_tezgahi"));
            Assert.False(s.BuyCard("yok_boyle_kart"));
            Assert.False(s.BuyCard(null));
            Assert.Equal(1000000, s.State.Money);
        }

        [Fact]
        public void GateCardNeedsEnoughUpgradeLevels()
        {
            var s = T.NewSession(1000000000);
            var ev = T.Capture(s);
            Assert.False(s.BuyCard("mahalle_sonsans"));
            Assert.Contains(ev, e => e.Kind == EventKind.Error && e.Speech.Contains("Son Şans kilitli"));
            foreach (var u in T.Cfg.Upgrades) if (u.MinCatalog == 1 && string.IsNullOrEmpty(u.Requires)) s.State.Upgrades[u.Id] = u.MaxLevel;
            Assert.True(s.State.TotalLevels >= T.Cfg.Catalogs[0].GateLevels);
            Assert.True(s.BuyCard("mahalle_sonsans"));
        }

        [Fact]
        public void GateWinUnlocksNextCatalog()
        {
            var s = Hand("mahalle_sonsans", 100, "anahtar", "anahtar", "anahtar", "kilit", "cira", "defter");
            var ev = T.Capture(s);
            for (int i = 0; i < 6; i++) s.ScratchTap();
            Assert.Equal(2, s.State.UnlockedCatalog);
            Assert.Contains(ev, e => e.Kind == EventKind.CatalogUnlocked && e.Speech.Contains("Çarşı Pazarı"));
            Assert.Equal(0, s.State.PityOf("mahalle_sonsans"));
        }

        [Fact]
        public void GateMissBuildsPityAndPityForcesWin()
        {
            var s = T.NewSession(1000000000);
            var t = T.Type("mahalle_sonsans");
            s.State.Pity[t.Id] = t.PityAfter;
            var c = s.CreateCard(t);
            Assert.True(c.Preview().GateHit);
            s.State.Pity[t.Id] = 0;
            var miss = Hand("mahalle_sonsans", 100, "anahtar", "anahtar", "kilit", "kilit", "cira", "cira");
            for (int i = 0; i < 6; i++) miss.ScratchTap();
            Assert.Equal(1, miss.State.PityOf("mahalle_sonsans"));
            Assert.Equal(1, miss.State.UnlockedCatalog);
        }

        [Fact]
        public void BagFillsUpToCapacityThenReportsFull()
        {
            var s = T.NewSession(100000);
            var ev = T.Capture(s);
            Assert.True(s.BuyCard("simitci"));          // el
            Assert.True(s.BuyCard("simitci"));          // çanta 1/1
            Assert.False(s.BuyCard("simitci"));         // dolu
            Assert.Contains(ev, e => e.Kind == EventKind.BagFull);
            Assert.Equal(1, s.State.Bag.Count);
        }

        [Fact]
        public void NextCardUsesBagThenRebuysLastType()
        {
            var s = T.NewSession(100000);
            s.BuyCard("simitci"); s.BuyCard("simitci");
            Assert.False(s.NextCard()); // önce kazı
            for (int i = 0; i < 10; i++) s.ScratchTap();
            Assert.True(s.State.Current.Done);
            Assert.True(s.NextCard());
            Assert.Equal(0, s.State.Bag.Count);
            for (int i = 0; i < 10; i++) s.ScratchTap();
            long before = s.State.Money;
            Assert.True(s.NextCard());       // çanta boş: son türden yeni kart
            Assert.Equal(before - 10, s.State.Money);
            Assert.Equal("simitci", s.State.Current.Type.Id);
        }

        // ---- kazıma ve ödeme ----
        [Fact]
        public void SequentialTapsFinishCardAndPay()
        {
            var s = Hand("simitci", 100, "simit", "cay", "simit", "guvercin", "simit", "cay");
            var ev = T.Capture(s);
            for (int i = 0; i < 6; i++) s.ScratchTap();
            Assert.True(s.State.Current.Resolved);
            long pay = (long)Math.Round(10 * T.Type("simitci").Symbols[0].Pay);
            Assert.Equal(100 + pay, s.State.Money);
            Assert.Equal(6, ev.Count(e => e.Kind == EventKind.CellOpened));
            var res = ev.Single(e => e.Kind == EventKind.CardResult);
            Assert.Equal("Kazandın, " + pay + " lira.", res.Speech);
            Assert.Equal(Rarity.Small, res.Rarity);
        }

        [Fact]
        public void LosingCardSaysNicely()
        {
            var s = Hand("simitci", 100, "simit", "simit", "cay", "cay", "guvercin", "guvercin");
            var ev = T.Capture(s);
            for (int i = 0; i < 6; i++) s.ScratchTap();
            Assert.Equal(100, s.State.Money);
            Assert.Equal("Bu sefer çıkmadı.", ev.Single(e => e.Kind == EventKind.CardResult).Speech);
        }

        [Fact]
        public void ScratchPowerUpgradeOpensMoreCellsPerTap()
        {
            var s = Hand("simitci", 1000, "simit", "cay", "simit", "guvercin", "simit", "cay");
            s.State.Upgrades["guc"] = 2; s.RecalcStats();
            Assert.Equal(3, s.Stats.ScratchPower);
            s.ScratchTap();
            Assert.Equal(3, s.State.Current.OpenCount);
            s.ScratchTap();
            Assert.True(s.State.Current.Resolved);
        }

        [Fact]
        public void PayoutMultiplierAndCardHandling()
        {
            var s = Hand("simitci", 100, "simit", "simit", "simit", "cay", "cay", "guvercin");
            s.State.Upgrades["carp1"] = T.Cfg.UpgradeById["carp1"].MaxLevel; s.RecalcStats();
            double mult = s.Stats.PayoutMult;
            Assert.True(mult > 1.05);
            for (int i = 0; i < 6; i++) s.ScratchTap();
            Assert.Equal(100 + (long)Math.Round(10 * 1.5 * mult, MidpointRounding.AwayFromZero), s.State.Money);
        }

        [Fact]
        public void TicketDiscountLowersPriceNotPayout()
        {
            var s = T.NewSession(1000);
            s.State.Upgrades["indirim1"] = T.Cfg.UpgradeById["indirim1"].MaxLevel; s.RecalcStats();
            Assert.True(s.Stats.Discount > 0.1);
            Assert.True(s.TicketPrice(T.Type("simitci")) < 10);
            Assert.True(s.TicketPrice(T.Type("simitci")) >= 1);
        }

        [Fact]
        public void DragModeOpensCellsAndFinishes()
        {
            var s = Hand("simitci", 100, "simit", "simit", "simit", "cay", "cay", "guvercin");
            s.Mode = ScratchMode.Drag;
            var ev = T.Capture(s);
            s.ScratchDrag(0, 0.3);
            Assert.False(s.State.Current.Open[0]);
            Assert.Single(ev.Where(e => e.Kind == EventKind.CellPeek));
            s.ScratchDrag(0, 0.3);
            Assert.True(s.State.Current.Open[0]);
            for (int i = 1; i < 6; i++) s.ScratchDrag(i, 1.0);
            Assert.True(s.State.Current.Resolved);
            Assert.Equal(1, ev.Count(e => e.Kind == EventKind.CardResult));
        }

        [Fact]
        public void DragPeekWarnsAboutTrapCell()
        {
            var s = Hand("fener_ustasi", 5000, "akrep", "kapi", "fener", "sandik", "ay", "yildiz", "tac", "kapi", "fener");
            var ev = T.Capture(s);
            s.ScratchDrag(0, 0.3);
            var peek = ev.Single(e => e.Kind == EventKind.CellPeek);
            Assert.Contains("tuzak", peek.Speech.ToLowerInvariant());
            s.ScratchDrag(1, 0.3);
            Assert.Equal("", ev.Last(e => e.Kind == EventKind.CellPeek).Speech);
        }

        // ---- tuzak ----
        [Fact]
        public void TrapPenaltyReducesWinnings()
        {
            var s = Hand("fener_ustasi", 5000, "kapi", "kapi", "kapi", "fener", "fener", "ay", "akrep", "yildiz", "sandik");
            s.State.Current.Paid = 1000;
            s.State.Money = 5000;
            for (int i = 0; i < 9; i++) s.ScratchTap();
            long gross = (long)Math.Round(1000 * T.Type("fener_ustasi").Symbols[0].Pay * 1);
            Assert.Equal(5000 + gross - 1000, s.State.Money);
        }

        [Fact]
        public void TrapPenaltyNeverMakesBalanceNegative()
        {
            var s = Hand("fener_ustasi", 5, "kapi", "fener", "sandik", "ay", "yildiz", "tac", "akrep", "kara_bulut", "kapi");
            var ev = T.Capture(s);
            for (int i = 0; i < 9; i++) s.ScratchTap();
            Assert.Equal(0, s.State.Money);
            Assert.Contains(ev, e => e.Kind == EventKind.TrapHit);
            Assert.StartsWith("Tuzağa düştün", ev.Single(e => e.Kind == EventKind.CardResult).Speech);
        }

        [Fact]
        public void PartialScratchThenDiscardRefundsAndForfeitsEverything()
        {
            var s = Hand("fener_ustasi", 5000, "kapi", "kapi", "kapi", "fener", "fener", "ay", "akrep", "yildiz", "sandik");
            s.State.Current.Paid = 1000;
            s.State.Money = 5000;
            s.ScratchTap(); s.ScratchTap(); s.ScratchTap();
            Assert.False(s.State.Current.Resolved);
            var ev = T.Capture(s);
            Assert.True(s.DiscardCard());
            long refund = (long)Math.Floor(1000 * T.Cfg.Settings.DiscardRefundPct);
            Assert.Equal(5000 + refund, s.State.Money);
            Assert.True(s.State.Current.Discarded);
            Assert.False(s.State.Current.Resolved);
            Assert.Contains(ev, e => e.Kind == EventKind.CardDiscarded && e.Amount == refund);
            Assert.False(ev.Any(e => e.Kind == EventKind.CardResult));
        }

        [Fact]
        public void CannotDiscardCardWithoutTrapsOrFinishedCard()
        {
            var s = Hand("simitci", 100, "simit", "cay", "simit", "guvercin", "simit", "cay");
            Assert.False(s.DiscardCard());
            Assert.False(s.State.Current.Done);
            var t = Hand("fener_ustasi", 5000, "kapi", "kapi", "kapi", "fener", "fener", "ay", "akrep", "yildiz", "sandik");
            for (int i = 0; i < 9; i++) t.ScratchTap();
            Assert.False(t.DiscardCard());
        }

        [Fact]
        public void RefundUpgradeRaisesRefund()
        {
            var s = T.NewSession(100);
            double before = s.Stats.Refund;
            s.State.Upgrades["iade"] = 3; s.RecalcStats();
            Assert.True(s.Stats.Refund > before);
            Assert.True(s.Stats.Refund <= T.Cfg.Settings.MaxRefund);
        }

        // ---- bakiye ve yardım parası ----
        [Fact]
        public void BalanceNeverNegativeUnderRandomPlay()
        {
            var s = T.NewSession(50, 7);
            s.State.UnlockedCatalog = 2;
            var rnd = new Random(3);
            var ids = T.Cfg.CardTypes.Where(t => t.Catalog <= 2 && !t.IsGate).Select(t => t.Id).ToList();
            for (int i = 0; i < 3000; i++)
            {
                switch (rnd.Next(6))
                {
                    case 0: s.BuyCard(ids[rnd.Next(ids.Count)]); break;
                    case 1: case 2: s.ScratchTap(); break;
                    case 3: s.DiscardCard(); break;
                    case 4: s.NextCard(); break;
                    case 5: s.Tick(rnd.NextDouble() * 5); break;
                }
                Assert.True(s.State.Money >= 0);
            }
        }

        [Fact]
        public void HelpMoneyKeepsBrokePlayerPlaying()
        {
            var s = T.NewSession(0);
            var ev = T.Capture(s);
            s.Tick(0.1);
            Assert.Contains(ev, e => e.Kind == EventKind.HelpMoney && e.Speech.StartsWith("Yardım parası"));
            Assert.True(s.State.Money >= s.TicketPrice(T.Type("simitci")));
            // iflas döngüsü: her seferinde tüm parayı harca; oyuncu asla tıkanmaz
            int bought = 0;
            for (int i = 0; i < 300; i++)
            {
                s.State.Money = Math.Min(s.State.Money, 9);
                s.State.Current = null; s.State.Bag.Clear();
                s.Tick(0.1);
                if (s.BuyCard("simitci")) bought++;
            }
            Assert.Equal(300, bought);
        }

        [Fact]
        public void NoHelpMoneyWhileHoldingCardOrWhenRich()
        {
            var s = T.NewSession(5);
            s.State.Current = T.Make("simitci", "simit", "cay", "simit", "guvercin", "simit", "cay");
            var ev = T.Capture(s);
            s.Tick(1);
            Assert.DoesNotContain(ev, e => e.Kind == EventKind.HelpMoney);
            var rich = T.NewSession(500);
            var ev2 = T.Capture(rich);
            rich.Tick(1);
            Assert.DoesNotContain(ev2, e => e.Kind == EventKind.HelpMoney);
        }

        // ---- yükseltmeler ----
        [Fact]
        public void UpgradeCostIsBaseTimesMultPowLevel()
        {
            var u = T.Cfg.UpgradeById["sans2"];
            Assert.Equal((long)Math.Ceiling(u.BaseCost), UpgradeSystem.Cost(u, 0, 1.0));
            Assert.Equal((long)Math.Ceiling(u.BaseCost * Math.Pow(u.CostMult, 5)), UpgradeSystem.Cost(u, 5, 1.0));
            Assert.True(UpgradeSystem.Cost(u, 6, 1.0) > UpgradeSystem.Cost(u, 5, 1.0));
            Assert.Equal(1, UpgradeSystem.Cost(new UpgradeDef { BaseCost = 0.01, CostMult = 1 }, 0, 1.0));
        }

        [Fact]
        public void BuyUpgradeSpendsMoneyRaisesLevelAndUpdatesStats()
        {
            var s = T.NewSession(1000000);
            var ev = T.Capture(s);
            long cost = UpgradeSystem.NextCost(T.Cfg, s.State, s.Stats, T.Cfg.UpgradeById["guc"]);
            Assert.True(s.BuyUpgrade("guc"));
            Assert.Equal(1000000 - cost, s.State.Money);
            Assert.Equal(1, s.State.Level("guc"));
            Assert.Equal(2, s.Stats.ScratchPower);
            Assert.Contains(ev, e => e.Kind == EventKind.UpgradeBought && e.Speech.Contains("Kazıma Gücü"));
        }

        [Fact]
        public void UpgradeRulesRequirementsCatalogMaxAndMoney()
        {
            var s = T.NewSession(10000000);
            Assert.False(s.BuyUpgrade("oto_hiz"));        // Otomatik Kazıyıcı gerekir
            Assert.False(s.BuyUpgrade("sans2"));          // katalog 2 gerekir
            Assert.True(s.BuyUpgrade("oto_kaziyici"));
            Assert.True(s.BuyUpgrade("oto_hiz"));
            Assert.False(s.BuyUpgrade("yok"));
            s.State.Money = 0;
            var ev = T.Capture(s);
            Assert.False(s.BuyUpgrade("guc"));
            Assert.Contains(ev, e => e.Kind == EventKind.NotEnoughMoney);
            s.State.Money = long.MaxValue / 4;
            int max = T.Cfg.UpgradeById["oto_kaziyici"].MaxLevel;
            Assert.False(s.BuyUpgrade("oto_kaziyici"));   // tek seviyeli, zaten alındı
            Assert.Equal(max, s.State.Level("oto_kaziyici"));
        }

        [Fact]
        public void AffordableNotificationFiresOnceAfterCrossingThreshold()
        {
            var s = T.NewSession(0);
            s.State.Current = T.Make("simitci", "simit", "cay", "simit", "guvercin", "simit", "cay"); // yardım parası karışmasın
            var ev = T.Capture(s);
            s.Tick(1);
            Assert.DoesNotContain(ev, e => e.Kind == EventKind.UpgradeAffordable);
            s.State.Money = 100;
            s.Tick(1);
            Assert.Equal(1, ev.Count(e => e.Kind == EventKind.UpgradeAffordable));
            s.Tick(1); s.Tick(1);
            Assert.Equal(1, ev.Count(e => e.Kind == EventKind.UpgradeAffordable));
        }

        [Fact]
        public void AffordableNotificationCanBeDisabled_AndIsQuietAtStartup()
        {
            var s = T.NewSession(500); // açılışta zaten alınabilir şeyler var
            var ev = T.Capture(s);
            s.Tick(1);
            Assert.DoesNotContain(ev, e => e.Kind == EventKind.UpgradeAffordable);
            var q = T.NewSession(0);
            q.State.Current = T.Make("simitci", "simit", "cay", "simit", "guvercin", "simit", "cay");
            q.State.NotifyAffordable = false;
            var ev2 = T.Capture(q);
            q.Tick(1); q.State.Money = 500; q.Tick(1);
            Assert.DoesNotContain(ev2, e => e.Kind == EventKind.UpgradeAffordable);
        }

        // ---- dükkân ----
        [Fact]
        public void ShopNavigationReadsItemsAndBuys()
        {
            var s = T.NewSession(1000);
            var ev = T.Capture(s);
            s.ToggleShop();
            Assert.True(s.ShopOpen);
            Assert.Contains("Kartlar", ev.Last().Speech);
            Assert.Contains("Simitçi", s.ReadShopItem());
            s.ShopNextCategory();
            Assert.Equal("Kazıma", s.CategoryName);
            Assert.Contains("Fiyat", s.ReadShopItem());
            s.ShopNext(); s.ShopPrev();
            string first = s.ReadShopItem();
            Assert.True(s.ShopBuy());
            Assert.True(s.State.TotalLevels == 1);
            s.ShopPrevCategory();
            Assert.Equal("Kartlar", s.CategoryName);
            s.ToggleShop();
            Assert.False(s.ShopOpen);
        }

        [Fact]
        public void ShopTellsMissingMoney()
        {
            var s = T.NewSession(1);
            s.ShopNextCategory();
            Assert.Contains("eksik", s.ReadShopItem());
        }

        [Fact]
        public void ShopCategoriesAreStable()
        {
            Assert.Equal(new[] { "Kartlar", "Kazıma", "Otomasyon", "Gadget", "Prestij" }, GameSession.Categories);
            var s = T.NewSession(10);
            for (int c = 0; c < 5; c++) { s.ShopNextCategory(); Assert.False(string.IsNullOrEmpty(s.ReadShopItem())); }
        }

        [Fact]
        public void ReadStatusMentionsBalanceAndCatalog()
        {
            var s = T.NewSession(1234);
            string st = s.ReadStatus();
            Assert.Contains("Bakiye 1,2 bin lira", st);
            Assert.Contains("Mahalle Bayii", st);
        }

        [Fact]
        public void LensGivesTrapHintOnlyWithLens()
        {
            var s = Hand("fener_ustasi", 100, "akrep", "kapi", "fener", "sandik", "ay", "yildiz", "tac", "kapi", "fener");
            Assert.Equal("", s.LensHint());
            s.State.Upgrades["oto_kaziyici"] = 1; s.State.Upgrades["mercek"] = 4; s.State.UnlockedCatalog = 2; s.RecalcStats();
            Assert.StartsWith("Mercek", s.LensHint());
        }
    }
}
