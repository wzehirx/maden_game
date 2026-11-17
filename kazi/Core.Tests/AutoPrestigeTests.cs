using System;
using System.Collections.Generic;
using System.Linq;
using Kazi.Core;
using Xunit;

namespace Kazi.Core.Tests
{
    public class AutomationTests
    {
        private static GameSession WithAuto(long money = 100000, bool autoBuy = false)
        {
            var s = T.NewSession(money, 99);
            s.State.Upgrades["oto_kaziyici"] = 1;
            if (autoBuy) s.State.Upgrades["oto_alim"] = 1;
            s.RecalcStats();
            return s;
        }

        [Fact]
        public void NothingRunsWithoutAutoScratcher()
        {
            var s = T.NewSession(1000);
            s.State.Bag.Add(s.CreateCard(T.Type("simitci")));
            long cards = s.State.TotalCards;
            s.Tick(600);
            Assert.Equal(cards, s.State.TotalCards);
            Assert.Equal(1, s.State.Bag.Count);
        }

        [Fact]
        public void AutoScratcherProcessesQueueAtConfiguredRate()
        {
            var s = WithAuto();
            Assert.Equal(T.Cfg.Settings.AutoBaseCpm, s.Stats.AutoCpm, 6);
            for (int i = 0; i < 4; i++) s.State.Bag.Add(s.CreateCard(T.Type("simitci")));
            s.Tick(60 * 2.0 / s.Stats.AutoCpm); // iki kart süresi
            Assert.Equal(2, s.State.TotalCards);
            Assert.Equal(2, s.State.Bag.Count);
            s.Tick(600);
            Assert.Empty(s.State.Bag);
            Assert.Equal(4, s.State.TotalCards);
        }

        [Fact]
        public void AutoScratcherPaysIntoBalanceAutomatically()
        {
            var s = WithAuto(0);
            s.State.Money = 0;
            var c = T.Make("simitci", "simit", "simit", "simit", "cay", "cay", "guvercin"); c.Paid = 10;
            s.State.Bag.Add(c);
            s.State.Current = T.Make("simitci", "simit", "cay", "simit", "guvercin", "simit", "cay"); // yardım parası karışmasın
            s.Tick(60);
            Assert.Equal((long)Math.Round(10 * T.Type("simitci").Symbols[0].Pay * s.Stats.PayoutMult), s.State.Money);
        }

        [Fact]
        public void AutoBuyKeepsQueueFilledWhileMoneyLasts()
        {
            var s = WithAuto(1000000, true);
            long m0 = s.State.Money;
            s.Tick(60);
            int expected = (int)Math.Floor(s.Stats.AutoCpm * s.Stats.Batch);
            Assert.InRange(s.State.TotalCards, expected - 1, expected + 1);
            Assert.True(s.State.Bag.Count <= s.Stats.BagCap);
            Assert.NotEqual(m0, s.State.Money);
        }

        [Fact]
        public void AutoBuyStopsWhenBroke_ThenHelpMoneyRestarts()
        {
            var s = WithAuto(0, true);
            s.State.Money = 3;
            s.Tick(1);
            Assert.True(s.State.Money >= 10 || s.State.TotalCards > 0);
            for (int i = 0; i < 200; i++) { s.Tick(10); Assert.True(s.State.Money >= 0); }
            Assert.True(s.State.TotalCards > 20);
        }

        [Fact]
        public void AutoBuyFallsBackToCheapestWhenSelectedTypeTooExpensive()
        {
            var s = WithAuto(500, true);
            s.State.UnlockedCatalog = 2; s.State.Upgrades["oto_tur"] = 2; s.RecalcStats();
            Assert.True(s.SetAutoType("fener_ustasi"));
            s.State.Money = 200; // 1000 liralık kart alınamaz
            s.Tick(30);
            Assert.True(s.State.TotalCards > 0);
        }

