using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Kazi.Core;

namespace Kazi.Sim
{
    public sealed class Strategy
    {
        public double ActionsPerSec = 1.7;       // elle dokunuş/eylem hızı (≈10 kart/dk sıralı modda)
        public double BankrollFraction = 0.5;   // kart fiyatı ≤ bankroll × bu
        public double GateMoneyFactor = 2.5;     // Son Şans kartı için gereken bakiye / fiyat
        public double HorizonSeconds = 120;
        public double PaybackFactor = 1e9;      // bu süre içinde biriken gelirle ulaşılabilen kalemler adaydır
        public bool ManualAfterAuto = false;
        public double MaxHours = 40;
        public double Dt = 0.25;
    }

    public sealed class Mark { public double T; public string Label; public int Run; }

    public sealed class RunReport
    {
        public double StartT, EndT; public long Ab; public double FirstAutoT = -1;
        public double[] CatalogT = new double[8];
    }

    public sealed class SeedResult
    {
        public List<Mark> Marks = new List<Mark>();
        public List<RunReport> Runs = new List<RunReport>();
        public double FirstPrestigeT = -1, TreeDoneT = -1, MaxGap, MaxGapAt; public string MaxGapLabel = "";
        public double MaxGapFirstRun, MaxGapFirstRunAt; public string MaxGapFirstRunLabel = "";
        public double FirstAutoT = -1;
        public double EndT;
    }

    public static class Program
    {
        public static string FindData(string given)
        {
            if (!string.IsNullOrEmpty(given)) return given;
            string d = AppContext.BaseDirectory;
            for (int i = 0; i < 8 && d != null; i++)
            {
                string c = Path.Combine(d, "data");
                if (File.Exists(Path.Combine(c, "cards.json"))) return c;
                string c2 = Path.Combine(d, "kazi", "data");
                if (File.Exists(Path.Combine(c2, "cards.json"))) return c2;
                d = Path.GetDirectoryName(d);
            }
            throw new FileNotFoundException("data klasörü bulunamadı (--data verin).");
        }

        public static int Main(string[] args)
        {
            int only = 0; int seeds = 5; string data = null; var strat = new Strategy(); bool verbose = false, rtp = false;
            for (int i = 0; i < args.Length; i++)
            {
                switch (args[i])
                {
                    case "--seeds": seeds = int.Parse(args[++i]); break;
                    case "--data": data = args[++i]; break;
                    case "--hours": strat.MaxHours = double.Parse(args[++i], System.Globalization.CultureInfo.InvariantCulture); break;
                    case "--f": strat.BankrollFraction = double.Parse(args[++i], System.Globalization.CultureInfo.InvariantCulture); break;
                    case "--manual-after-auto": strat.ManualAfterAuto = true; break;
                    case "--verbose": verbose = true; trace = true; break;
                    case "--rtp": rtp = true; break;
                    case "--only": only = int.Parse(args[++i]); break;
                }
            }
            var cfg = GameConfig.LoadDirectory(FindData(data));
            if (rtp) { PrintRtp(cfg); }
            var results = new List<SeedResult>();
            for (int s = 1; s <= seeds; s++) if (only == 0 || only == s) results.Add(RunSeed(cfg, strat, (ulong)(s * 7919 + 13)));
            Report(results, verbose);
            return Accept(results) ? 0 : 1;
        }

        static void PrintRtp(GameConfig cfg)
        {
            Console.WriteLine("Kart RTP (şans 0):");
            foreach (var t in cfg.CardTypes)
                Console.WriteLine("  " + t.Id.PadRight(18) + " fiyat " + t.Price.ToString().PadLeft(10) + "  RTP " + Odds.Rtp(t, 0, cfg.Settings).ToString("0.000"));
        }

        // ---------- sanal oyuncu ----------
        sealed class Bot
        {
            public GameSession S; public GameConfig Cfg; public Strategy St; public SeedResult R;
            public double T; public double Budget; public double LastBuyCost = 10; public int Run;
            public double NextDecision, NextTrace; public double NextTypePick; public int Purchases;
            public RunReport Cur;
            public double lastMarkT;
        }

        static double Net(Stats st, CardTypeDef t, GameConfig cfg) { return Economy.ExpectedNet(cfg, st, t); }

