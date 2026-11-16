using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Maden.Core;

namespace Maden.Sim
{
    /// <summary>
    /// Sanal oyuncu: saniyede ortalama 3 dokunuş, çanta dolunca satar (1 sn kayıp),
    /// parası yettiğinde (yaklaşık) geri ödeme süresi en kısa yükseltmeyi alır.
    /// Core'u (GameSession.Tick, 1/3 sn adımla) kullanır.
    /// </summary>
    public static class Program
    {
        const double Dt = 1.0 / 3.0;
        const double TapsPerSec = 3.0;
        const double SellPauseSec = 1.0;
        const double MaxWaitForTarget = 25.0;
        const double MaxIdleWithMoney = 100.0; // elinde alınabilir bir şey varken en fazla bu kadar bekler
        const double MaxHours = 40;

        public class PurchaseGap { public double Start, End; public string Item; public int Layer; public double Gap { get { return End - Start; } } }

        public class Report
        {
            public bool Finished;
            public double FinishSeconds;
            public double[] LayerArrival;
            public double[] LayerIncome;
            public PurchaseGap Longest;
            public List<PurchaseGap> AllGaps = new List<PurchaseGap>();
            public int Purchases;
            public long TotalEarned;
            public List<string> Log = new List<string>();
        }

        public static int Main(string[] args)
        {
            string path = null;
            int seeds = 3;
            bool verbose = false;
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == "--seeds" && i + 1 < args.Length) seeds = int.Parse(args[++i]);
                else if (args[i] == "-v") verbose = true;
                else path = args[i];
            }
            path = path ?? FindEconomy();
            var cfg = EconomyConfig.LoadFromFile(path);
            Console.WriteLine("economy: " + path);
            double worstGap = 0;
            double minH = double.MaxValue, maxH = 0;
            bool allFinished = true;
            for (int s = 1; s <= seeds; s++)
            {
                var r = Run(cfg, s);
                Print(cfg, r, s, verbose);
                if (!r.Finished) allFinished = false;
                double h = r.FinishSeconds / 3600.0;
                minH = Math.Min(minH, h); maxH = Math.Max(maxH, h);
                if (r.Longest != null) worstGap = Math.Max(worstGap, r.Longest.Gap);
            }
            bool ok = allFinished && minH >= 15 && maxH <= 20 && worstGap <= 300;
            Console.WriteLine();
            Console.WriteLine("ÖZET: bitiş {0:F2}-{1:F2} saat, en uzun bekleme {2:F0} sn -> KABUL: {3}",
                minH, maxH, worstGap, ok ? "EVET" : "HAYIR");
            return ok ? 0 : 1;
        }

        static string FindEconomy()
        {
            string dir = AppContext.BaseDirectory;
            for (int i = 0; i < 8 && dir != null; i++)
            {
                string p = Path.Combine(dir, "Assets", "StreamingAssets", "economy.json");
                if (File.Exists(p)) return p;
                dir = Path.GetDirectoryName(dir);
            }
            return "Assets/StreamingAssets/economy.json";
        }

        static void Print(EconomyConfig cfg, Report r, int seed, bool verbose)
        {
            Console.WriteLine();
            Console.WriteLine("=== Tohum {0} ===", seed);
            Console.WriteLine(r.Finished ? "Bitiş süresi: {0:F2} saat" : "BİTMEDİ (sınır {0:F0} saat)", r.Finished ? r.FinishSeconds / 3600.0 : MaxHours);
            Console.WriteLine("Katman varış zamanları:");
            for (int i = 0; i < cfg.Layers.Count; i++)
                Console.WriteLine("  {0,2} {1,-16} {2}  (gelir ~{3}/sn)", i + 1, cfg.Layers[i].Name,
                    double.IsNaN(r.LayerArrival[i]) ? "-" : (r.LayerArrival[i] / 3600.0).ToString("F2", CultureInfo.InvariantCulture) + " sa",
                    NumberFormat.Short((long)r.LayerIncome[i]));
            if (Environment.GetEnvironmentVariable("SIM_RAW") == "1")
                for (int i = 0; i < cfg.Layers.Count; i++)
                    Console.WriteLine("RAW {0} {1} {2}", i, r.LayerArrival[i].ToString("R", CultureInfo.InvariantCulture), r.LayerIncome[i].ToString("R", CultureInfo.InvariantCulture));
            Console.WriteLine("Satın alma sayısı: {0}, toplam kazanç: {1}", r.Purchases, NumberFormat.Short(r.TotalEarned));
            if (r.Longest != null)
                Console.WriteLine("En uzun bekleme: {0:F0} sn ({1:F1} dk) -> '{2}' alınmadan önce, katman {3} ({4}), t={5:F2} sa",
                    r.Longest.Gap, r.Longest.Gap / 60, r.Longest.Item, r.Longest.Layer + 1, cfg.Layers[r.Longest.Layer].Name, r.Longest.End / 3600.0);
            r.AllGaps.Sort((a, b) => b.Gap.CompareTo(a.Gap));
            Console.WriteLine("En uzun 5 bekleme:");
            for (int i = 0; i < Math.Min(5, r.AllGaps.Count); i++)
                Console.WriteLine("  {0:F0} sn  '{1}' (katman {2}, t={3:F2} sa)", r.AllGaps[i].Gap, r.AllGaps[i].Item, r.AllGaps[i].Layer + 1, r.AllGaps[i].End / 3600.0);
            if (verbose) foreach (var l in r.Log) Console.WriteLine(l);
        }

        // ---------------- Analitik gelir tahmini (karar için) ----------------
        static double Rate(GameSession s)
        {
            var cfg = s.Config;
            var layer = s.CurrentLayer;
            double hp = layer.BlockHp;
            double tapBps = TapsPerSec * s.TapDamage * (1 + s.CritChance * (cfg.CritMultiplier - 1)) / hp;
            double machBps = 0;
            foreach (var kv in s.State.MachineTimers)
            {
                var m = cfg.FindMachine(kv.Key);
                machBps += s.MachinePower(m) / s.MachineInterval(m) / hp;
            }
            double rb = s.RareBonus, wsum = 0, vsum = 0;
            foreach (var it in layer.Items)
            {
                double w = GameSession.ItemWeight(it, rb);
                wsum += w; vsum += w * it.Value;
            }
            double ev = vsum / wsum * s.SellMultiplier;
            double bps = tapBps + machBps;
            double tapEff = tapBps;
            if (!s.AutoSell)
            {
                double loss = Math.Min(0.7, (bps / s.BagCapacity) * SellPauseSec);
                tapEff = tapBps * (1 - loss);
            }
            return (tapEff + machBps) * ev;
        }

        static bool SellRequested;

        static long BagValue(GameSession s)
        {
            double total = 0;
            foreach (var kv in s.State.Bag)
            {
                var parts = kv.Key.Split(':');
                total += s.Config.Layers[int.Parse(parts[0])].Items[int.Parse(parts[1])].Value * kv.Value;
            }
            return (long)(total * s.SellMultiplier);
        }

        static void Apply(GameSession s, ShopEntry e, int dir)
        {
            var st = s.State;
            switch (e.Kind)
            {
                case ShopKind.Pickaxe: st.PickaxeTier += dir; break;
                case ShopKind.Machine:
                    if (dir > 0) st.MachineTimers[e.Id] = 0; else st.MachineTimers.Remove(e.Id);
                    break;
                default: st.UpgradeLevels[e.Id] = st.GetLevel(e.Id) + dir; break;
            }
        }

        public static Report Run(EconomyConfig cfg, int seed)
        {
            var state = new GameState();
            var rng = new SystemRandom(seed);
            var s = new GameSession(cfg, state, rng);
            var rep = new Report { LayerArrival = new double[cfg.Layers.Count], LayerIncome = new double[cfg.Layers.Count] };
            var earnedAt = new long[cfg.Layers.Count];
            for (int i = 0; i < rep.LayerArrival.Length; i++) rep.LayerArrival[i] = double.NaN;
            rep.LayerArrival[0] = 0;

            double t = 0, lastPurchase = 0, nextDecision = 0;
            int pause = 0;
            double tapAcc = 0;
            int steps = 0;
            double dbgFrom = double.Parse(Environment.GetEnvironmentVariable("SIM_DBG_FROM") ?? "0"), dbgTo = double.Parse(Environment.GetEnvironmentVariable("SIM_DBG_TO") ?? "0");
            while (!state.Finished && t < MaxHours * 3600)
            {
                steps++;
                t += Dt;
                s.Tick(Dt);
                if (pause > 0) pause--;
                else
                {
                    tapAcc += TapsPerSec * Dt;
                    while (tapAcc >= 1) { tapAcc -= 1; s.Tap(); }
                }
                if (!s.AutoSell && state.BagCount >= s.BagCapacity)
                {
                    s.SellAll();
                    pause = (int)Math.Round(SellPauseSec / Dt);
                }
                if (double.IsNaN(rep.LayerArrival[state.LayerIndex])) { rep.LayerArrival[state.LayerIndex] = t; earnedAt[state.LayerIndex] = state.TotalEarned; }
                for (int li = 0; li <= state.LayerIndex; li++)
                    if (rep.LayerIncome[li] == 0 && !double.IsNaN(rep.LayerArrival[li]) && t >= rep.LayerArrival[li] + 600)
                        rep.LayerIncome[li] = (state.TotalEarned - earnedAt[li]) / 600.0;
                if (dbgFrom > 0 && t >= dbgFrom && t <= dbgTo && steps % 90 == 0)
                {
                    var sl = s.GetShopEntries(); long cheapest = long.MaxValue; foreach (var e0 in sl) cheapest = Math.Min(cheapest, e0.Cost);
                    Console.WriteLine("DBG t={0:F0} money={1} layer={2} bag={3}/{4} rate={5:F1} cheapest={6} blocks={7} dmg={8:F1} hp={9}", t, state.Money, state.LayerIndex + 1, state.BagCount, s.BagCapacity, Rate(s), cheapest, state.TotalBlocks, s.TapDamage, s.CurrentLayer.BlockHp);
                }
                if (t >= nextDecision)
                {
                    nextDecision = t + 1.0;
                    // Alınabilecek bir şey varsa (çanta satılırsa) önce sat.
                    if (!s.AutoSell && state.BagCount > 0 && pause == 0)
                    {
                        long cheapest = long.MaxValue;
                        foreach (var e0 in s.GetShopEntries()) cheapest = Math.Min(cheapest, e0.Cost);
                        if (state.Money < cheapest && state.Money + BagValue(s) >= cheapest)
                        {
                            s.SellAll();
                            pause = (int)Math.Round(SellPauseSec / Dt);
                        }
                    }
                    var bought = Decide(s, rep, t, ref lastPurchase);
                    if (bought) nextDecision = t;
                    if (SellRequested)
                    {
                        SellRequested = false;
                        if (pause == 0) { s.SellAll(); pause = (int)Math.Round(SellPauseSec / Dt); nextDecision = t + 0.5; }
                    }
                }
            }
            rep.Finished = state.Finished;
            rep.FinishSeconds = t;
            rep.TotalEarned = state.TotalEarned;
            foreach (var g in rep.AllGaps) if (rep.Longest == null || g.Gap > rep.Longest.Gap) rep.Longest = g;
            return rep;
        }

        static bool Decide(GameSession s, Report rep, double t, ref double lastPurchase)
        {
            var list = s.GetShopEntries();
            if (list.Count == 0) return false;
            double baseRate = Rate(s);
            ShopEntry target = null, bestAff = null;
            double targetPb = double.MaxValue, affPb = double.MaxValue;
            foreach (var e in list)
            {
                if (e.Cost > s.State.Money + baseRate * 600 && target != null) continue; // çok uzak adayları atla
                Apply(s, e, +1);
                double gain = Rate(s) - baseRate;
                Apply(s, e, -1);
                if (gain <= 1e-9) continue;
                double pb = e.Cost / gain;
                if (pb < targetPb) { targetPb = pb; target = e; }
                if (e.Cost <= s.State.Money && pb < affPb) { affPb = pb; bestAff = e; }
            }
            if (target == null)
            {
                // Hiçbiri gelir artırmıyor: en ucuzunu al (ör. ilerleme için)
                foreach (var e in list) if (target == null || e.Cost < target.Cost) target = e;
                bestAff = target.Cost <= s.State.Money ? target : null;
                targetPb = affPb = 1;
            }
            ShopEntry pick = null;
            if (target.Cost <= s.State.Money) pick = target;
            else
            {
                double wait = (target.Cost - s.State.Money) / Math.Max(1e-9, baseRate);
                if (bestAff != null && (wait > MaxWaitForTarget || affPb <= 2 * targetPb)) pick = bestAff;
            }
            if (pick == null)
            {
                if (!s.AutoSell && s.State.BagCount > 0 && s.State.Money + BagValue(s) >= target.Cost) SellRequested = true;
                return false;
            }
            int layer = s.State.LayerIndex;
            if (!s.Buy(pick)) return false;
            rep.Purchases++;
            rep.AllGaps.Add(new PurchaseGap { Start = lastPurchase, End = t, Item = pick.Name + " (" + (pick.Level + 1) + ")", Layer = layer });
            lastPurchase = t;
            rep.Log.Add(string.Format(CultureInfo.InvariantCulture, "  t={0,7:F0}s L{1} {2} ({3} lira)", t, layer + 1, pick.Name, pick.Cost));
            return true;
        }
    }
}
