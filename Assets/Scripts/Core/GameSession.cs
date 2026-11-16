using System;
using System.Collections.Generic;
using System.Text;

namespace Maden.Core
{
    public enum ShopKind { Pickaxe, Upgrade, Machine }

    public sealed class ShopEntry
    {
        public ShopKind Kind;
        public string Id;
        public string Name;
        public string Desc;
        public long Cost;
        public int Level;
        public int MaxLevel;
    }

    public sealed class GameSession
    {
        private const int MaxBlocksPerCall = 200000;
        private const long MaxMoney = 4000000000000000000L; // taşmaya karşı üst sınır

        private readonly EconomyConfig cfg;
        private readonly IRandom rng;
        private bool silent;
        private bool shopOpen;
        private int shopIndex;
        private int drillBlocks;
        private long drillValue;
        private double drillClock;

        public event Action<GameEvent> OnEvent;
        public GameState State { get; private set; }
        public EconomyConfig Config { get { return cfg; } }
        public bool ShopOpen { get { return shopOpen; } }

        public GameSession(EconomyConfig cfg, GameState state, IRandom rng)
        {
            if (cfg == null) throw new ArgumentNullException("cfg");
            this.cfg = cfg;
            this.rng = rng ?? new SystemRandom();
            State = state ?? new GameState();
            State.Sanitize(cfg);
        }

        // ------------------------------------------------------------------
        // Türetilmiş değerler
        // ------------------------------------------------------------------
        private double KindBonus(string kind)
        {
            double sum = 0;
            foreach (var u in cfg.Upgrades)
                if (u.Kind == kind) sum += u.EffectPerLevel * State.GetLevel(u.Id);
            return sum;
        }

        /// <summary>Çarpımsal etki: her seviye (1 + effectPerLevel) ile çarpılır.</summary>
        private double KindMult(string kind)
        {
            double m = 1.0;
            foreach (var u in cfg.Upgrades)
            {
                if (u.Kind != kind) continue;
                int lv = State.GetLevel(u.Id);
                if (lv > 0) m *= Math.Pow(1.0 + u.EffectPerLevel, lv);
            }
            return m;
        }

        public double TapDamage
        {
            get { return cfg.Pickaxes[State.PickaxeTier].Power * KindMult("power"); }
        }

        public double CritChance
        {
            get { return Math.Min(cfg.CritCap, cfg.CritBase + KindBonus("crit")); }
        }

        public double RareBonus { get { return KindBonus("rareChance"); } }

        public int BagCapacity
        {
            get { return Math.Max(1, cfg.BagBase + (int)Math.Round(KindBonus("bag"))); }
        }

        public bool AutoSell { get { return KindBonus("autosell") > 0; } }

        public double SellMultiplier
        {
            get { return KindMult("sellBonus") * (1.0 + cfg.PrestigeBonusPerRun * State.PrestigeCount); }
        }

        public double MachineInterval(MachineDef m)
        {
            return m.IntervalSec / KindMult(m.Id + "Speed");
        }

        public double MachinePower(MachineDef m)
        {
            return m.Power * KindMult(m.Id + "Power");
        }

        public LayerDef CurrentLayer { get { return cfg.Layers[State.LayerIndex]; } }

        public static long UpgradeCost(UpgradeDef u, int level)
        {
            double c = Math.Ceiling(u.BaseCost * Math.Pow(u.CostMult, level));
            return c > 9e15 ? 9000000000000000L : (long)c;
        }

        // ------------------------------------------------------------------
        // Olay yardımcıları
        // ------------------------------------------------------------------
        private void Emit(EventKind kind, string speech, Rarity rarity = Rarity.Common, long amount = 0, int? layer = null)
        {
            if (silent) return;
            var h = OnEvent;
            if (h != null) h(new GameEvent(kind, speech, layer ?? State.LayerIndex, rarity, amount));
        }

        private sealed class FoundItem { public ItemDef Def; public int Layer; public long Value; }