        static CardTypeDef BestType(Bot b, Stats st, int maxCat, double bankroll, double money)
        {
            CardTypeDef best = null; double bestNet = double.NegativeInfinity;
            CardTypeDef cheapest = null;
            foreach (var t in b.Cfg.CardTypes)
            {
                if (t.Catalog > maxCat || t.IsGate) continue;
                long price = st.TicketPrice(t);
                if (cheapest == null || price < st.TicketPrice(cheapest)) cheapest = t;
                if (price > bankroll * b.St.BankrollFraction || price > money) continue;
                double n = Net(st, t, b.Cfg);
                if (n > bestNet) { bestNet = n; best = t; }
            }
            return best ?? cheapest;
        }

        static double Income(Bot b, Stats st, GameState state, double bankroll)
        {
            bool auto = st.AutoOwned && st.AutoBuyOwned && !b.St.ManualAfterAuto;
            if (auto)
            {
                int maxCat = st.AutoTypeLevel > 0 ? Math.Min(state.UnlockedCatalog, st.AutoTypeLevel) : 1;
                var t = BestType(b, st, state.UnlockedCatalog, double.MaxValue, double.MaxValue);
                if (t == null) return 0;
                double cpm = st.AutoCpm * st.Batch;
                return cpm / 60.0 * Net(st, t, b.Cfg);
            }
            else
            {
                var t = BestType(b, st, state.UnlockedCatalog, double.MaxValue, double.MaxValue);
                if (t == null) return 0;
                double actions = 1 + Math.Ceiling(t.Cells / (double)st.ScratchPower);
                double manual = b.St.ActionsPerSec / actions * Net(st, t, b.Cfg);
                if (st.AutoOwned && st.AutoBuyOwned) manual += st.AutoCpm * st.Batch / 60.0 * Net(st, BestType(b, st, state.UnlockedCatalog, double.MaxValue, double.MaxValue), b.Cfg);
                return manual;
            }
        }

        static readonly HashSet<string> Mandatory = new HashSet<string> { "oto_kaziyici", "oto_alim" };

        static void Mark_(Bot b, string label)
        {
            b.R.Marks.Add(new Mark { T = b.T, Label = label, Run = b.Run });
        }

        static bool trace;
        static SeedResult RunSeed(GameConfig cfg, Strategy strat, ulong seed)
        {
            var state = SaveSystem.NewGame(cfg); state.RngState = seed;
            var session = new GameSession(cfg, state);
            var bot = new Bot { S = session, Cfg = cfg, St = strat, R = new SeedResult() };
            bot.Cur = new RunReport { StartT = 0 }; bot.R.Runs.Add(bot.Cur);
            session.OnEvent += e =>
            {
                switch (e.Kind)
                {
                    case EventKind.UpgradeBought: case EventKind.GadgetBought: case EventKind.AutomationBought:
                        Mark_(bot, e.Symbol + " alındı");
                        if (e.Symbol == "oto_kaziyici" && bot.Cur.FirstAutoT < 0) bot.Cur.FirstAutoT = bot.T;
                        break;
                    case EventKind.CardResult: if (trace && e.Symbol.EndsWith("sonsans") || e.Symbol == "altin_bilet") Console.WriteLine("    gate sonuç " + H(bot.T) + " " + e.Speech + " para " + NumberFormat.Short(state.Money)); break;
                    case EventKind.NodeBought: Mark_(bot, "düğüm " + e.Symbol); break;
                    case EventKind.CatalogUnlocked: Mark_(bot, "katalog " + e.Amount + " açıldı"); bot.Cur.CatalogT[e.Amount] = bot.T - bot.Cur.StartT; break;
                }
            };
            // başlangıçta kaziyici zaten açıksa (kalıcı düğüm) kaydet
            double maxT = strat.MaxHours * 3600;
            bot.R.Marks.Add(new Mark { T = 0, Label = "başlangıç", Run = 0 });
            while (bot.T < maxT)
            {
                bot.T += strat.Dt;
                session.Tick(strat.Dt);
                bool autoPhase = session.Stats.AutoOwned && session.Stats.AutoBuyOwned && !strat.ManualAfterAuto;
                bool wantGate = WantGate(bot, out var gate);
                if (!autoPhase || wantGate || (session.State.Current != null && !session.State.Current.Done))
                {
                    bot.Budget += strat.ActionsPerSec * strat.Dt;
                    while (bot.Budget >= 1)
                    {
                        bot.Budget -= 1;
                        ManualStep(bot, wantGate ? gate : null);
                        wantGate = WantGate(bot, out gate);
                    }
                }
                else bot.Budget = 0;
                if (bot.T >= bot.NextTrace && trace)
                {
                    bot.NextTrace = bot.T + 600;
                    var sx = session.Stats;
                    Console.WriteLine("    [" + H(bot.T) + "] para=" + NumberFormat.Short(state.Money) + " K" + state.UnlockedCatalog + " luck=" + sx.Luck.ToString("0.#") + " pay=x" + sx.PayoutMult.ToString("0.##") + " cpm=" + sx.AutoCpm.ToString("0") + "x" + sx.Batch + " gelir/sn~" + NumberFormat.Short((long)Income(bot, sx, state, Math.Max(state.Money, bot.LastBuyCost))));
                }
                if (bot.T >= bot.NextDecision)
                {
                    bot.NextDecision = bot.T + 1.0;
                    Decide(bot);
                    if (bot.R.TreeDoneT >= 0) break;
                }
            }
            bot.R.EndT = bot.T;
            bot.Cur.EndT = bot.T;
            ComputeGaps(bot.R);
            return bot.R;
        }

