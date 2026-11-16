using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Maden.Core;
using Xunit;

namespace Maden.Core.Tests
{
    public class MiniJsonTests
    {
        [Fact]
        public void ParsesAndWritesRoundTrip()
        {
            string src = "{\"a\":1,\"b\":[true,false,null,\"x\\n\\\"y\\u00e7\"],\"c\":{\"d\":-2.5e1}}";
            var o = MiniJson.AsObject(MiniJson.Parse(src));
            Assert.Equal(1.0, o["a"]);
            var arr = MiniJson.AsArray(o["b"]);
            Assert.Equal(4, arr.Count);
            Assert.Equal("x\n\"yç", arr[3]);
            Assert.Equal(-25.0, MiniJson.GetDouble(MiniJson.GetObject(o, "c"), "d", 0));
            var again = MiniJson.AsObject(MiniJson.Parse(MiniJson.Serialize(o)));
            Assert.Equal(1.0, again["a"]);
        }

        [Theory]
        [InlineData("")]
        [InlineData("{")]
        [InlineData("{\"a\":}")]
        [InlineData("[1,2")]
        [InlineData("{\"a\":1} fazlalık")]
        [InlineData("nul")]
        public void BadJsonThrowsFormatException(string bad)
        {
            Assert.Throws<FormatException>(() => MiniJson.Parse(bad));
        }
    }

    public class NumberFormatTests
    {
        [Theory]
        [InlineData(0, "0")]
        [InlineData(999, "999")]
        [InlineData(1000, "1 bin")]
        [InlineData(1200, "1,2 bin")]
        [InlineData(15300, "15 bin")]
        [InlineData(999950, "1 milyon")]
        [InlineData(3400000, "3,4 milyon")]
        [InlineData(2000000000, "2 milyar")]
        [InlineData(5600000000000, "5,6 trilyon")]
        public void ShortFormats(long v, string expected)
        {
            Assert.Equal(expected, NumberFormat.Short(v));
        }
    }

    public class ConfigTests
    {
        [Fact]
        public void ParsesSmallConfig()
        {
            var c = T.Small();
            Assert.Equal(3, c.Layers.Count);
            Assert.Equal(7, c.TotalDepth);
            Assert.Equal(7200, c.OfflineCapSeconds);
            Assert.Equal(Rarity.Rare, c.Layers[0].Items[2].Rarity);
            Assert.Equal(2, c.Machines.Count);
            Assert.Equal(1, c.FindUpgrade("gizli").MinLayer);
            Assert.Equal(0, c.LayerForBlocks(0));
            Assert.Equal(0, c.LayerForBlocks(2));
            Assert.Equal(1, c.LayerForBlocks(3));
            Assert.Equal(2, c.LayerForBlocks(5));
            Assert.Equal(2, c.LayerForBlocks(999));
        }

        [Fact]
        public void MissingLayersThrows()
        {
            Assert.Throws<FormatException>(() => EconomyConfig.FromJson("{\"pickaxes\":[{}]}"));
            Assert.Throws<FormatException>(() => EconomyConfig.FromJson("bozuk"));
        }

        static string RealPath()
        {
            string dir = AppContext.BaseDirectory;
            for (int i = 0; i < 8; i++)
            {
                string p = Path.Combine(dir, "economy.json");
                if (File.Exists(p)) return p;
                p = Path.Combine(dir, "Assets", "StreamingAssets", "economy.json");
                if (File.Exists(p)) return p;
                dir = Path.GetDirectoryName(dir);
            }
            throw new FileNotFoundException("economy.json");
        }

        [Fact]
        public void RealEconomyMeetsStructure()
        {
            var c = EconomyConfig.LoadFromFile(RealPath());
            Assert.Equal(10, c.Layers.Count);
            string[] names = { "Toprak", "Kil", "Taş", "Kömür", "Demir", "Gümüş", "Altın", "Kristal", "Elmas", "Kadim Hazine" };
            for (int i = 0; i < 10; i++) Assert.Equal(names[i], c.Layers[i].Name);
            foreach (var l in c.Layers)
            {
                Assert.True(l.Items.Count >= 6, l.Name);
                Assert.Contains(l.Items, it => it.Rarity == Rarity.Common);
                Assert.Contains(l.Items, it => it.Rarity == Rarity.Uncommon);
                Assert.Contains(l.Items, it => it.Rarity == Rarity.Rare);
                Assert.True(l.DepthBlocks > 0 && l.BlockHp > 0);
            }
            Assert.True(c.Upgrades.Count + c.Machines.Count >= 25);
            Assert.Equal(5, c.Pickaxes.Count);
            Assert.Equal(0, c.Pickaxes[0].Cost);
            for (int i = 1; i < 5; i++) { Assert.True(c.Pickaxes[i].Power > c.Pickaxes[i - 1].Power); Assert.True(c.Pickaxes[i].Cost > c.Pickaxes[i - 1].Cost); }
            Assert.Equal(7200, c.OfflineCapSeconds);
            Assert.Equal(30, c.AutosaveSeconds);
            Assert.Contains(c.Machines, m => m.Id == "dynamite");
            Assert.Contains(c.Machines, m => m.Id == "drill");
            Assert.True(c.Machines.Count(m => m.Kind == "dynamite") >= 2);
            Assert.True(c.Machines.Count(m => m.Kind == "drill") >= 2);
            // Her makine grubunun güç/hız yükseltmesi var
            foreach (var m in c.Machines)
            {
                Assert.NotNull(c.FindUpgrade(m.Id + "Power"));
                Assert.NotNull(c.FindUpgrade(m.Id + "Speed"));
            }
            // Ucuzdan değerliye: katman ortalama değeri artar
            double prev = 0;
            foreach (var l in c.Layers)
            {
                double avg = l.Items.Sum(i => i.Value * i.Weight) / l.Items.Sum(i => i.Weight);
                Assert.True(avg > prev, l.Name);
                prev = avg;
            }
            Assert.Contains(c.Layers[0].Items, i => i.Name == "Paslı çivi");
        }

