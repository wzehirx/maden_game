using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Kazi.Core;
using Xunit;

namespace Kazi.Core.Tests
{
    public class MiniJsonTests
    {
        [Fact]
        public void ParsesObjectsArraysAndScalars()
        {
            var o = MiniJson.AsObject(MiniJson.Parse("{\"a\":1.5,\"b\":[1,2,3],\"c\":{\"d\":\"x\"},\"e\":true,\"f\":null}"));
            Assert.Equal(1.5, MiniJson.GetDouble(o, "a", 0));
            Assert.Equal(3, MiniJson.GetArray(o, "b").Count);
            Assert.Equal("x", MiniJson.GetString(MiniJson.GetObject(o, "c"), "d", ""));
            Assert.True(MiniJson.GetBool(o, "e", false));
            Assert.Equal(-1, MiniJson.GetInt(o, "yok", -1));
        }

        [Fact]
        public void StringEscapesAndUnicodeRoundTrip()
        {
            var d = new Dictionary<string, object> { { "s", "Kazı \"Kazan\" \\ \n İğş" } };
            var back = MiniJson.AsObject(MiniJson.Parse(MiniJson.Serialize(d)));
            Assert.Equal("Kazı \"Kazan\" \\ \n İğş", MiniJson.GetString(back, "s", ""));
            Assert.Equal("A", MiniJson.GetString(MiniJson.AsObject(MiniJson.Parse("{\"s\":\"\\u0041\"}")), "s", ""));
        }

        [Theory]
        [InlineData("")]
        [InlineData("{")]
        [InlineData("{\"a\":}")]
        [InlineData("[1,2")]
        [InlineData("{\"a\":1} x")]
        [InlineData("nope")]
        public void InvalidJsonThrowsFormatException(string json)
        {
            Assert.Throws<FormatException>(() => MiniJson.Parse(json));
        }

        [Fact]
        public void LongValuesAreClamped()
        {
            var o = MiniJson.AsObject(MiniJson.Parse("{\"a\":1e30,\"b\":-1e30}"));
            Assert.Equal(9000000000000000L, MiniJson.GetLong(o, "a", 0));
            Assert.Equal(-9000000000000000L, MiniJson.GetLong(o, "b", 0));
        }

        [Fact]
        public void PrettySerializeParsesBack()
        {
            var d = new Dictionary<string, object> { { "x", new List<object> { 1, 2, "z" } }, { "y", 3.25 } };
            var back = MiniJson.AsObject(MiniJson.Parse(MiniJson.Serialize(d, true)));
            Assert.Equal(3.25, MiniJson.GetDouble(back, "y", 0));
            Assert.Equal(3, MiniJson.GetArray(back, "x").Count);
        }
    }

    public class NumberFormatTests
    {
        [Theory]
        [InlineData(0, "0")]
        [InlineData(7, "7")]
        [InlineData(999, "999")]
        [InlineData(1000, "1 bin")]
        [InlineData(1234, "1,2 bin")]
        [InlineData(12500, "13 bin")]
        [InlineData(999999, "1 milyon")]
        [InlineData(1200000, "1,2 milyon")]
        [InlineData(3400000, "3,4 milyon")]
        [InlineData(2500000000L, "2,5 milyar")]
        [InlineData(-5, "eksi 5")]
        public void ShortFormat(long v, string expected) { Assert.Equal(expected, NumberFormat.Short(v)); }

        [Fact]
        public void LiraAndHugeValues()
        {
            Assert.Equal("250 lira", NumberFormat.Lira(250));
            Assert.Equal("1,2 bin lira", NumberFormat.Lira(1234));
            Assert.Contains("kentilyon", NumberFormat.Short(4000000000000000000L));
            Assert.Equal("2,5", NumberFormat.Dec(2.5));
            Assert.Equal("3", NumberFormat.Dec(3));
        }

        [Fact]
        public void DurationFormat()
        {
            Assert.Equal("45 saniye", NumberFormat.Duration(45));
            Assert.Equal("5 dakika", NumberFormat.Duration(300));
            Assert.Equal("2 saat", NumberFormat.Duration(7200));
            Assert.Equal("1 saat 30 dakika", NumberFormat.Duration(5400));
        }
    }