        private sealed class Tally
        {
            public int Blocks;
            public long Value;
            public bool BagFull;
            public List<FoundItem> Items = new List<FoundItem>();
        }

        // ------------------------------------------------------------------
        // Kazma
        // ------------------------------------------------------------------
        public void Tap()
        {
            if (State.Finished) { Emit(EventKind.Error, "Kadim hazineyi zaten buldun. Yeni oyun artı başlatabilirsin."); return; }
            State.TotalTaps++;
            bool crit = rng.NextDouble() < CritChance;
            double dmg = TapDamage * (crit ? cfg.CritMultiplier : 1.0);
            Emit(EventKind.Hit, crit ? "Kritik vuruş!" : "", crit ? Rarity.Uncommon : Rarity.Common, (long)Math.Round(dmg));
            var t = new Tally();
            DealDamage(dmg, t);
            ReportTally(t, true);
        }

        private bool EnsureBagRoom(Tally t)
        {
            if (State.BagCount < BagCapacity) return true;
            if (AutoSell) { SellInternal(true); return true; }
            t.BagFull = true;
            return false;
        }

        private void DealDamage(double dmg, Tally t)
        {
            int guard = 0;
            while (dmg > 0 && !State.Finished && guard++ < MaxBlocksPerCall)
            {
                if (!EnsureBagRoom(t)) break;
                if (State.BlockHp <= 0) State.BlockHp = CurrentLayer.BlockHp;
                if (dmg >= State.BlockHp)
                {
                    dmg -= State.BlockHp;
                    State.BlockHp = 0;
                    BreakBlock(t);
                }
                else
                {
                    State.BlockHp -= dmg;
                    dmg = 0;
                }
            }
            if (!State.Finished && State.BagCount >= BagCapacity && !AutoSell) t.BagFull = true;
        }

        private void BreakBlock(Tally t)
        {
            var layer = CurrentLayer;
            int li = State.LayerIndex;
            int idx = PickItem(layer);
            var def = layer.Items[idx];
            State.AddToBag(GameState.BagKey(li, idx), 1);
            long val = (long)Math.Round(def.Value * SellMultiplier, MidpointRounding.AwayFromZero);
            State.TotalItems++;
            if (def.Rarity == Rarity.Rare) State.RareCount++;
            State.TotalBlocks++;
            t.Blocks++;
            t.Value += val;
            t.Items.Add(new FoundItem { Def = def, Layer = li, Value = val });

            if (State.TotalBlocks >= cfg.TotalDepth)
            {
                State.TotalBlocks = cfg.TotalDepth;
                State.Finished = true;
                State.LayerIndex = cfg.Layers.Count - 1;
                Emit(EventKind.AncientTreasureFound,
                    cfg.TreasureName + " bulundu! Tebrikler, madenin en dibine ulaştın ve oyunu bitirdin. " +
                    "Yeni oyun artı ile kalıcı çarpanla yeniden başlayabilirsin.",
                    Rarity.Rare, State.Money);
                return;
            }
            int nl = cfg.LayerForBlocks(State.TotalBlocks);
            if (nl > State.LayerIndex)
            {
                State.LayerIndex = nl;
                Emit(EventKind.LayerChanged,
                    "Yeni katman: " + cfg.Layers[nl].Name + ". Derinlik " + NumberFormat.Short(State.TotalBlocks) + " blok.",
                    Rarity.Common, State.TotalBlocks);
            }
        }

        private int PickItem(LayerDef layer)
        {
            double rb = RareBonus;
            double total = 0;
            for (int i = 0; i < layer.Items.Count; i++) total += ItemWeight(layer.Items[i], rb);
            double r = rng.NextDouble() * total;
            for (int i = 0; i < layer.Items.Count; i++)
            {
                r -= ItemWeight(layer.Items[i], rb);
                if (r < 0) return i;
            }
            return layer.Items.Count - 1;
        }