        [Fact]
        public void RealEconomyPlaysWithoutErrors()
        {
            var c = EconomyConfig.LoadFromFile(RealPath());
            var s = new GameSession(c, new GameState(), new SystemRandom(1));
            for (int i = 0; i < 2000; i++) { s.Tap(); s.Tick(1.0 / 3); }
            s.SellAll();
            Assert.True(s.State.Money > 0);
            Assert.Contains("Para", s.ReadStatus());
        }
    }

    public class DiggingTests
    {
        [Fact]
        public void TapsBreakBlocksAndFillBag()
        {
            var log = new List<GameEvent>();
            var s = T.Session(0.5, null, null, log);
            s.Tap();
            Assert.Equal(0, s.State.TotalBlocks);
            Assert.Equal(0, s.State.BagCount);
            s.Tap(); // hp 2, güç 1 -> ikinci vuruşta kırılır
            Assert.Equal(1, s.State.TotalBlocks);
            Assert.Equal(1, s.State.BagCount);
            Assert.Contains(log, e => e.Kind == EventKind.Hit);
            var found = log.Single(e => e.Kind == EventKind.ItemFound);
            Assert.Equal("Çivi, 10 lira.", found.Speech);
            Assert.Equal(10, found.Amount);
            Assert.Equal(Rarity.Common, found.Rarity);
        }

        [Fact]
        public void CritDoublesUp()
        {
            var cfg = T.Small();
            cfg.CritBase = 0.5;
            var log = new List<GameEvent>();
            var s = new GameSession(cfg, new GameState(), new FixedRandom(0.1)); // 0.1 < 0.5 -> kritik
            s.OnEvent += e => log.Add(e);
            s.Tap();
            var hit = log.First(e => e.Kind == EventKind.Hit);
            Assert.Equal("Kritik vuruş!", hit.Speech);
            Assert.Equal(3, hit.Amount); // 1 güç x3
            Assert.Equal(1, s.State.TotalBlocks); // 3 hasar: blok 2 + 1 artık
        }

        [Fact]
        public void NoCritWhenChanceZero()
        {
            var log = new List<GameEvent>();
            var s = T.Session(0.0, null, null, log);
            s.Tap();
            Assert.Equal("", log.First(e => e.Kind == EventKind.Hit).Speech);
        }

        [Fact]
        public void PowerUpgradeIncreasesDamage()
        {
            var st = new GameState { Money = 1000 };
            var s = T.Session(0.5, st);
            Assert.Equal(1.0, s.TapDamage);
            s.ToggleShop();
            s.ShopNextCategory(); // Vuruş kategorisi: ilk öğe guc
            // ilk öğe taş kazma (index 0) -> sonraki güç
            var e = s.GetShopEntries()[1];
            Assert.Equal("guc", e.Id);
            s.ShopBuy();
            Assert.Equal(1.5, s.TapDamage, 6);
        }