    public class WalletTests
    {
        [Fact]
        public void NeverNegative()
        {
            var w = new Wallet();
            w.Add(100);
            Assert.False(w.TrySpend(101));
            Assert.Equal(100, w.Balance);
            Assert.True(w.TrySpend(100));
            Assert.Equal(0, w.Balance);
            w.Balance = -50;
            Assert.Equal(0, w.Balance);
            w.Add(-10);
            Assert.Equal(0, w.Balance);
            w.Add(30);
            Assert.Equal(30, w.SpendUpTo(1000));
            Assert.Equal(0, w.Balance);
        }

        [Fact]
        public void AddSaturatesInsteadOfOverflowing()
        {
            var w = new Wallet { Balance = long.MaxValue - 5 };
            w.Add(100);
            Assert.Equal(long.MaxValue, w.Balance);
        }
    }

    public class ConfigTests
    {
        [Fact]
        public void ContentMeetsDesignTargets()
        {
            var c = T.Cfg;
            Assert.Equal(5, c.CatalogCount);
            Assert.True(c.CardTypes.Count >= 10);
            Assert.True(c.Upgrades.Count >= 30);
            Assert.InRange(c.Nodes.Count, 22, 26);
            Assert.Equal(7200, c.Settings.OfflineCapSeconds);
            Assert.True(c.Settings.HelpMoney > 0);
            Assert.InRange(c.Settings.DiscardRefundPct, 0.01, 0.99);
        }

        [Fact]
        public void EveryRuleIsUsed()
        {
            foreach (CardRule r in Enum.GetValues(typeof(CardRule)))
                Assert.Contains(T.Cfg.CardTypes, t => t.Rule == r);
        }

        [Fact]
        public void EachCatalogHasTypesAndGate_AndPricesRiseByTensToHundreds()
        {
            var c = T.Cfg;
            Assert.Equal(10, c.CheapestInCatalogs(1).Price);
            long prev = 0;
            for (int k = 1; k <= c.CatalogCount; k++)
            {
                var inCat = c.CardTypes.Where(t => t.Catalog == k).ToList();
                Assert.True(inCat.Count >= 2);
                Assert.NotNull(c.GateOfCatalog(k));
                long cheapest = c.CheapestInCatalogs(k).Price;
                long cheapestOfCat = inCat.Where(t => !t.IsGate).Min(t => t.Price);
                if (prev > 0) Assert.InRange(cheapestOfCat / (double)prev, 25, 100);
                prev = cheapestOfCat;
            }
            Assert.Equal(CardRule.AltinBilet, c.GateOfCatalog(5).Rule);
        }

        [Fact]
        public void NamesAreOriginalTurkish()
        {
            string[] banned = { "scritchy", "lucky cat", "snake eyes", "final chance", "quick cash", "jack points", "mundo" };
            var all = T.Cfg.CardTypes.Select(t => t.Name).Concat(T.Cfg.Upgrades.Select(u => u.Name)).Concat(T.Cfg.Nodes.Select(n => n.Name))
                .Concat(T.Cfg.CardTypes.SelectMany(t => t.Symbols.Select(s => s.Name))).ToList();
            foreach (string n in all)
            {
                Assert.False(string.IsNullOrWhiteSpace(n));
                foreach (string b in banned) Assert.DoesNotContain(b, n.ToLowerInvariant());
            }
            Assert.Contains(T.Cfg.CardTypes, t => t.Name == "Simitçi");
        }

        [Fact]
        public void PrestigeTreeIsAcyclicAndRootsExist()
        {
            var c = T.Cfg;
            Assert.Contains(c.Nodes, n => string.IsNullOrEmpty(n.Requires));
            foreach (var n in c.Nodes)
            {
                var seen = new HashSet<string>();
                var cur = n;
                while (cur != null && !string.IsNullOrEmpty(cur.Requires))
                {
                    Assert.True(seen.Add(cur.Id), "döngü: " + n.Id);
                    cur = c.NodeById[cur.Requires];
                }
            }
        }

        [Fact]
        public void FirstAutomationExistsAndGadgetsPresent()
        {
            var c = T.Cfg;
            Assert.Equal(EffectKind.AutoUnlock, c.UpgradeById["oto_kaziyici"].Kind);
            foreach (string g in new[] { "sayac", "mercek", "cikrik", "pres", "zil" }) Assert.Equal("gadget", c.UpgradeById[g].Category);
            Assert.Equal(EffectKind.AutoBuy, c.UpgradeById["oto_alim"].Kind);
        }

        [Fact]
        public void InvalidConfigThrowsFormatException()
        {
            Assert.Throws<FormatException>(() => GameConfig.FromJson("{}", "{}", "{}", "{}"));
        }
    }
}
