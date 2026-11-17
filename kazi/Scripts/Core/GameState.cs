using System;
using System.Collections.Generic;

namespace Kazi.Core
{
    public enum SpeechDetail { Full = 0, Short = 1, Silent = 2 }
    public enum ScratchMode { Sequential = 0, Drag = 1 }

    /// <summary>Kaydedilen oyun durumu.</summary>
    public sealed class GameState
    {
        public const int CurrentVersion = 1;
        public int Version = CurrentVersion;
        public Wallet Wallet = new Wallet();
        public long Money { get { return Wallet.Balance; } set { Wallet.Balance = value; } }

        public Dictionary<string, int> Upgrades = new Dictionary<string, int>();
        public Dictionary<string, int> Nodes = new Dictionary<string, int>();
        public int UnlockedCatalog = 1;

        public Card Current;
        public List<Card> Bag = new List<Card>();
        public string LastTypeId = "";

        public bool AutoBuyOn = true;
        public bool AutoDiscardTrap = false;
        public string AutoType = "";
        public double AutoAccum;

        public Dictionary<string, int> Pity = new Dictionary<string, int>();

        public long AB;            // harcanabilir Altın Bilet puanı
        public long ABTotal;       // toplam kazanılan
        public int PrestigeCount;
        public bool PrestigeOffer;

        public long RunEarned;     // bu koşuda kazanılan toplam (brüt ödeme)
        public long TotalEarned;
        public long TotalCards;
        public double PlaySeconds;
        public double RunSeconds;

        public ulong RngState = 0x1234ABCDUL;
        public long LastSaveUnix;

        public bool NotifyAffordable = true;
        public SpeechDetail Detail = SpeechDetail.Short;
        /// <summary>Konuşma hızı çarpanı (Godot katmanı TTS'e iletir).</summary>
        public double SpeechRate = 1.25;
        /// <summary>Hızlı tekrar: kart bitince aynı türden sonraki kart otomatik alınır.</summary>
        public bool QuickRepeat = false;
        /// <summary>Oyun hızı: 1, 1.5, 2, 3. Otomasyon ve çevrimiçi tick'leri hızlandırır (çevrimdışı hariç).</summary>
        public double GameSpeed = 1.0;
        public ScratchMode Mode = ScratchMode.Sequential;

        public static readonly double[] SpeedSteps = { 1.0, 1.5, 2.0, 3.0 };
        public static double NormalizeSpeed(double v)
        {
            double best = 1.0;
            foreach (double x in SpeedSteps) if (Math.Abs(x - v) < Math.Abs(best - v)) best = x;
            return best;
        }

        public int Level(string id) { int v; return Upgrades.TryGetValue(id, out v) ? v : 0; }
        public int NodeLevel(string id) { int v; return Nodes.TryGetValue(id, out v) ? v : 0; }
        public int PityOf(string id) { int v; return Pity.TryGetValue(id, out v) ? v : 0; }

        /// <summary>Yüklemeden sonra değerleri güvenli aralığa çeker, bilinmeyen kimlikleri atar.</summary>
        public void Sanitize(GameConfig cfg)
        {
            if (Version <= 0) Version = CurrentVersion;
            if (UnlockedCatalog < 1) UnlockedCatalog = 1;
            if (UnlockedCatalog > cfg.CatalogCount) UnlockedCatalog = cfg.CatalogCount;
            var keys = new List<string>(Upgrades.Keys);
            foreach (var k in keys)
            {
                UpgradeDef d;
                if (!cfg.UpgradeById.TryGetValue(k, out d)) { Upgrades.Remove(k); continue; }
                int v = Upgrades[k];
                if (v < 0) v = 0;
                if (v > d.MaxLevel) v = d.MaxLevel;
                if (v == 0) Upgrades.Remove(k); else Upgrades[k] = v;
            }
            keys = new List<string>(Nodes.Keys);
            foreach (var k in keys)
            {
                if (!cfg.NodeById.ContainsKey(k)) { Nodes.Remove(k); continue; }
                if (Nodes[k] <= 0) Nodes.Remove(k); else Nodes[k] = 1;
            }
            keys = new List<string>(Pity.Keys);
            foreach (var k in keys) if (!cfg.TypeById.ContainsKey(k) || Pity[k] < 0) Pity.Remove(k);
            if (AB < 0) AB = 0;
            if (ABTotal < AB) ABTotal = AB;
            if (PrestigeCount < 0) PrestigeCount = 0;
            if (RunEarned < 0) RunEarned = 0;
            if (TotalEarned < 0) TotalEarned = 0;
            if (TotalCards < 0) TotalCards = 0;
            if (PlaySeconds < 0) PlaySeconds = 0;
            if (RunSeconds < 0) RunSeconds = 0;
            if (double.IsNaN(AutoAccum) || AutoAccum < 0) AutoAccum = 0;
            if (AutoAccum > 1) AutoAccum = 1;
            if (AutoType == null) AutoType = "";
            if (AutoType != "" && !cfg.TypeById.ContainsKey(AutoType)) AutoType = "";
            if (LastTypeId == null || !cfg.TypeById.ContainsKey(LastTypeId)) LastTypeId = "";
            if (Current != null && Current.Type == null) Current = null;
            Bag.RemoveAll(c => c == null || c.Type == null);
            if (!Enum.IsDefined(typeof(SpeechDetail), Detail)) Detail = SpeechDetail.Full;
            if (!Enum.IsDefined(typeof(ScratchMode), Mode)) Mode = ScratchMode.Sequential;
            if (double.IsNaN(SpeechRate) || SpeechRate < 0.5) SpeechRate = 0.5;
            if (SpeechRate > 3) SpeechRate = 3;
            GameSpeed = NormalizeSpeed(GameSpeed);
        }
    }