        [Fact]
        public void AutoTypeSelectionRequiresSelectorAndUnlockedCatalog()
        {
            var s = WithAuto(1000, true);
            Assert.False(s.SetAutoType("simitci")); // seçici yok
            s.State.Upgrades["oto_tur"] = 1; s.State.UnlockedCatalog = 2; s.RecalcStats();
            Assert.True(s.SetAutoType("simitci"));
            Assert.False(s.SetAutoType("kervan_jokeri")); // katalog 3 kapalı
            Assert.False(s.SetAutoType("yok"));
        }

        [Fact]
        public void GadgetEffectsOnStats()
        {
            var s = WithAuto();
            double baseCpm = s.Stats.AutoCpm;
            var C = T.Cfg;
            s.State.UnlockedCatalog = 5;
            s.State.Upgrades["sayac"] = 3; s.RecalcStats();                         // Mekanik Sayaç: hız çarpanı
            Assert.Equal(baseCpm * (1 + 3 * C.UpgradeById["sayac"].Effect), s.Stats.AutoCpm, 6);
            double luck0 = s.Stats.Luck;
            s.State.Upgrades["cikrik"] = 4; s.RecalcStats();                        // Şans Çıkrığı
            Assert.Equal(luck0 + 4 * C.UpgradeById["cikrik"].Effect, s.Stats.Luck, 6);
            Assert.Equal(1, s.Stats.Batch);
            s.State.Upgrades["pres"] = 2; s.RecalcStats();                          // Dişli Pres: tur başına birden çok kart
            Assert.Equal(3, s.Stats.Batch);
            double pay0 = s.Stats.PayoutMult;
            s.State.Upgrades["zil"] = 5; s.RecalcStats();                           // Kasa Zili: ödeme bonusu
            Assert.Equal(pay0 + 5 * C.UpgradeById["zil"].Effect, s.Stats.PayoutMult, 6);
            s.State.Upgrades["mercek"] = 2; s.RecalcStats();                        // Mercek
            Assert.Equal(2, s.Stats.Lens);
        }

        [Fact]
        public void PressProcessesMoreCardsPerCycle()
        {
            var a = WithAuto(10000000, true);
            var b = WithAuto(10000000, true);
            b.State.Upgrades["pres"] = 2; b.State.Upgrades["oto_kuyruk"] = 3; b.RecalcStats();
            a.Tick(60); b.Tick(60);
            Assert.True(b.State.TotalCards >= a.State.TotalCards * 2.5, b.State.TotalCards + " vs " + a.State.TotalCards);
        }

        [Fact]
        public void SpeedUpgradesRaiseRate()
        {
            var s = WithAuto();
            double c0 = s.Stats.AutoCpm;
            s.State.Upgrades["oto_hiz"] = 5; s.RecalcStats();
            Assert.True(s.Stats.AutoCpm > c0);
        }

        [Fact]
        public void QueueUpgradeRaisesCapacity()
        {
            var s = WithAuto();
            int c0 = s.Stats.BagCap;
            s.State.Upgrades["oto_kuyruk"] = 3; s.State.Upgrades["canta"] = 2; s.RecalcStats();
            Assert.True(s.Stats.BagCap > c0);
        }

        [Fact]
        public void TrapCardsFullyOpenedByDefault()
        {
            var s = WithAuto(0);
            s.State.Money = 0; s.State.UnlockedCatalog = 2;
            var c = T.Make("fener_ustasi", "akrep", "kara_bulut", "kapi", "fener", "sandik", "ay", "yildiz", "tac", "kapi"); c.Paid = 1000;
            s.State.Bag.Add(c);
            s.State.Current = T.Make("simitci", "simit", "cay", "simit", "guvercin", "simit", "cay");
            s.State.Money = 100000;
            s.Tick(60);
            Assert.True(c.Resolved);
            Assert.False(c.Discarded);
        }

        [Fact]
        public void AutoDiscardTrapDropsPeekedTrapCardsWithRefund()
        {
            var s = WithAuto(0);
            s.State.UnlockedCatalog = 2; s.State.AutoDiscardTrap = true;
            s.State.Current = T.Make("simitci", "simit", "cay", "simit", "guvercin", "simit", "cay");
            var c = T.Make("fener_ustasi", "kapi", "akrep", "fener", "sandik", "ay", "yildiz", "tac", "kapi", "kara_bulut"); c.Paid = 1000;
            s.State.Bag.Add(c);
            s.State.Money = 0;
            s.Tick(60);
            Assert.True(c.Discarded);
            Assert.False(c.Resolved);
            Assert.Equal((long)Math.Floor(1000 * s.Stats.Refund), s.State.Money);
        }