        static bool WantGate(Bot b, out CardTypeDef gate)
        {
            gate = null;
            var s = b.S;
            gate = b.Cfg.GateOfCatalog(s.State.UnlockedCatalog);
            if (gate == null) return false;
            if (s.State.PrestigeOffer) return false;
            if (s.GateLevelsLeft(gate) > 0) return false;
            var cur = s.State.Current;
            if (cur != null && !cur.Done) return cur.Type == gate;
            return s.State.Money >= s.Stats.TicketPrice(gate) * b.St.GateMoneyFactor;
        }

        static void ManualStep(Bot b, CardTypeDef forceType)
        {
            var s = b.S; var st = s.State;
            var c = st.Current;
            if (c != null && !c.Done) { s.ScratchTap(); return; }
            CardTypeDef t = forceType;
            if (t == null)
            {
                t = BestType(b, s.Stats, st.UnlockedCatalog, st.Money, st.Money);
            }
            if (t == null) return;
            bool okb = s.BuyCard(t.Id);
            if (trace && forceType != null && b.T < 4000 && b.T % 60 < 1) Console.WriteLine("gate buy " + okb + " money " + st.Money + " cur " + (st.Current != null ? st.Current.Type.Id + st.Current.Done : "null"));
        }

        class Cand { public string Kind, Id; public long Cost; public double Score; public bool UsesAB; }

