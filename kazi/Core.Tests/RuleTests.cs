using System;
using System.Collections.Generic;
using System.Linq;
using Kazi.Core;
using Xunit;

namespace Kazi.Core.Tests
{
    public class RuleTests
    {
        private static double Pay(string type, string sym) { var t = T.Type(type); return t.Symbols[t.SymbolIndex(sym)].Pay; }

        [Fact]
        public void ThreeOfAKind_PaysTheTripleSymbol()
        {
            var r = T.Make("simitci", "simit", "cay", "simit", "guvercin", "simit", "cay").Preview();
            Assert.True(r.Win);
            Assert.Equal(Pay("simitci", "simit"), r.PayMult, 6);
        }

        [Fact]
        public void ThreeOfAKind_PairsOnlyPayNothing()
        {
            var r = T.Make("simitci", "simit", "simit", "cay", "cay", "guvercin", "guvercin").Preview();
            Assert.False(r.Win);
            Assert.Equal(0, r.PayMult);
        }

        [Fact]
        public void ThreeOfAKind_TwoTriplesAddUp()
        {
            var r = T.Make("simitci", "simit", "simit", "simit", "cay", "cay", "cay").Preview();
            Assert.Equal(Pay("simitci", "simit") + Pay("simitci", "cay"), r.PayMult, 6);
        }

        [Fact]
        public void NumberHunt_SingleAndDoubleMatch()
        {
            // 0..2 kazanan, 3..8 oyuncu
            var one = T.Make("cay_bardagi", "n1", "n2", "n3", "n1", "n4", "n5", "n6", "n7", "n8").Preview();
            Assert.Equal(1, one.Matches);
            Assert.Equal(Pay("cay_bardagi", "n1"), one.PayMult, 6);
            var two = T.Make("cay_bardagi", "n1", "n2", "n3", "n1", "n2", "n5", "n6", "n7", "n8").Preview();
            Assert.Equal(2, two.Matches);
            Assert.Equal(Pay("cay_bardagi", "n1") + Pay("cay_bardagi", "n2"), two.PayMult, 6);
            var none = T.Make("cay_bardagi", "n1", "n2", "n3", "n4", "n5", "n6", "n7", "n8", "n4").Preview();
            Assert.False(none.Win);
        }

        [Fact]
        public void Multiplier_AppliesOnlyOnWin()
        {
            var t = T.Type("lale_tezgahi");
            int idx = t.Multipliers.FindIndex(m => m.Mult == 5);
            var win = T.MakeMul("lale_tezgahi", idx, "lale", "lale", "lale", "fener", "fener", "kilim").Preview();
            Assert.Equal(Pay("lale_tezgahi", "lale") * 5, win.PayMult, 6);
            Assert.Equal(5, win.Multiplier);
            var lose = T.MakeMul("lale_tezgahi", idx, "lale", "lale", "fener", "fener", "kilim", "kilim").Preview();
            Assert.False(lose.Win);
        }

        [Fact]
        public void Trap_PenaltyAndWinAreSeparate()
        {
            var win = T.Make("fener_ustasi", "kapi", "kapi", "kapi", "fener", "fener", "ay", "akrep", "yildiz", "sandik").Preview();
            Assert.True(win.Win);
            Assert.Equal(1, win.TrapCount);
            Assert.Equal(Pay("fener_ustasi", "akrep"), win.PenaltyMult, 6);
            var onlyTraps = T.Make("fener_ustasi", "kapi", "fener", "sandik", "ay", "yildiz", "tac", "akrep", "kara_bulut", "kapi").Preview();
            Assert.False(onlyTraps.Win);
            Assert.Equal(2, onlyTraps.TrapCount);
            Assert.Equal(Pay("fener_ustasi", "akrep") + Pay("fener_ustasi", "kara_bulut"), onlyTraps.PenaltyMult, 6);
        }

        [Fact]
        public void Trap_SymbolsDoNotFormTriplesThemselves()
        {
            var r = T.Make("fener_ustasi", "akrep", "akrep", "akrep", "kapi", "fener", "sandik", "ay", "yildiz", "tac").Preview();
            Assert.False(r.Win);
        }

        [Fact]
        public void Joker_SubstitutesForAnySymbol()
        {
            var r = T.Make("kervan_jokeri", "kehribar", "kehribar", "joker", "mercan", "inci", "zeytin").Preview();
            Assert.True(r.Win);
            Assert.Equal(Pay("kervan_jokeri", "kehribar"), r.PayMult, 6);
            var no = T.Make("kervan_jokeri", "kehribar", "joker", "mercan", "inci", "zeytin", "baharat").Preview();
            Assert.False(no.Win);
        }

        [Fact]
        public void Joker_PicksBestPayingSymbol()
        {
            var r = T.Make("kervan_jokeri", "kehribar", "kehribar", "kervan", "kervan", "joker", "inci").Preview();
            Assert.Equal(Pay("kervan_jokeri", "kervan"), r.PayMult, 6);
        }