        [Fact]
        public void AutoDiscardLetsHiddenTrapsThroughButTrapFreePeekContinues()
        {
            var s = WithAuto(0);
            s.State.UnlockedCatalog = 2; s.State.AutoDiscardTrap = true;
            s.State.Current = T.Make("simitci", "simit", "cay", "simit", "guvercin", "simit", "cay");
            var c = T.Make("fener_ustasi", "kapi", "fener", "sandik", "ay", "yildiz", "tac", "kapi", "akrep", "fener"); c.Paid = 1000; // tuzak 8. hücrede
            s.State.Bag.Add(c);
            s.Tick(60);
            Assert.True(c.Resolved);
        }

        [Fact]
        public void AutoEventsAreSummarisedNotSpokenPerCard()
        {
            var s = WithAuto(100000000, true);
            s.State.Upgrades["oto_hiz"] = 30; s.RecalcStats();
            var ev = T.Capture(s);
            s.Tick(59.9);
            Assert.Empty(ev.Where(e => e.Kind == EventKind.AutoReport));
            Assert.All(ev.Where(e => e.Kind == EventKind.AutoResult), e => Assert.Equal("", e.Speech));
            Assert.DoesNotContain(ev, e => e.Kind == EventKind.CardResult);
            s.Tick(0.2);
            var rep = ev.Single(e => e.Kind == EventKind.AutoReport);
            Assert.Matches(@"^\d+ kart kazıldı, \d+ kazandı\.$", rep.Speech);
            Assert.True(rep.CellIndex > 10);
            Assert.True(rep.Speech.Length < 40);
        }
    }

    public class PrestigeTests
    {
        private static GameSession Rich()
        {
            var s = T.NewSession(5000000);
            s.State.Upgrades["guc"] = 3; s.State.Upgrades["oto_kaziyici"] = 1; s.State.Upgrades["oto_alim"] = 1;
            s.State.UnlockedCatalog = 5; s.State.RunEarned = 4 * (long)T.Cfg.Settings.AbRefEarned; s.State.AutoType = "simitci";
            s.State.Pity["mahalle_sonsans"] = 2;
            s.State.Current = T.Make("simitci", "simit", "cay", "simit", "guvercin", "simit", "cay");
            s.RecalcStats();
            return s;
        }

        [Fact]
        public void GainFormulaUsesRunEarnings()
        {
            var s = Rich();
            double expected = T.Cfg.Settings.AbBase * Math.Sqrt(4.0) * s.Stats.AbGainMult;
            Assert.Equal((long)Math.Floor(expected), s.PrestigeGain());
            s.State.RunEarned = 0;
            Assert.Equal(0, s.PrestigeGain());
            s.State.RunEarned = 1;
            Assert.Equal(1, s.PrestigeGain());
        }

        [Fact]
        public void WinningGoldenTicketOffersReset_DeclineKeepsRun()
        {
            var s = T.NewSession(100);
            s.State.UnlockedCatalog = 5;
            var c = T.Make("altin_bilet", "anahtar", "anahtar", "anahtar", "kilit", "cira", "defter"); c.Paid = 5;
            s.State.Current = c; s.State.RunEarned = 1000000;
            var ev = T.Capture(s);
            for (int i = 0; i < 6; i++) s.ScratchTap();
            Assert.True(s.PrestigeOffered);
            Assert.Contains(ev, e => e.Kind == EventKind.PrestigeOffer && e.Speech.Contains("Altın Bilet"));
            s.DeclinePrestige();
            Assert.False(s.PrestigeOffered);
            Assert.False(s.AcceptPrestige());
        }