        static void Decide(Bot b)
        {
            var s = b.S; var st = s.State; var cfg = b.Cfg;
            // prestij
            if (st.PrestigeOffer)
            {
                var res = b.Cur; res.Ab = s.PrestigeGain(); res.EndT = b.T;
                if (b.R.FirstPrestigeT < 0) b.R.FirstPrestigeT = b.T;
                s.AcceptPrestige();
                Mark_(b, "PRESTİJ");
                b.Run++;
                b.Cur = new RunReport { StartT = b.T }; b.R.Runs.Add(b.Cur);
                b.LastBuyCost = 10;
                if (s.Stats.AutoOwned) b.Cur.FirstAutoT = b.T;
            }
            // düğümler
            while (true)
            {
                PrestigeNodeDef pick = null;
                foreach (var n in cfg.Nodes)
                    if (UpgradeSystem.NodeAvailable(cfg, st, n) && n.Cost <= st.AB && (pick == null || n.Cost < pick.Cost)) pick = n;
                if (pick == null) break;
                s.BuyNode(pick.Id);
                if (cfg.Nodes.All(n => st.NodeLevel(n.Id) > 0)) { b.R.TreeDoneT = b.T; return; }
            }
            if (st.PrestigeCount == 0 || true)
            {
                if (cfg.Nodes.All(n => st.NodeLevel(n.Id) > 0)) { b.R.TreeDoneT = b.T; return; }
            }
            // yükseltmeler
            double bankroll = Math.Max(st.Money, b.LastBuyCost);
            double inc0 = Income(b, s.Stats, st, bankroll);
            double gateNeed = 0;
            { var gc = cfg.GateOfCatalog(st.UnlockedCatalog); if (gc != null && !st.PrestigeOffer && s.GateLevelsLeft(gc) == 0) gateNeed = s.Stats.TicketPrice(gc) * b.St.GateMoneyFactor; }
            Cand best = null, cheapest = null;
            foreach (var u in cfg.Upgrades)
            {
                if (!UpgradeSystem.IsAvailable(cfg, st, u)) continue;
                long cost = UpgradeSystem.NextCost(cfg, st, s.Stats, u);
                double score;
                if (Mandatory.Contains(u.Id)) score = 1e9 / cost;
                else
                {
                    st.Upgrades[u.Id] = st.Level(u.Id) + 1; s.RecalcStats();
                    double inc1 = Income(b, s.Stats, st, bankroll);
                    int lv = st.Level(u.Id) - 1; if (lv <= 0) st.Upgrades.Remove(u.Id); else st.Upgrades[u.Id] = lv;
                    s.RecalcStats();
                    score = (inc1 - inc0) / cost;
                    // doğrudan geliri artırmayan yararlı kalemler için küçük değer
                    if (score <= 0 && (u.Kind == EffectKind.AutoQueue || u.Kind == EffectKind.Bag)) score = 1e-12 / cost * 0 + 1e-15;
                    if (u.Kind == EffectKind.Offline || u.Kind == EffectKind.Refund || u.Kind == EffectKind.Lens) score = 1e-16;
                }
                var cd = new Cand { Kind = "u", Id = u.Id, Cost = cost, Score = score };
                if (gateNeed > 0 && !Mandatory.Contains(u.Id) && score > 1e-14)
                {
                    // Son Şans kapısı yaklaşırken yalnızca kısa sürede kendini geri ödeyen yükseltmeler alınır
                    double dInc = score * cost;
                    double remaining = Math.Max(0, gateNeed - st.Money) / Math.Max(1e-9, inc0);
                    if (cost / Math.Max(1e-12, dInc) > Math.Max(30, remaining * b.St.PaybackFactor)) continue;
                }
                if (cheapest == null || cost < cheapest.Cost) cheapest = cd;
                if (cost <= st.Money + inc0 * b.St.HorizonSeconds || cost <= st.Money)
                    if (best == null || score > best.Score) best = cd;
            }
            Cand target = best ?? cheapest;
            // Son Şans için bakiye ayır
            var gateC = cfg.GateOfCatalog(st.UnlockedCatalog);
            double reserve = 0;
            if (gateC != null && !st.PrestigeOffer && s.GateLevelsLeft(gateC) == 0)
            {
                double need = s.Stats.TicketPrice(gateC) * b.St.GateMoneyFactor;
                if (need <= st.Money + inc0 * 180) reserve = need;
            }
            // oynanan en iyi kartın bedelini koruyacak küçük tampon
            double buffer = 0;
            {
                int mc = st.UnlockedCatalog;
                var bt = BestType(b, s.Stats, mc, double.MaxValue, double.MaxValue);
                if (bt != null) buffer = s.Stats.TicketPrice(bt) * 2.0;
            }
            reserve = Math.Max(reserve, buffer);
            if (target != null && target.Cost <= st.Money && st.Money - target.Cost >= reserve)
            {
                if (s.BuyUpgrade(target.Id)) { b.LastBuyCost = Math.Max(10, target.Cost); }
            }
            // otomatik tür
            if (false && b.T >= b.NextTypePick && s.Stats.AutoTypeLevel > 0)
            {
                b.NextTypePick = b.T + 5;
                int maxCat = Math.Min(st.UnlockedCatalog, s.Stats.AutoTypeLevel);
                var t = BestType(b, s.Stats, maxCat, st.Money, st.Money);
                if (t != null) s.SetAutoType(t.Id);
            }
        }

        static void ComputeGaps(SeedResult r)
        {
            var marks = r.Marks;
            r.FirstAutoT = r.Runs[0].FirstAutoT;
            for (int i = 1; i < marks.Count; i++)
            {
                double gap = marks[i].T - marks[i - 1].T;
                string lbl = marks[i - 1].Label + " -> " + marks[i].Label;
                if (gap > r.MaxGap) { r.MaxGap = gap; r.MaxGapAt = marks[i - 1].T; r.MaxGapLabel = lbl; }
                if (marks[i].Run == 0 && gap > r.MaxGapFirstRun) { r.MaxGapFirstRun = gap; r.MaxGapFirstRunAt = marks[i - 1].T; r.MaxGapFirstRunLabel = lbl; }
            }
        }

        static string H(double sec) { return sec < 0 ? "-" : (sec / 3600.0).ToString("0.00") + " sa"; }
        static string M(double sec) { return sec < 0 ? "-" : (sec / 60.0).ToString("0.0") + " dk"; }