        [Fact]
        public void ItemDistributionMatchesWeights()
        {
            var cfg = T.Small();
            var st = new GameState();
            var s = new GameSession(cfg, st, new SystemRandom(42));
            int common = 0, unc = 0, rare = 0;
            s.OnEvent += e =>
            {
                if (e.Kind == EventKind.RareFound) rare++;
            };
            // Toprak katmanında 3 blok var; dağılımı doğrudan PickItem yerine uzun oyunla ölç:
            var big = T.Big();
            big.Layers[0].Items.Clear();
            big.Layers[0].Items.Add(new ItemDef { Name = "a", Value = 1, Rarity = Rarity.Common, Weight = 90 });
            big.Layers[0].Items.Add(new ItemDef { Name = "b", Value = 1, Rarity = Rarity.Uncommon, Weight = 9 });
            big.Layers[0].Items.Add(new ItemDef { Name = "c", Value = 1, Rarity = Rarity.Rare, Weight = 1 });
            var bs = new GameSession(big, new GameState(), new SystemRandom(7));
            bs.Tick(1.0); // makine yok değil: drill unlockCost 0 ama sahip değiliz
            bs.State.MachineTimers["drill"] = 0;
            bs.Tick(20000); // 20000 matkap vuruşu = 20000 blok
            Assert.Equal(20000, bs.State.TotalBlocks);
            foreach (var kv in bs.State.Bag)
            {
                int idx = int.Parse(kv.Key.Split(':')[1]);
                if (idx == 0) common += kv.Value; else if (idx == 1) unc += kv.Value; else rare += kv.Value;
            }
            Assert.InRange(common / 20000.0, 0.88, 0.92);
            Assert.InRange(unc / 20000.0, 0.075, 0.105);
            Assert.InRange(rare / 20000.0, 0.004, 0.017);
        }

        [Fact]
        public void RareChanceUpgradeRaisesRareShare()
        {
            double Share(int level)
            {
                var big = T.Big();
                big.Layers[0].Items.Clear();
                big.Layers[0].Items.Add(new ItemDef { Name = "a", Value = 1, Rarity = Rarity.Common, Weight = 90 });
                big.Layers[0].Items.Add(new ItemDef { Name = "c", Value = 1, Rarity = Rarity.Rare, Weight = 10 });
                big.Upgrades.Add(new UpgradeDef { Id = "sans", Kind = "rareChance", EffectPerLevel = 1.0, MaxLevel = 5, BaseCost = 1, CostMult = 1 });
                var st = new GameState();
                st.UpgradeLevels["sans"] = level;
                st.MachineTimers["drill"] = 0;
                var bs = new GameSession(big, st, new SystemRandom(3));
                bs.Tick(10000);
                int rare = st.Bag.Where(kv => kv.Key.EndsWith(":1")).Sum(kv => kv.Value);
                return rare / (double)st.BagCount;
            }
            double s0 = Share(0), s3 = Share(3);
            Assert.InRange(s0, 0.08, 0.12);   // 10/100
            Assert.InRange(s3, 0.30, 0.37);   // 40/130 = 0.307
            Assert.True(s3 > s0 * 2.5);
        }

        [Fact]
        public void RareItemRaisesRareEvent()
        {
            var log = new List<GameEvent>();
            // 0.999 -> en son öğe (nadir Sikke)
            var s = T.Session(0.999, null, null, log);
            T.Tap(s, 2);
            var r = log.Single(e => e.Kind == EventKind.RareFound);
            Assert.Equal(Rarity.Rare, r.Rarity);
            Assert.Contains("Sikke", r.Speech);
            Assert.Equal(100, r.Amount);
            Assert.Equal(1, s.State.RareCount);
        }
    }

    public class SellingTests
    {
        [Fact]
        public void SellAllPaysAndEmptiesBag()
        {
            var log = new List<GameEvent>();
            var s = T.Session(0.5, null, null, log);
            T.Tap(s, 4); // 2 blok = 2 Çivi
            Assert.Equal(2, s.State.BagCount);
            s.SellAll();
            Assert.Equal(20, s.State.Money);
            Assert.Equal(0, s.State.BagCount);
            var sold = log.Single(e => e.Kind == EventKind.Sold);
            Assert.Equal(20, sold.Amount);
            Assert.Contains("2 eşya satıldı", sold.Speech);
            Assert.Contains("20 lira", sold.Speech);
        }

        [Fact]
        public void SellEmptyBagGivesError()
        {
            var log = new List<GameEvent>();
            var s = T.Session(0.5, null, null, log);
            s.SellAll();
            Assert.Equal(EventKind.Error, log.Single().Kind);
            Assert.Equal(0, s.State.Money);
        }

        [Fact]
        public void SellBonusMultipliesIncome()
        {
            var st = new GameState();
            st.UpgradeLevels["pazar"] = 2; // +%50 x2 => 1.5^2 = 2.25
            var s = T.Session(0.5, st);
            T.Tap(s, 2);
            s.SellAll();
            Assert.Equal(23, s.State.Money); // 10 * 2.25 = 22.5 -> 23 (yuvarlama)
        }

        [Fact]
        public void BagFullBlocksDiggingAndAnnounces()
        {
            var log = new List<GameEvent>();
            var s = T.Session(0.5, null, null, log); // çanta 3
            T.Tap(s, 6); // 3 blok
            Assert.Equal(3, s.State.BagCount);
            Assert.Contains(log, e => e.Kind == EventKind.BagFull && e.Speech.Contains("Çanta dolu"));
            long blocks = s.State.TotalBlocks;
            log.Clear();
            T.Tap(s, 3);
            Assert.Equal(blocks, s.State.TotalBlocks); // kazı durdu
            Assert.Contains(log, e => e.Kind == EventKind.BagFull);
            s.SellAll();
            T.Tap(s, 2);
            Assert.Equal(blocks + 1, s.State.TotalBlocks);
        }