    /// <summary>Yükseltme ve prestij düğümlerinden türetilen etkin değerler.</summary>
    public sealed class Stats
    {
        public int ScratchPower = 1;
        public double Luck;
        public double PayoutMult = 1;
        public double Discount;
        public int BagCap = 1;
        public double OfflineCap;
        public double Refund;
        public bool AutoOwned, AutoBuyOwned;
        public int AutoTypeLevel;
        public double AutoCpm;
        public int Batch = 1;
        public int Lens;
        public double UpgradeCostMult = 1;
        public long StartMoney;
        public double AbGainMult = 1;
        public long HelpMoney;
        public int StartCatalog = 1;

        public static Stats Compute(GameConfig cfg, GameState st)
        {
            var s = new Stats();
            var set = cfg.Settings;
            double power = 0, luck = 0, pay = 0, disc = 0, bag = 0, off = 0, refund = 0, speed = 0, queue = 0, counter = 0, press = 0, lens = 0, ab = 0, ucd = 0, help = 0, startMoney = 0;
            int autoLvl = 0, autoBuy = 0, autoTypes = 0, startAuto = 0, startCat = 1;

            foreach (var u in cfg.Upgrades)
            {
                int lv = st.Level(u.Id);
                if (lv <= 0) continue;
                Apply(u.Kind, u.Effect * lv, lv, ref power, ref luck, ref pay, ref disc, ref bag, ref off, ref refund, ref speed, ref queue, ref counter, ref press, ref lens, ref ab, ref ucd, ref help, ref startMoney, ref autoLvl, ref autoBuy, ref autoTypes, ref startAuto, ref startCat);
            }
            foreach (var n in cfg.Nodes)
            {
                if (st.NodeLevel(n.Id) <= 0) continue;
                Apply(n.Kind, n.Value, 1, ref power, ref luck, ref pay, ref disc, ref bag, ref off, ref refund, ref speed, ref queue, ref counter, ref press, ref lens, ref ab, ref ucd, ref help, ref startMoney, ref autoLvl, ref autoBuy, ref autoTypes, ref startAuto, ref startCat);
            }

            s.ScratchPower = 1 + (int)Math.Floor(power + 1e-9);
            s.Luck = luck;
            s.PayoutMult = 1 + pay;
            s.Discount = Math.Min(set.MaxDiscount, disc);
            s.BagCap = Math.Max(1, set.BaseBag + (int)Math.Floor(bag + 1e-9) + (int)Math.Floor(queue + 1e-9));
            s.OfflineCap = Math.Min(set.OfflineCapSeconds, set.BaseOfflineSeconds + off);
            s.Refund = Math.Min(set.MaxRefund, set.DiscardRefundPct + refund);
            s.AutoOwned = autoLvl > 0 || startAuto > 0;
            s.AutoBuyOwned = autoBuy > 0;
            s.AutoTypeLevel = autoTypes;
            s.AutoCpm = s.AutoOwned ? (set.AutoBaseCpm + speed) * (1 + counter) : 0;
            s.Batch = 1 + (int)Math.Floor(press + 1e-9);
            s.Lens = (int)Math.Floor(lens + 1e-9);
            s.UpgradeCostMult = Math.Max(set.MinUpgradeCostMult, 1 - ucd);
            s.StartMoney = (long)startMoney;
            s.AbGainMult = 1 + ab;
            s.HelpMoney = (long)help;
            s.StartCatalog = Math.Max(1, Math.Min(cfg.CatalogCount, startCat));
            return s;
        }

        private static void Apply(EffectKind k, double eff, int lv,
            ref double power, ref double luck, ref double pay, ref double disc, ref double bag, ref double off, ref double refund,
            ref double speed, ref double queue, ref double counter, ref double press, ref double lens, ref double ab, ref double ucd,
            ref double help, ref double startMoney, ref int autoLvl, ref int autoBuy, ref int autoTypes, ref int startAuto, ref int startCat)
        {
            switch (k)
            {
                case EffectKind.ScratchPower: power += eff; break;
                case EffectKind.Luck: luck += eff; break;
                case EffectKind.Payout: pay += eff; break;
                case EffectKind.Discount: disc += eff; break;
                case EffectKind.Bag: bag += eff; break;
                case EffectKind.Offline: off += eff; break;
                case EffectKind.Refund: refund += eff; break;
                case EffectKind.AutoUnlock: autoLvl += lv; break;
                case EffectKind.AutoSpeed: speed += eff; break;
                case EffectKind.AutoQueue: queue += eff; break;
                case EffectKind.AutoBuy: autoBuy += lv; break;
                case EffectKind.AutoTypes: autoTypes += lv; break;
                case EffectKind.Counter: counter += eff; break;
                case EffectKind.Lens: lens += eff; break;
                case EffectKind.Press: press += eff; break;
                case EffectKind.StartMoney: startMoney += eff; break;
                case EffectKind.StartAuto: startAuto += 1; break;
                case EffectKind.AbGain: ab += eff; break;
                case EffectKind.UpgradeDiscount: ucd += eff; break;
                case EffectKind.StartCatalog: startCat = Math.Max(startCat, (int)eff); break;
                case EffectKind.HelpMoney: help += eff; break;
            }
        }

        public long TicketPrice(CardTypeDef t)
        {
            long p = (long)Math.Round(t.Price * (1.0 - Discount), MidpointRounding.AwayFromZero);
            return p < 1 ? 1 : p;
        }
    }
}