        static void Report(List<SeedResult> rs, bool verbose)
        {
            var sb = new StringBuilder();
            int i = 0;
            foreach (var r in rs)
            {
                i++;
                sb.AppendLine("--- tohum " + i + " ---");
                sb.AppendLine("  ilk otomasyon (Otomatik Kazıyıcı): " + M(r.FirstAutoT));
                var f = r.Runs[0];
                sb.Append("  katalog açılışları (koşu 1):");
                for (int c = 2; c <= 5; c++) sb.Append(" K" + c + "=" + M(f.CatalogT[c]));
                sb.AppendLine();
                sb.AppendLine("  ilk prestij: " + H(r.FirstPrestigeT) + "   tüm ağaç: " + H(r.TreeDoneT) + "   (benzetim sonu " + H(r.EndT) + ")");
                sb.AppendLine("  en uzun bekleme (tümü): " + M(r.MaxGap) + " @" + H(r.MaxGapAt) + " [" + r.MaxGapLabel + "]");
                sb.AppendLine("  en uzun bekleme (koşu 1): " + M(r.MaxGapFirstRun) + " @" + H(r.MaxGapFirstRunAt) + " [" + r.MaxGapFirstRunLabel + "]");
                var runs = new StringBuilder();
                int k = 0;
                foreach (var run in r.Runs) { k++; runs.Append(" #" + k + ":" + H(run.EndT - run.StartT) + "/" + run.Ab + "AB"); }
                sb.AppendLine("  koşular:" + runs);
                if (verbose && i == 1)
                {
                    for (int q = 1; q < r.Marks.Count; q++)
                        if (r.Marks[q].T - r.Marks[q - 1].T >= 180) sb.AppendLine("    UZUN " + M(r.Marks[q].T - r.Marks[q - 1].T) + " @" + H(r.Marks[q - 1].T) + " " + r.Marks[q - 1].Label + " -> " + r.Marks[q].Label);
                }
            }
            Console.Write(sb.ToString());
            Console.WriteLine("=== ÖZET (" + rs.Count + " tohum) ===");
            Func<Func<SeedResult, double>, string> mm = sel => { var v = rs.Select(sel).ToList(); return v.Min().ToString("0.00") + " .. " + v.Max().ToString("0.00") + " (ort " + v.Average().ToString("0.00") + ")"; };
            Console.WriteLine("ilk otomasyon (dk):   " + mm(r => r.FirstAutoT / 60.0) + "   [hedef 30-45]");
            Console.WriteLine("ilk prestij (sa):     " + mm(r => r.FirstPrestigeT / 3600.0) + "   [hedef 4-6]");
            Console.WriteLine("tüm ağaç (sa):        " + mm(r => (r.TreeDoneT < 0 ? r.EndT : r.TreeDoneT) / 3600.0) + "   [hedef 15-20]");
            Console.WriteLine("en uzun bekleme (dk): " + mm(r => r.MaxGap / 60.0) + "   [hedef < 5]");
            Console.WriteLine("TUNE AUTO=" + rs.Average(r => r.FirstAutoT / 60.0).ToString("0.0") + " " + string.Join(" ", Enumerable.Range(2, 4).Select(c => "K" + c + "=" + (rs.Average(r => r.Runs[0].CatalogT[c]) / 60.0).ToString("0.0"))) + " PRESTIGE=" + (rs.Average(r => r.FirstPrestigeT) / 60.0).ToString("0.0") + " TREE=" + (rs.Average(r => (r.TreeDoneT < 0 ? r.EndT : r.TreeDoneT)) / 60.0).ToString("0.0") + " GAP=" + rs.Max(r => r.MaxGap / 60.0).ToString("0.0"));
            Console.WriteLine("katalog açılış ort. (dk): " + string.Join(", ", Enumerable.Range(2, 4).Select(c => "K" + c + "=" + (rs.Average(r => r.Runs[0].CatalogT[c]) / 60.0).ToString("0.0"))));
        }

        static bool Accept(List<SeedResult> rs)
        {
            bool ok = true;
            foreach (var r in rs)
            {
                double auto = r.FirstAutoT / 60.0, fp = r.FirstPrestigeT / 3600.0, tree = r.TreeDoneT / 3600.0, gap = r.MaxGap / 60.0;
                if (r.FirstAutoT < 0 || auto < 30 || auto > 45) ok = false;
                if (r.FirstPrestigeT < 0 || fp < 4 || fp > 6) ok = false;
                if (r.TreeDoneT < 0 || tree < 15 || tree > 20) ok = false;
                if (gap >= 5) ok = false;
            }
            Console.WriteLine(ok ? "KABUL: TÜM ÖLÇÜTLER TUTTU" : "KABUL: TUTMADI");
            return ok;
        }
    }
}