        public static double ItemWeight(ItemDef it, double rareBonus)
        {
            if (it.Rarity == Rarity.Rare) return it.Weight * (1.0 + rareBonus);
            if (it.Rarity == Rarity.Uncommon) return it.Weight * (1.0 + rareBonus * 0.5);
            return it.Weight;
        }

        private void ReportTally(Tally t, bool reportCommons)
        {
            if (silent) return;
            int commons = 0;
            foreach (var f in t.Items) if (f.Def.Rarity != Rarity.Rare) commons++;
            bool summarize = commons > 3 || !reportCommons;
            long summaryValue = 0; int summaryCount = 0;
            foreach (var f in t.Items)
            {
                if (f.Def.Rarity == Rarity.Rare)
                {
                    Emit(EventKind.RareFound, "Nadir buluş! " + f.Def.Name + ", " + NumberFormat.Lira(f.Value) + " değerinde.",
                        Rarity.Rare, f.Value, f.Layer);
                }
                else if (reportCommons && !summarize)
                {
                    Emit(EventKind.ItemFound, f.Def.Name + ", " + NumberFormat.Lira(f.Value) + ".", f.Def.Rarity, f.Value, f.Layer);
                }
                else { summaryCount++; summaryValue += f.Value; }
            }
            if (reportCommons && summarize && summaryCount > 0)
                Emit(EventKind.ItemFound, summaryCount + " eşya buldun, toplam " + NumberFormat.Lira(summaryValue) + ".", Rarity.Common, summaryValue);
            if (t.BagFull)
                Emit(EventKind.BagFull, "Çanta dolu. Hepsini sat.", Rarity.Common, BagCapacity);
        }

        // ------------------------------------------------------------------
        // Satış
        // ------------------------------------------------------------------
        public void SellAll()
        {
            if (State.BagCount == 0) { Emit(EventKind.Error, "Çantan boş."); return; }
            SellInternal(false);
        }

        private long SellInternal(bool auto)
        {
            double mult = SellMultiplier;
            double total = 0;
            int count = 0;
            foreach (var kv in State.Bag)
            {
                var parts = kv.Key.Split(':');
                var def = cfg.Layers[int.Parse(parts[0])].Items[int.Parse(parts[1])];
                total += def.Value * kv.Value;
                count += kv.Value;
            }
            long money = (long)Math.Round(total * mult, MidpointRounding.AwayFromZero);
            State.ClearBag();
            State.Money = Math.Min(State.Money + money, MaxMoney);
            State.TotalEarned = Math.Min(State.TotalEarned + money, MaxMoney);
            Emit(EventKind.Sold,
                (auto ? "Otomatik satış: " : "") + count + " eşya satıldı, " + NumberFormat.Lira(money) + " kazandın. Toplam " + NumberFormat.Lira(State.Money) + ".",
                Rarity.Common, money);
            return money;
        }


        // ------------------------------------------------------------------
        // Dükkân
        // ------------------------------------------------------------------
        public List<ShopEntry> GetShopEntries()
        {
            var list = new List<ShopEntry>();
            int layer = State.LayerIndex;
            if (State.PickaxeTier + 1 < cfg.Pickaxes.Count)
            {
                var p = cfg.Pickaxes[State.PickaxeTier + 1];
                if (p.MinLayer <= layer)
                    list.Add(new ShopEntry
                    {
                        Kind = ShopKind.Pickaxe, Id = "pickaxe", Name = p.Name, Cost = p.Cost,
                        Desc = "Vuruş gücü " + NumberFormat.Dec(p.Power), Level = State.PickaxeTier + 1, MaxLevel = cfg.Pickaxes.Count - 1
                    });
            }
            foreach (var u in cfg.Upgrades)
            {
                int lv = State.GetLevel(u.Id);
                if (lv >= u.MaxLevel || u.MinLayer > layer) continue;
                if (IsMachineKind(u.Kind) && !State.OwnsMachine(MachineOfKind(u.Kind))) continue;
                list.Add(new ShopEntry
                {
                    Kind = ShopKind.Upgrade, Id = u.Id, Name = u.Name, Desc = u.Desc,
                    Cost = UpgradeCost(u, lv), Level = lv, MaxLevel = u.MaxLevel
                });
            }
            foreach (var m in cfg.Machines)
            {
                if (State.OwnsMachine(m.Id) || m.MinLayer > layer) continue;
                list.Add(new ShopEntry
                {
                    Kind = ShopKind.Machine, Id = m.Id, Name = m.Name,
                    Desc = (m.Kind == "drill" ? "Matkap" : "Dinamit") + ", " + NumberFormat.Dec(m.IntervalSec) + " saniyede bir, güç " + NumberFormat.Dec(m.Power),
                    Cost = m.UnlockCost, Level = 0, MaxLevel = 1
                });
            }
            return list;
        }