        [Fact]
        public void ResetClearsRunButKeepsPermanentThings()
        {
            var s = Rich();
            s.State.PrestigeOffer = true;
            s.State.Nodes["n_luck1"] = 1;
            long gain = s.PrestigeGain();
            var ev = T.Capture(s);
            Assert.True(s.AcceptPrestige());
            Assert.Equal(T.Cfg.Settings.StartMoney, s.State.Money);
            Assert.Empty(s.State.Upgrades);
            Assert.Equal(1, s.State.UnlockedCatalog);
            Assert.Null(s.State.Current);
            Assert.Empty(s.State.Bag);
            Assert.Equal("", s.State.AutoType);
            Assert.Empty(s.State.Pity);
            Assert.Equal(gain, s.State.AB);
            Assert.Equal(1, s.State.PrestigeCount);
            Assert.Equal(1, s.State.NodeLevel("n_luck1"));
            Assert.Equal(0, s.State.RunEarned);
            Assert.False(s.PrestigeOffered);
            Assert.True(s.State.TotalEarned >= 0);
            Assert.Contains(ev, e => e.Kind == EventKind.PrestigeDone);
            Assert.Equal(5, s.Stats.Luck, 6); // kalıcı şans düğümü
        }

        [Fact]
        public void NodesNeedPointsAndParents()
        {
            var s = T.NewSession(0);
            Assert.False(s.BuyNode("n_start1"));            // puan yok
            s.State.AB = 100;
            Assert.False(s.BuyNode("n_start2"));            // ebeveyn yok
            Assert.True(s.BuyNode("n_start1"));
            Assert.Equal(100 - T.Cfg.NodeById["n_start1"].Cost, s.State.AB);
            Assert.True(s.BuyNode("n_start2"));
            Assert.False(s.BuyNode("n_start1"));            // bir kez
            Assert.False(s.BuyNode("yok"));
        }

        [Fact]
        public void NodeEffectsApply()
        {
            var s = T.NewSession(0);
            s.State.AB = 1000;
            Assert.True(s.BuyNode("n_power1")); Assert.Equal(2, s.Stats.ScratchPower);
            Assert.True(s.BuyNode("n_pay1")); Assert.True(s.Stats.PayoutMult > 1.2);
            Assert.True(s.BuyNode("n_disc1")); Assert.True(s.Stats.Discount > 0.02);
            Assert.True(s.BuyNode("n_off1")); Assert.True(s.Stats.OfflineCap > T.Cfg.Settings.BaseOfflineSeconds);
            Assert.True(s.BuyNode("n_help")); Assert.True(s.Stats.HelpMoney > 0);
        }

        [Fact]
        public void StartAutoNodeGivesAutomationFromTheBeginning()
        {
            var s = Rich();
            s.State.Nodes["n_start1"] = 1; s.State.Nodes["n_auto"] = 1; s.State.Nodes["n_buy"] = 1;
            s.State.PrestigeOffer = true;
            Assert.True(s.AcceptPrestige());
            Assert.True(s.Stats.AutoOwned);
            Assert.True(s.Stats.AutoBuyOwned);
            Assert.Equal(1, s.State.Level("oto_kaziyici"));
            long before = s.State.Money;
            long cost = UpgradeSystem.Cost(T.Cfg.UpgradeById["oto_hiz"], 0, s.Stats.UpgradeCostMult);
            Assert.Equal(T.Cfg.Settings.StartMoney + 5000, before);
            Assert.True(s.BuyUpgrade("oto_hiz")); // gereksinim karşılanmış
            Assert.Equal(before - cost, s.State.Money);
        }

        [Fact]
        public void StartMoneyAndStartCatalogNodes()
        {
            var s = Rich();
            s.State.Nodes["n_start1"] = 1; s.State.Nodes["n_start2"] = 1; s.State.Nodes["n_start3"] = 1; s.State.Nodes["n_cat2"] = 1; s.State.Nodes["n_cat3"] = 1;
            s.State.PrestigeOffer = true;
            s.AcceptPrestige();
            Assert.Equal(T.Cfg.Settings.StartMoney + 5000 + 1000000 + 100000000, s.State.Money);
            Assert.Equal(3, s.State.UnlockedCatalog);
        }