        [Fact]
        public void BagUpgradeRaisesCapacity()
        {
            var st = new GameState();
            st.UpgradeLevels["canta"] = 2;
            var s = T.Session(0.5, st);
            Assert.Equal(13, s.BagCapacity);
        }

        [Fact]
        public void AutoSellSellsWhenFullAndNeverBlocks()
        {
            var log = new List<GameEvent>();
            var st = new GameState();
            st.UpgradeLevels["oto"] = 1;
            var s = T.Session(0.5, st, null, log);
            Assert.True(s.AutoSell);
            T.Tap(s, 10); // 5 blok > çanta 3
            Assert.Equal(5, s.State.TotalBlocks);
            Assert.DoesNotContain(log, e => e.Kind == EventKind.BagFull);
            Assert.Contains(log, e => e.Kind == EventKind.Sold && e.Speech.StartsWith("Otomatik satış"));
            Assert.True(s.State.Money >= 30);
        }
    }

    public class LayerAndEndingTests
    {
        [Fact]
        public void LayerChangesAtDepthBoundary()
        {
            var log = new List<GameEvent>();
            var st = new GameState();
            st.UpgradeLevels["canta"] = 3;
            var s = T.Session(0.5, st, null, log);
            T.Tap(s, 4); // 2 blok
            Assert.Equal(0, s.State.LayerIndex);
            T.Tap(s, 2); // 3. blok -> Kil
            Assert.Equal(1, s.State.LayerIndex);
            var e = log.Single(x => x.Kind == EventKind.LayerChanged);
            Assert.Contains("Kil", e.Speech);
            Assert.Equal(1, e.Layer);
            Assert.Contains("Kil", s.ReadStatus());
        }

        [Fact]
        public void LayerUpgradeAppearsOnlyAfterLayer()
        {
            var st = new GameState { Money = 100 };
            st.UpgradeLevels["canta"] = 3;
            var s = T.Session(0.5, st);
            Assert.DoesNotContain(s.GetShopEntries(), e => e.Id == "gizli");
            T.Tap(s, 6);
            Assert.Contains(s.GetShopEntries(), e => e.Id == "gizli");
        }

        [Fact]
        public void AncientTreasureEndsGame()
        {
            var log = new List<GameEvent>();
            var st = new GameState();
            st.UpgradeLevels["oto"] = 1;
            var s = T.Session(0.5, st, null, log);
            T.Tap(s, 40);
            Assert.True(s.State.Finished);
            Assert.Equal(7, s.State.TotalBlocks);
            var t = log.Single(e => e.Kind == EventKind.AncientTreasureFound);
            Assert.Contains("Kadim hazine", t.Speech);
            Assert.Equal(2, s.State.LayerIndex);
            // bittikten sonra kazı ve makine durur
            long money = s.State.Money;
            log.Clear();
            s.Tap();
            s.Tick(100);
            Assert.Equal(EventKind.Error, log.Single().Kind);
            Assert.Equal(7, s.State.TotalBlocks);
        }

        [Fact]
        public void NewGamePlusResetsWithPermanentMultiplier()
        {
            var log = new List<GameEvent>();
            var st = new GameState();
            st.UpgradeLevels["oto"] = 1;
            var s = T.Session(0.5, st, null, log);
            s.StartNewGamePlus(); // henüz bitmedi
            Assert.Equal(EventKind.Error, log.Last().Kind);
            Assert.Equal(0, s.State.PrestigeCount);
            T.Tap(s, 40);
            Assert.True(s.State.Finished);
            s.StartNewGamePlus();
            Assert.False(s.State.Finished);
            Assert.Equal(1, s.State.PrestigeCount);
            Assert.Equal(0, s.State.TotalBlocks);
            Assert.Equal(0, s.State.Money);
            Assert.Empty(s.State.UpgradeLevels);
            Assert.Equal(0, s.State.PickaxeTier);
            Assert.Equal(1.5, s.SellMultiplier, 6);
            // Kalıcı çarpan satışa yansır: 10 * 1.5 = 15
            T.Tap(s, 2);
            s.SellAll();
            Assert.Equal(15, s.State.Money);
            Assert.Contains("Yeni oyun artı", log.Last(e => e.Kind == EventKind.LayerChanged).Speech);
        }
    }