        [Fact]
        public void Sequence_CompletedRowOrColumn()
        {
            var row = T.Make("kilim_deseni", "sedef", "sedef", "sedef", "kilim1", "ibrik", "hali", "seccade", "sultan", "kilim1").Preview();
            Assert.True(row.Win);
            Assert.Equal(Pay("kilim_deseni", "sedef"), row.PayMult, 6);
            var col = T.Make("kilim_deseni", "ibrik", "kilim1", "hali", "ibrik", "sedef", "seccade", "ibrik", "sultan", "kilim1").Preview();
            Assert.Equal(Pay("kilim_deseni", "ibrik"), col.PayMult, 6);
            var diag = T.Make("kilim_deseni", "ibrik", "kilim1", "hali", "sedef", "ibrik", "seccade", "kilim1", "sultan", "ibrik").Preview();
            Assert.False(diag.Win);
        }

        [Fact]
        public void Jackpot_PaysHugeAmount()
        {
            var r = T.Make("bogaz_yalisi", "hilal", "hilal", "hilal", "yali", "martik", "kule").Preview();
            Assert.Equal(Pay("bogaz_yalisi", "hilal"), r.PayMult, 6);
            Assert.True(r.PayMult >= 100);
        }

        [Fact]
        public void SonSans_KeyTriplePaysAndHitsGate()
        {
            var hit = T.Make("mahalle_sonsans", "anahtar", "anahtar", "anahtar", "kilit", "cira", "defter").Preview();
            Assert.True(hit.GateHit);
            Assert.True(hit.Win);
            var miss = T.Make("mahalle_sonsans", "anahtar", "anahtar", "kilit", "kilit", "cira", "cira").Preview();
            Assert.False(miss.GateHit);
            var ab = T.Make("altin_bilet", "anahtar", "anahtar", "anahtar", "kilit", "cira", "defter").Preview();
            Assert.True(ab.GateHit);
        }
    }

    public class GenerationTests
    {
        private static IEnumerable<CardTypeDef> All { get { return T.Cfg.CardTypes; } }

        [Fact]
        public void SameSeedSameCard_DifferentSeedsDiffer()
        {
            foreach (var t in All)
            {
                var a = CardFactory.Create(t, 12345, 0, T.Cfg.Settings, false);
                var b = CardFactory.Create(t, 12345, 0, T.Cfg.Settings, false);
                Assert.Equal(a.Cells, b.Cells);
                bool differs = false;
                for (ulong s = 1; s < 50 && !differs; s++) differs = !CardFactory.Create(t, s, 0, T.Cfg.Settings, false).Cells.SequenceEqual(a.Cells);
                Assert.True(differs, t.Id);
            }
        }

        [Fact]
        public void CardsHaveCorrectCellCountAndValidSymbols()
        {
            foreach (var t in All)
                for (ulong s = 0; s < 200; s++)
                {
                    var c = CardFactory.Create(t, s, 5, T.Cfg.Settings, s % 7 == 0);
                    Assert.Equal(t.Cells, c.Cells.Length);
                    for (int i = 0; i < c.Cells.Length; i++)
                    {
                        int limit = (t.Rule == CardRule.Multiplier && i == 6) ? t.Multipliers.Count : t.Symbols.Count;
                        Assert.InRange(c.Cells[i], 0, limit - 1);
                    }
                }
        }

        [Fact]
        public void ForceWinAlwaysWins()
        {
            foreach (var t in All)
                for (ulong s = 0; s < 100; s++)
                    Assert.True(CardFactory.Create(t, s, 0, T.Cfg.Settings, true).Preview().Win, t.Id);
        }

        [Fact]
        public void WinFrequencyMatchesConfiguredChance_LayoutIsExact()
        {
            // Üretici sonucu önceden seçer; yerleşim kazanmayı/kaybı kazara değiştirmemeli.
            foreach (var t in All)
            {
                int n = 4000, wins = 0;
                for (int i = 0; i < n; i++) if (CardFactory.Create(t, (ulong)(i * 31 + 7), 0, T.Cfg.Settings, false).Preview().Win) wins++;
                double f = wins / (double)n;
                Assert.InRange(f, t.WinChance - 0.045, t.WinChance + 0.045);
            }
        }

        [Fact]
        public void RtpOfEveryCardIsBetween085And160_BySampling()
        {
            var set = T.Cfg.Settings;
            foreach (var t in All)
            {
                int n = 60000; double total = 0;
                for (int i = 0; i < n; i++)
                {
                    var c = CardFactory.Create(t, (ulong)i * 2654435761UL + 99, 0, set, false);
                    var r = c.Preview();
                    total += r.PayMult - r.PenaltyMult;
                }
                double rtp = total / n;
                Assert.InRange(rtp, 0.85, 1.6);
                double analytic = Odds.Rtp(t, 0, set);
                Assert.InRange(analytic, 0.85, 1.6);
                Assert.InRange(Math.Abs(rtp - analytic), 0, 0.09);
            }
        }