        [Fact]
        public void ShopShowsResetOnlyWhenOffered_WithConfirmation()
        {
            var s = Rich();
            for (int i = 0; i < 4; i++) s.ShopNextCategory();
            Assert.Equal("Prestij", s.CategoryName);
            Assert.DoesNotContain("Sıfırla", s.ReadShopItem());
            s.State.PrestigeOffer = true;
            s.ShopNextCategory(); s.ShopPrevCategory();
            Assert.Contains("Sıfırla", s.ReadShopItem());
            var ev = T.Capture(s);
            s.ShopBuy();                       // ilk: onay iste
            Assert.True(s.PrestigeOffered);
            Assert.Contains(ev, e => e.Speech.Contains("Emin misin"));
            s.ShopBuy();                       // ikinci: uygula
            Assert.False(s.PrestigeOffered);
            Assert.Equal(1, s.State.PrestigeCount);
        }
    }

    public class OfflineTests
    {
        [Fact]
        public void NoAutomationMeansNoOfflineProgress()
        {
            var s = T.NewSession(1000);
            var ev = T.Capture(s);
            var r = s.ApplyOffline(3600);
            Assert.Equal(0, r.SecondsCounted);
            Assert.Equal(1000, s.State.Money);
            Assert.Empty(ev);
            Assert.Equal("", r.Speech);
        }

        [Fact]
        public void CapsAtStatsCapAndNeverMoreThan7200()
        {
            var s = T.NewSession(100000000, 5);
            s.State.Upgrades["oto_kaziyici"] = 1; s.State.Upgrades["oto_alim"] = 1;
            s.RecalcStats();
            double cap = s.Stats.OfflineCap;
            Assert.Equal(T.Cfg.Settings.BaseOfflineSeconds, cap, 6);
            var r = s.ApplyOffline(1000000);
            Assert.Equal(cap, r.SecondsCounted, 6);
            s.State.Upgrades["cevrimdisi"] = T.Cfg.UpgradeById["cevrimdisi"].MaxLevel;
            foreach (var n in T.Cfg.Nodes) s.State.Nodes[n.Id] = 1;
            s.RecalcStats();
            Assert.Equal(7200, s.Stats.OfflineCap, 6);
            var r2 = s.ApplyOffline(99999999);
            Assert.Equal(7200, r2.SecondsCounted, 6);
        }

        [Fact]
        public void OfflineRunsOnlyAutomationAndReportsOnce()
        {
            var s = T.NewSession(1000000, 8);
            s.State.Upgrades["oto_kaziyici"] = 1; s.State.Upgrades["oto_alim"] = 1; s.RecalcStats();
            var ev = T.Capture(s);
            var r = s.ApplyOffline(300);
            Assert.True(r.Cards > 0);
            Assert.Single(ev);
            Assert.Equal(EventKind.OfflineReport, ev[0].Kind);
            Assert.Matches(@"^Sen yokken \d+ kart kazındı, .* (kazandın|kaybettin)\.$", ev[0].Speech);
            Assert.DoesNotContain(ev, e => e.Kind == EventKind.CardResult);
        }

        [Fact]
        public void OfflineWithoutAutoBuyOnlyBurnsQueuedCards()
        {
            var s = T.NewSession(1000, 8);
            s.State.Upgrades["oto_kaziyici"] = 1; s.RecalcStats();
            s.State.Bag.Add(s.CreateCard(T.Type("simitci")));
            var r = s.ApplyOffline(600);
            Assert.Equal(1, r.Cards);
            Assert.Empty(s.State.Bag);
        }

        [Fact]
        public void NegativeOrNaNSecondsAreIgnored()
        {
            var s = T.NewSession(10);
            s.State.Upgrades["oto_kaziyici"] = 1; s.RecalcStats();
            Assert.Equal(0, s.ApplyOffline(-5).SecondsCounted);
            Assert.Equal(0, s.ApplyOffline(double.NaN).SecondsCounted);
            Assert.Equal(0, s.ApplyOfflineSince(1000).SecondsCounted); // LastSaveUnix yok
        }
    }
}