        private static bool IsMachineKind(string kind)
        {
            return MachineOfKind(kind) != null;
        }

        private static string MachineOfKind(string kind)
        {
            if (kind.Length > 5 && kind.EndsWith("Power", StringComparison.Ordinal)) return kind.Substring(0, kind.Length - 5);
            if (kind.Length > 5 && kind.EndsWith("Speed", StringComparison.Ordinal)) return kind.Substring(0, kind.Length - 5);
            return null;
        }

        private ShopEntry CurrentEntry()
        {
            var l = GetShopEntries();
            if (l.Count == 0) return null;
            if (shopIndex >= l.Count) shopIndex = l.Count - 1;
            if (shopIndex < 0) shopIndex = 0;
            return l[shopIndex];
        }

        public void ToggleShop()
        {
            shopOpen = !shopOpen;
            if (shopOpen)
            {
                shopIndex = 0;
                Emit(EventKind.ShopOpened, "Dükkân açıldı. " + ReadShopItem());
            }
            else Emit(EventKind.ShopClosed, "Dükkân kapandı.");
        }

        public void ShopNext() { MoveShop(1); }
        public void ShopPrev() { MoveShop(-1); }

        private void MoveShop(int d)
        {
            if (!shopOpen) { Emit(EventKind.Error, "Dükkân kapalı."); return; }
            var l = GetShopEntries();
            if (l.Count > 0) shopIndex = ((shopIndex + d) % l.Count + l.Count) % l.Count;
            Emit(EventKind.ShopItemChanged, ReadShopItem());
        }

        public string ReadShopItem()
        {
            var e = CurrentEntry();
            if (e == null) return "Dükkânda alınacak bir şey kalmadı.";
            var sb = new StringBuilder();
            sb.Append(e.Name).Append(". ").Append(e.Desc);
            if (e.Kind == ShopKind.Upgrade) sb.Append(". Seviye ").Append(e.Level).Append(" / ").Append(e.MaxLevel);
            sb.Append(". Fiyat ").Append(NumberFormat.Lira(e.Cost)).Append('.');
            if (State.Money < e.Cost) sb.Append(" Yetmiyor, ").Append(NumberFormat.Lira(e.Cost - State.Money)).Append(" eksik.");
            return sb.ToString();
        }

        public void ShopBuy()
        {
            if (!shopOpen) { Emit(EventKind.Error, "Dükkân kapalı."); return; }
            var e = CurrentEntry();
            if (e == null) { Emit(EventKind.Error, "Dükkânda alınacak bir şey kalmadı."); return; }
            Buy(e);
        }