    public class ShopTests
    {
        [Fact]
        public void ShopOpenCloseAndNavigation()
        {
            var log = new List<GameEvent>();
            var s = T.Session(0.5, null, null, log);
            Assert.False(s.ShopOpen);
            s.ToggleShop();
            Assert.True(s.ShopOpen);
            Assert.Equal(EventKind.ShopOpened, log[0].Kind);
            Assert.StartsWith("Dükkân açıldı. Taş kazma", log[0].Speech);
            s.ShopNextCategory(); // Kazma -> Vuruş (3 öğe)
            string first = s.ReadShopItem();
            s.ShopNext();
            Assert.Equal(EventKind.ShopItemChanged, log.Last().Kind);
            Assert.NotEqual(first, s.ReadShopItem());
            s.ShopPrev();
            Assert.Equal(first, s.ReadShopItem());
            s.ShopPrev(); // kategori içinde başa sarar -> son öğe
            Assert.NotEqual(first, s.ReadShopItem());
            s.ToggleShop();
            Assert.False(s.ShopOpen);
            Assert.Equal(EventKind.ShopClosed, log.Last().Kind);
        }

        [Fact]
        public void NavigationWhenClosedIsError()
        {
            var log = new List<GameEvent>();
            var s = T.Session(0.5, null, null, log);
            s.ShopNext(); s.ShopBuy();
            Assert.All(log, e => Assert.Equal(EventKind.Error, e.Kind));
            Assert.Equal(2, log.Count);
        }

        [Fact]
        public void NotEnoughMoneyReportsShortfall()
        {
            var log = new List<GameEvent>();
            var st = new GameState { Money = 60 };
            var s = T.Session(0.5, st, null, log);
            s.ToggleShop(); // taş kazma 100
            Assert.Contains("Yetmiyor, 40 lira eksik", s.ReadShopItem());
            s.ShopBuy();
            var e = log.Last();
            Assert.Equal(EventKind.NotEnoughMoney, e.Kind);
            Assert.Equal(40, e.Amount);
            Assert.Contains("40 lira eksik", e.Speech);
            Assert.Equal(60, s.State.Money);
            Assert.Equal(0, s.State.PickaxeTier);
        }

        [Fact]
        public void BuyUpgradeCostsGrowGeometrically()
        {
            var log = new List<GameEvent>();
            var st = new GameState { Money = 1000 };
            var s = T.Session(0.5, st, null, log);
            var guc = s.Config.FindUpgrade("guc");
            Assert.Equal(10, GameSession.UpgradeCost(guc, 0));
            Assert.Equal(20, GameSession.UpgradeCost(guc, 1));
            Assert.Equal(40, GameSession.UpgradeCost(guc, 2));
            s.ToggleShop(); s.ShopNextCategory(); // Vuruş: guc
            s.ShopBuy(); s.ShopBuy();
            Assert.Equal(2, st.GetLevel("guc"));
            Assert.Equal(1000 - 10 - 20, st.Money);
            Assert.Equal(2, log.Count(e => e.Kind == EventKind.UpgradeBought));
            Assert.Contains("seviye 2", log.Last(e => e.Kind == EventKind.UpgradeBought).Speech);
        }

        [Fact]
        public void MaxedUpgradeLeavesShop()
        {
            var st = new GameState { Money = 100000 };
            st.UpgradeLevels["guc"] = 3;
            var s = T.Session(0.5, st);
            Assert.DoesNotContain(s.GetShopEntries(), e => e.Id == "guc");
        }

        [Fact]
        public void PickaxeTierAdvances()
        {
            var st = new GameState { Money = 500 };
            var s = T.Session(0.5, st);
            s.ToggleShop();
            s.ShopBuy(); // taş kazma
            Assert.Equal(1, st.PickaxeTier);
            Assert.Equal(2.0, s.TapDamage);
            Assert.DoesNotContain(s.GetShopEntries(), e => e.Kind == ShopKind.Pickaxe);
            Assert.Contains("Taş kazma", s.ReadStatus());
        }

        [Fact]
        public void MachineUpgradesHiddenUntilMachineOwned()
        {
            var st = new GameState { Money = 1000 };
            var s = T.Session(0.5, st);
            Assert.DoesNotContain(s.GetShopEntries(), e => e.Id == "dynamitePower");
            s.Buy(s.GetShopEntries().First(e => e.Id == "dynamite"));
            Assert.Contains(s.GetShopEntries(), e => e.Id == "dynamitePower");
            Assert.DoesNotContain(s.GetShopEntries(), e => e.Id == "dynamite");
        }

        [Fact]
        public void EmptyShopSpeech()
        {
            var cfg = EconomyConfig.FromJson(T.BigJson);
            cfg.Machines.Clear();
            var s = T.Session(0.5, null, cfg);
            Assert.Equal("Dükkânda alınacak bir şey kalmadı.", s.ReadShopItem());
        }
    }