        [Fact]
        public void LuckRaisesRtpButWinChanceIsCapped()
        {
            var set = T.Cfg.Settings;
            foreach (var t in All)
            {
                double r0 = Odds.Rtp(t, 0, set), r1 = Odds.Rtp(t, 10, set), r2 = Odds.Rtp(t, 60, set);
                Assert.True(r1 > r0, t.Id);
                Assert.True(r2 >= r1, t.Id);
                Assert.True(Odds.WinChance(t, 1000, set) <= t.MaxWinChance + 1e-9);
                Assert.Equal(t.WinChance, Odds.WinChance(t, 0, set), 9);
            }
        }

        [Fact]
        public void LuckShiftsTrapChanceDown()
        {
            var t = T.Type("fener_ustasi");
            Assert.True(Odds.TrapChance(t, 40, T.Cfg.Settings) < Odds.TrapChance(t, 0, T.Cfg.Settings));
            Assert.True(Odds.TrapChance(t, 1e6, T.Cfg.Settings) >= t.TrapChance * (1 - t.MaxTrapReduction) - 1e-9);
        }

        [Fact]
        public void PityMathMatchesFormula()
        {
            var t = T.Type("mahalle_sonsans");
            double p = t.WinChance;
            double expected = (1 - Math.Pow(1 - p, t.PityAfter + 1)) / p;
            Assert.Equal(expected, Odds.ExpectedTrialsPerWin(t, 0, T.Cfg.Settings), 9);
            Assert.True(Odds.ExpectedTrialsPerWin(t, 0, T.Cfg.Settings) < 1 / p);
        }

        [Fact]
        public void NoCardTypeIsASureLoser_WithUpgrades()
        {
            var set = T.Cfg.Settings;
            foreach (var t in All) Assert.True(Odds.Rtp(t, 5, set) * 1.2 > 1.0, t.Id);
        }
    }

    public class CardScratchTests
    {
        [Fact]
        public void SequentialOpensPowerCellsInOrder()
        {
            var c = T.Make("simitci", "simit", "cay", "simit", "guvercin", "simit", "cay");
            var o = c.ScratchSequential(2);
            Assert.Equal(new[] { 0, 1 }, o);
            Assert.Equal(2, c.OpenCount);
            Assert.Equal(new[] { 2, 3, 4 }, c.ScratchSequential(3));
            Assert.Equal(new[] { 5 }, c.ScratchSequential(3));
            Assert.True(c.AllOpen);
        }

        [Fact]
        public void DragThresholdsPeekThenOpen()
        {
            var c = T.Make("simitci", "simit", "cay", "simit", "guvercin", "simit", "cay");
            var set = T.Cfg.Settings;
            Assert.Equal(ScratchOutcome.None, c.ScratchCell(0, 0.1, set.OpenThreshold, set.PeekThreshold));
            Assert.Equal(ScratchOutcome.Peek, c.ScratchCell(0, 0.2, set.OpenThreshold, set.PeekThreshold));
            Assert.False(c.Open[0]);
            Assert.Equal(ScratchOutcome.Open, c.ScratchCell(0, 0.4, set.OpenThreshold, set.PeekThreshold));
            Assert.True(c.Open[0]);
            Assert.Equal(ScratchOutcome.None, c.ScratchCell(0, 0.4, set.OpenThreshold, set.PeekThreshold));
            Assert.Equal(ScratchOutcome.None, c.ScratchCell(99, 0.4, set.OpenThreshold, set.PeekThreshold));
            Assert.Equal(ScratchOutcome.None, c.ScratchCell(1, double.NaN, set.OpenThreshold, set.PeekThreshold));
        }

        [Fact]
        public void OpenAllRevealsEverything()
        {
            var c = T.Make("simitci", "simit", "cay", "simit", "guvercin", "simit", "cay");
            c.ScratchSequential(1);
            Assert.Equal(5, c.OpenAll().Count);
            Assert.True(c.AllOpen);
        }

        [Fact]
        public void CellDescriptionsAreTurkish()
        {
            var nh = T.Make("cay_bardagi", "n1", "n2", "n3", "n1", "n4", "n5", "n6", "n7", "n8");
            Assert.Equal("Kazanan sayı bir", nh.DescribeCell(0));
            Assert.Equal("Senin sayın bir", nh.DescribeCell(3));
            var tr = T.Make("fener_ustasi", "kapi", "kapi", "kapi", "fener", "fener", "ay", "akrep", "yildiz", "sandik");
            Assert.StartsWith("Dikkat", tr.DescribeCell(6));
            var mu = T.MakeMul("lale_tezgahi", 1, "lale", "lale", "lale", "fener", "fener", "kilim");
            Assert.Contains("kat", mu.DescribeCell(6));
        }
    }
}