        /// <summary>Dükkân açık olmasa da satın alma (Sim ve testler için).</summary>
        public bool Buy(ShopEntry e)
        {
            if (State.Money < e.Cost)
            {
                Emit(EventKind.NotEnoughMoney, "Yetersiz para. " + NumberFormat.Lira(e.Cost - State.Money) + " eksik.",
                    Rarity.Common, e.Cost - State.Money);
                return false;
            }
            State.Money -= e.Cost;
            string msg;
            switch (e.Kind)
            {
                case ShopKind.Pickaxe:
                    State.PickaxeTier++;
                    msg = e.Name + " alındı. Vuruş gücü " + NumberFormat.Dec(TapDamage) + ".";
                    break;
                case ShopKind.Machine:
                    State.MachineTimers[e.Id] = 0;
                    msg = e.Name + " kuruldu.";
                    break;
                default:
                    State.UpgradeLevels[e.Id] = State.GetLevel(e.Id) + 1;
                    msg = e.Name + " seviye " + State.GetLevel(e.Id) + " oldu.";
                    break;
            }
            Emit(EventKind.UpgradeBought, msg, Rarity.Common, e.Cost);
            if (shopOpen)
            {
                var next = GetShopEntries();
                if (shopIndex >= next.Count) shopIndex = Math.Max(0, next.Count - 1);
            }
            return true;
        }

        // ------------------------------------------------------------------
        // Durum
        // ------------------------------------------------------------------
        public string ReadStatus()
        {
            var sb = new StringBuilder();
            sb.Append("Para ").Append(NumberFormat.Lira(State.Money)).Append(". ");
            if (State.Finished) sb.Append("Kadim hazineyi buldun, oyun bitti. ");
            else sb.Append("Derinlik ").Append(NumberFormat.Short(State.TotalBlocks)).Append(" blok, ").Append(CurrentLayer.Name).Append(" katmanı. ");
            sb.Append("Çanta ").Append(State.BagCount).Append(" / ").Append(BagCapacity).Append(". ");
            sb.Append("Kazma: ").Append(cfg.Pickaxes[State.PickaxeTier].Name).Append(", güç ").Append(NumberFormat.Dec(TapDamage)).Append(". ");
            int mc = State.MachineTimers.Count;
            sb.Append(mc == 0 ? "Makine yok." : mc + " makine çalışıyor.");
            if (State.PrestigeCount > 0) sb.Append(" Yeni oyun artı ").Append(State.PrestigeCount).Append(".");
            return sb.ToString();
        }

        // ------------------------------------------------------------------
        // Makineler / zaman
        // ------------------------------------------------------------------
        public void Tick(double dtSeconds)
        {
            if (dtSeconds <= 0 || double.IsNaN(dtSeconds)) return;
            State.PlaySeconds += dtSeconds;
            Advance(dtSeconds);
            if (!silent)
            {
                drillClock += dtSeconds;
                if (drillClock >= cfg.DrillReportSeconds)
                {
                    if (drillBlocks > 0)
                        Emit(EventKind.MachineDrill, "Matkaplar " + drillBlocks + " blok kazdı.", Rarity.Common, drillBlocks);
                    drillBlocks = 0; drillValue = 0; drillClock = 0;
                }
            }
        }

        private void Advance(double dt)
        {
            if (State.Finished) return;
            if (State.MachineTimers.Count == 0) { AutoSellIfFull(); return; }
            var keys = new List<string>(State.MachineTimers.Keys);
            foreach (var id in keys)
            {
                var m = cfg.FindMachine(id);
                if (m == null) continue;
                double interval = MachineInterval(m);
                double timer = State.MachineTimers[id] + dt;
                int fires = 0;
                while (timer >= interval && !State.Finished && fires < 100000)
                {
                    var t = new Tally();
                    if (State.BagCount >= BagCapacity && !AutoSell) { timer = interval; break; }
                    timer -= interval;
                    fires++;
                    DealDamage(MachinePower(m), t);
                    ReportMachine(m, t);
                    if (t.BagFull && !AutoSell) { timer = Math.Min(timer, interval); break; }
                }
                if (timer > interval) timer = interval;
                State.MachineTimers[id] = timer;
            }
            AutoSellIfFull();
        }

        private void AutoSellIfFull()
        {
            if (AutoSell && State.BagCount >= BagCapacity) SellInternal(true);
        }

        private void ReportMachine(MachineDef m, Tally t)
        {
            if (silent) return;
            if (m.Kind == "drill")
            {
                drillBlocks += t.Blocks; drillValue += t.Value;
                ReportTally(t, false);
                return;
            }
            Emit(EventKind.MachineBlast,
                m.Name + " patladı. " + t.Blocks + " blok kazıldı, " + NumberFormat.Lira(t.Value) + " değerinde eşya.",
                Rarity.Common, t.Blocks);
            ReportTally(t, false);
        }