    public class MachineTests
    {
        [Fact]
        public void DynamiteBlastsOnInterval()
        {
            var log = new List<GameEvent>();
            var st = new GameState();
            st.MachineTimers["dynamite"] = 0;
            st.UpgradeLevels["canta"] = 3;
            var s = T.Session(0.5, st, null, log);
            s.Tick(9.9);
            Assert.Equal(0, st.TotalBlocks);
            s.Tick(0.2); // 10 sn doldu: güç 4 / hp 2 = 2 blok
            Assert.Equal(2, st.TotalBlocks);
            var e = log.Single(x => x.Kind == EventKind.MachineBlast);
            Assert.Equal(2, e.Amount);
            Assert.Contains("Dinamit patladı", e.Speech);
            Assert.Contains("2 blok", e.Speech);
        }

        [Fact]
        public void DynamiteSpeedAndPowerUpgrades()
        {
            var st = new GameState();
            st.MachineTimers["dynamite"] = 0;
            st.UpgradeLevels["dynamiteSpeed"] = 1; // x2 hız -> 5 sn
            st.UpgradeLevels["dynamitePower"] = 1; // x2 güç -> 8 hasar
            st.UpgradeLevels["canta"] = 3;
            var s = T.Session(0.5, st);
            var m = s.Config.FindMachine("dynamite");
            Assert.Equal(5.0, s.MachineInterval(m), 6);
            Assert.Equal(8.0, s.MachinePower(m), 6);
            s.Tick(5.0);
            Assert.Equal(4, st.TotalBlocks); // 8 hasar / hp 2 = 4 blok
        }

        [Fact]
        public void DrillWorksContinuouslyAndReportsPeriodically()
        {
            var log = new List<GameEvent>();
            var st = new GameState();
            st.MachineTimers["drill"] = 0;
            st.UpgradeLevels["canta"] = 3;
            var s = T.Session(0.5, st, null, log);
            for (int i = 0; i < 6; i++) s.Tick(1.0);
            Assert.Equal(3, st.TotalBlocks); // 6 hasar / hp 2
            Assert.Equal(1, log.Count(e => e.Kind == EventKind.MachineDrill));
            Assert.Contains("Matkaplar 3 blok", log.First(e => e.Kind == EventKind.MachineDrill).Speech);
        }

        [Fact]
        public void MachinesStallWhenBagFullWithoutAutoSell()
        {
            var st = new GameState();
            st.MachineTimers["drill"] = 0;
            var s = T.Session(0.5, st); // çanta 3
            s.Tick(100);
            Assert.Equal(3, st.BagCount);
            Assert.Equal(3, st.TotalBlocks);
        }

        [Fact]
        public void MachinesKeepWorkingWithAutoSell()
        {
            var st = new GameState();
            st.MachineTimers["drill"] = 0;
            st.UpgradeLevels["oto"] = 1;
            var s = T.Session(0.5, st);
            s.Tick(100);
            Assert.Equal(7, st.TotalBlocks);
            Assert.True(st.Finished);
            Assert.True(st.Money > 0);
        }

        [Fact]
        public void BuyingMachineStartsIt()
        {
            var st = new GameState { Money = 50 };
            var s = T.Session(0.5, st);
            var e = s.GetShopEntries().First(x => x.Id == "drill");
            Assert.Equal(ShopKind.Machine, e.Kind);
            Assert.True(s.Buy(e));
            Assert.True(st.OwnsMachine("drill"));
            Assert.Equal(0, st.Money);
        }
    }

    public class OfflineTests
    {
        [Fact]
        public void OfflineIsCappedAtTwoHours()
        {
            var st = new GameState();
            st.MachineTimers["drill"] = 0;
            var s = T.Session(0.5, st, T.Big());
            var r = s.ApplyOffline(10 * 3600);
            Assert.Equal(7200, r.SecondsCounted);
            Assert.Equal(7200, r.Blocks);
            Assert.Equal(7200, st.TotalBlocks);
            Assert.Contains("Hoş geldin", r.Speech);
            Assert.Contains("en fazla 2 saatlik", r.Speech);
            Assert.Contains("7,2 bin blok", r.Speech);
        }

        [Fact]
        public void ShortAbsenceCountsExactly()
        {
            var st = new GameState();
            st.MachineTimers["drill"] = 0;
            var s = T.Session(0.5, st, T.Big());
            var r = s.ApplyOffline(600);
            Assert.Equal(600, r.SecondsCounted);
            Assert.Equal(600, r.Blocks);
            Assert.Contains("10 dakika", r.Speech);
        }

        [Fact]
        public void OfflineEmitsNoEvents()
        {
            var log = new List<GameEvent>();
            var st = new GameState();
            st.MachineTimers["drill"] = 0;
            var s = T.Session(0.5, st, T.Big(), log);
            s.ApplyOffline(300);
            Assert.Empty(log);
            s.Tick(1); // normal akış tekrar konuşur
        }

        [Fact]
        public void OfflineWithoutMachinesSaysSo()
        {
            var s = T.Session(0.5);
            var r = s.ApplyOffline(3600);
            Assert.Equal(0, r.Blocks);
            Assert.Contains("Makinen olmadığı", r.Speech);
        }

        [Fact]
        public void OfflineAutoSellEarnsMoney()
        {
            var st = new GameState();
            st.MachineTimers["drill"] = 0;
            st.UpgradeLevels["oto"] = 1;
            var s = T.Session(0.5, st);
            var r = s.ApplyOffline(3600);
            Assert.True(r.TreasureFound);
            Assert.True(r.Money > 0);
            Assert.Contains("kazandın", r.Speech);
        }

        [Fact]
        public void NegativeOrNaNAwayIsSafe()
        {
            var s = T.Session(0.5);
            Assert.Equal("", s.ApplyOffline(-5).Speech);
            Assert.Equal("", s.ApplyOffline(double.NaN).Speech);
        }
    }

    public class SaveTests
    {
        static GameState Played()
        {
            var cfg = T.Small();
            var st = new GameState { Money = 123456, PrestigeCount = 2, LastSaveUnix = 1700000000 };
            st.UpgradeLevels["guc"] = 2;
            st.UpgradeLevels["canta"] = 3;
            var s = new GameSession(cfg, st, new FixedRandom(0.5));
            s.Tap(); s.Tap(); s.Tap();
            st.MachineTimers["dynamite"] = 4.5;
            st.PickaxeTier = 1;
            return st;
        }

        [Fact]
        public void RoundTripKeepsEverything()
        {
            var cfg = T.Small();
            var a = Played();
            string json = SaveSystem.Serialize(a);
            GameState b;
            Assert.True(SaveSystem.TryDeserialize(json, cfg, out b));
            Assert.Equal(a.Money, b.Money);
            Assert.Equal(a.TotalBlocks, b.TotalBlocks);
            Assert.Equal(a.BlockHp, b.BlockHp);
            Assert.Equal(a.PickaxeTier, b.PickaxeTier);
            Assert.Equal(a.PrestigeCount, b.PrestigeCount);
            Assert.Equal(a.TotalTaps, b.TotalTaps);
            Assert.Equal(a.LastSaveUnix, b.LastSaveUnix);
            Assert.Equal(2, b.GetLevel("guc"));
            Assert.Equal(4.5, b.MachineTimers["dynamite"]);
            Assert.Equal(a.Bag.Count, b.Bag.Count);
            Assert.Equal(a.BagCount, b.BagCount);
            Assert.Equal(GameState.CurrentVersion, b.Version);
            Assert.Equal(json, SaveSystem.Serialize(b));
        }

        [Fact]
        public void FileRoundTrip()
        {
            var cfg = T.Small();
            string path = Path.Combine(Path.GetTempPath(), "maden_test_" + Guid.NewGuid().ToString("N") + ".json");
            try
            {
                var a = Played();
                SaveSystem.SaveToFile(a, path);
                SaveSystem.SaveToFile(a, path); // üzerine yazma
                var b = SaveSystem.LoadFromFile(path, cfg);
                Assert.Equal(a.Money, b.Money);
                Assert.Equal(0, SaveSystem.LoadFromFile(path + ".yok", cfg).Money);
            }
            finally { if (File.Exists(path)) File.Delete(path); }
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("{bozuk")]
        [InlineData("[1,2,3]")]
        [InlineData("42")]
        [InlineData("{\"money\": \"çok\"}")]
        [InlineData("{\"money\": 5, \"bag\": ")]
        [InlineData("\u0000\u0001\u0002")]
        public void CorruptSavesNeverThrowAndGiveFreshState(string bad)
        {
            var cfg = T.Small();
            GameState s;
            bool ok = SaveSystem.TryDeserialize(bad, cfg, out s);
            if (bad == "{\"money\": \"çok\"}") { Assert.True(ok); Assert.Equal(0, s.Money); }
            else Assert.False(ok);
            Assert.NotNull(s);
            Assert.Equal(0, s.TotalBlocks);
            var session = new GameSession(cfg, s, new FixedRandom(0.5));
            session.Tap(); // oyun hâlâ çalışır
        }