        public OfflineResult ApplyOffline(double secondsAway)
        {
            var res = new OfflineResult { Speech = "" };
            if (double.IsNaN(secondsAway) || secondsAway < 0) secondsAway = 0;
            double secs = Math.Min(secondsAway, cfg.OfflineCapSeconds);
            res.SecondsCounted = secs;
            if (State.Finished || secs < 1) { res.Speech = secs < 1 ? "" : "Hoş geldin."; return res; }

            long moneyBefore = State.Money, earnedBefore = State.TotalEarned, blocksBefore = State.TotalBlocks, itemsBefore = State.TotalItems;
            int layerBefore = State.LayerIndex;
            bool wasSilent = silent;
            silent = true;
            try
            {
                double left = secs;
                while (left > 0 && !State.Finished)
                {
                    double step = Math.Min(1.0, left);
                    Advance(step);
                    left -= step;
                }
                State.PlaySeconds += 0; // çevrimdışı süre oyun süresine eklenmez
            }
            finally { silent = wasSilent; }

            res.Blocks = (int)(State.TotalBlocks - blocksBefore);
            res.Items = (int)(State.TotalItems - itemsBefore);
            res.Money = State.TotalEarned - earnedBefore;
            res.TreasureFound = State.Finished;

            var sb = new StringBuilder();
            sb.Append("Hoş geldin. ");
            int mins = (int)Math.Round(secs / 60.0);
            sb.Append(secondsAway > cfg.OfflineCapSeconds
                ? "Uzun süre yoktun, en fazla " + (int)Math.Round(cfg.OfflineCapSeconds / 3600.0) + " saatlik kazanç sayıldı. "
                : (mins >= 1 ? mins + " dakika yoktun. " : ""));
            if (State.MachineTimers.Count == 0) sb.Append("Makinen olmadığı için kazanç yok.");
            else if (res.Blocks == 0) sb.Append("Makineler bir şey kazamadı. Çanta dolu olabilir.");
            else
            {
                sb.Append("Makineler ").Append(NumberFormat.Short(res.Blocks)).Append(" blok kazdı. ");
                if (res.Money > 0) sb.Append(NumberFormat.Lira(res.Money)).Append(" kazandın. ");
                else sb.Append(res.Items).Append(" eşya çantada, satmayı unutma. ");
                if (State.LayerIndex > layerBefore) sb.Append("Yeni katman: ").Append(CurrentLayer.Name).Append(". ");
                if (res.TreasureFound) sb.Append(cfg.TreasureName).Append(" bulundu! Oyunu bitirdin. ");
            }
            res.Speech = sb.ToString().Trim();
            return res;
        }

        // ------------------------------------------------------------------
        // Yeni oyun artı
        // ------------------------------------------------------------------
        public void StartNewGamePlus()
        {
            if (!State.Finished)
            {
                Emit(EventKind.Error, "Yeni oyun artı için önce kadim hazineyi bulmalısın.");
                return;
            }
            int prestige = State.PrestigeCount + 1;
            long taps = State.TotalTaps, items = State.TotalItems, rare = State.RareCount, earned = State.TotalEarned;
            double play = State.PlaySeconds;
            var fresh = new GameState();
            fresh.PrestigeCount = prestige;
            fresh.TotalTaps = taps; fresh.TotalItems = items; fresh.RareCount = rare;
            fresh.TotalEarned = earned; fresh.PlaySeconds = play;
            State = fresh;
            shopOpen = false; shopIndex = 0;
            Emit(EventKind.LayerChanged,
                "Yeni oyun artı başladı. Kalıcı satış çarpanın " + NumberFormat.Dec(SellMultiplier) + " kat. Toprak katmanındasın.",
                Rarity.Common, prestige, 0);
        }
    }
}