        [Fact]
        public void InsaneValuesAreSanitized()
        {
            var cfg = T.Small();
            string json = "{\"money\":-50,\"totalBlocks\":99999,\"pickaxeTier\":9,\"blockHp\":-3," +
                          "\"upgradeLevels\":{\"guc\":99,\"yok\":4,\"krit\":-2}," +
                          "\"machineTimers\":{\"dynamite\":999,\"hayalet\":1}," +
                          "\"bag\":{\"0:0\":2,\"9:9\":5,\"x\":1,\"0:1\":-4}}";
            GameState s;
            Assert.True(SaveSystem.TryDeserialize(json, cfg, out s));
            Assert.Equal(0, s.Money);
            Assert.Equal(7, s.TotalBlocks);
            Assert.Equal(2, s.LayerIndex);
            Assert.Equal(1, s.PickaxeTier);
            Assert.Equal(0, s.BlockHp);
            Assert.Equal(3, s.GetLevel("guc"));
            Assert.False(s.UpgradeLevels.ContainsKey("yok"));
            Assert.False(s.UpgradeLevels.ContainsKey("krit"));
            Assert.Equal(10.0, s.MachineTimers["dynamite"]);
            Assert.False(s.MachineTimers.ContainsKey("hayalet"));
            Assert.Single(s.Bag);
            Assert.Equal(2, s.BagCount);
        }

        [Fact]
        public void TruncatedSaveFromHalfWriteIsHandled()
        {
            var cfg = T.Small();
            string json = SaveSystem.Serialize(Played());
            for (int cut = 1; cut < json.Length; cut += 7)
            {
                GameState s;
                SaveSystem.TryDeserialize(json.Substring(0, cut), cfg, out s); // istisna yok
                Assert.NotNull(s);
            }
        }

        [Fact]
        public void UnknownFutureFieldsAreIgnored()
        {
            var cfg = T.Small();
            GameState s;
            Assert.True(SaveSystem.TryDeserialize("{\"version\":99,\"money\":7,\"yeniAlan\":{\"a\":[1]}}", cfg, out s));
            Assert.Equal(7, s.Money);
        }
    }

    public class ShopCategoryTests
    {
        [Fact]
        public void CategoriesGroupEntriesAndOpenOnFirst()
        {
            var s = T.Session(0.5, new GameState { Money = 0 });
            s.ToggleShop();
            var v = s.GetShopView();
            Assert.Equal("Kazma", v.CategoryName);
            Assert.Equal(1, v.Pos); Assert.Equal(1, v.Total);
            Assert.Equal(4, v.CategoryCount);
            Assert.All(s.GetShopEntries(), e => Assert.InRange(e.Category, 0, 3));
            Assert.Equal(0, s.GetShopEntries().First(e => e.Id == "pickaxe").Category);
            Assert.Equal(1, s.GetShopEntries().First(e => e.Id == "guc").Category);
            Assert.Equal(2, s.GetShopEntries().First(e => e.Id == "canta").Category);
            Assert.Equal(3, s.GetShopEntries().First(e => e.Id == "dynamite").Category);
        }

        [Fact]
        public void CategorySwitchAnnouncesNameAndWraps()
        {
            var log = new List<GameEvent>();
            var s = T.Session(0.5, new GameState(), null, log);
            s.ToggleShop();
            s.ShopNextCategory();
            Assert.StartsWith("Kategori Vuruş, 3 öğe.", log.Last().Speech);
            s.ShopNextCategory(); Assert.Equal("Çanta", s.GetShopView().CategoryName);
            s.ShopNextCategory(); Assert.Equal("Makineler", s.GetShopView().CategoryName);
            s.ShopNextCategory(); Assert.Equal("Kazma", s.GetShopView().CategoryName); // başa sarar
            s.ShopPrevCategory(); Assert.Equal("Makineler", s.GetShopView().CategoryName);
        }

        [Fact]
        public void ItemNavigationStaysInsideCategory()
        {
            var s = T.Session(0.5, new GameState());
            s.ToggleShop(); s.ShopNextCategory(); // Vuruş: guc, krit, sans
            for (int i = 0; i < 7; i++) { s.ShopNext(); Assert.Equal("Vuruş", s.GetShopView().CategoryName); }
            Assert.Equal(3, s.GetShopView().Total);
        }

        [Fact]
        public void ShopReadIncludesPosition()
        {
            var s = T.Session(0.5, new GameState());
            s.ToggleShop(); s.ShopNextCategory(); s.ShopNext();
            Assert.EndsWith("Vuruş, sıra 2 / 3.", s.ReadShopItem());
        }

        [Fact]
        public void EmptyCategoriesAreSkipped()
        {
            var st = new GameState { Money = 100000 };
            st.PickaxeTier = 1; // Kazma kategorisi boşalır (Small: 2 kazma)
            var s = T.Session(0.5, st);
            s.ToggleShop();
            Assert.Equal("Vuruş", s.GetShopView().CategoryName);
            Assert.Equal(3, s.GetShopView().CategoryCount);
        }

        [Fact]
        public void NavigationWhenClosedIsErrorForCategoriesToo()
        {
            var log = new List<GameEvent>();
            var s = T.Session(0.5, null, null, log);
            s.ShopNextCategory(); s.ShopPrevCategory();
            Assert.All(log, e => Assert.Equal(EventKind.Error, e.Kind));
        }
    }
}
