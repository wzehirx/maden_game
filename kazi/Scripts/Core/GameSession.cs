using System;
using System.Collections.Generic;

namespace Kazi.Core
{
    public enum SpeechLevel { Cell, Result, Info, Menu }

    /// <summary>Olay tabanlı oyun oturumu. Godot/Unity bağımsızdır; oyun katmanı OnEvent'i dinler.</summary>
    public sealed partial class GameSession
    {
        public readonly GameConfig Config;
        public GameState State { get; private set; }
        public Stats Stats { get; private set; }
        public event Action<GameEvent> OnEvent;
        public Automation Auto { get; private set; }

        private Rng rng;
        private bool suppress; // çevrimdışı hesapta olay yayma
        private readonly HashSet<string> notified = new HashSet<string>();
        private bool notifyInit;
        private double lastNotifyAt = -1000;
        private double notifyCheckAccum;

        public GameSession(GameConfig cfg, GameState state = null)
        {
            Config = cfg;
            State = state ?? SaveSystem.NewGame(cfg);
            rng = new Rng(State.RngState);
            Auto = new Automation(this);
            RecalcStats();
            shopCat = 0; shopIndex = 0;
        }

        public void LoadState(GameState s)
        {
            State = s; rng = new Rng(State.RngState); RecalcStats(); notified.Clear(); notifyInit = false; confirmPrestige = false;
        }

        public SpeechDetail Detail { get { return State.Detail; } set { State.Detail = value; } }
        public ScratchMode Mode { get { return State.Mode; } set { State.Mode = value; } }
        public double SpeechRate { get { return State.SpeechRate; } set { State.SpeechRate = GameState.NormalizeRate(value); } }

        public void RecalcStats() { Stats = Stats.Compute(Config, State); }

        private ulong NextSeed() { ulong v = rng.NextULong(); State.RngState = rng.State; return v; }

        // ---- olay yayma ----
        internal void Emit(EventKind kind, string speech, SpeechLevel lvl, long amount = 0, string symbol = "", int cell = -1, Rarity rarity = Rarity.None)
        {
            if (suppress) return;
            string sp = speech ?? "";
            if (lvl != SpeechLevel.Menu)
            {
                if (State.Detail == SpeechDetail.Silent) sp = "";
                else if (State.Detail == SpeechDetail.Short && lvl == SpeechLevel.Cell) sp = "";
            }
            var h = OnEvent;
            if (h == null) return;
            var e = new GameEvent(kind, sp, amount, symbol, cell, rarity);
            e.EstimatedSeconds = GameEvent.Estimate(sp, State.SpeechRate);
            h(e);
        }

        // ---- kart alma ----
        public bool CanUseType(CardTypeDef t) { return t != null && t.Catalog <= State.UnlockedCatalog; }

        public long TicketPrice(CardTypeDef t) { return Stats.TicketPrice(t); }

        internal Card CreateCard(CardTypeDef t)
        {
            bool force = t.IsGate && t.PityAfter > 0 && State.PityOf(t.Id) >= t.PityAfter;
            var c = CardFactory.Create(t, NextSeed(), Stats.Luck + GadgetLuck(), Config.Settings, force);
            c.Paid = Stats.TicketPrice(t);
            return c;
        }

        private double GadgetLuck() { return 0; } // şans yükseltmeleri zaten Stats.Luck içinde

        /// <summary>Manuel kart satın alma. Başarılıysa true.</summary>
        public bool BuyCard(string typeId)
        {
            CardTypeDef t;
            if (typeId == null || !Config.TypeById.TryGetValue(typeId, out t)) { Emit(EventKind.Error, "Böyle bir kart yok.", SpeechLevel.Info); return false; }
            if (!CanUseType(t)) { Emit(EventKind.Error, "Bu katalog henüz kilitli.", SpeechLevel.Info); return false; }
            long price = Stats.TicketPrice(t);
            bool holding = State.Current != null && !State.Current.Done;
            if (holding && State.Bag.Count >= Stats.BagCap) { Emit(EventKind.BagFull, "Çanta dolu.", SpeechLevel.Info); return false; }
            if (State.Money < price)
            {
                Emit(EventKind.NotEnoughMoney, "Para yetmiyor, " + NumberFormat.Lira(price - State.Money) + " eksik.", SpeechLevel.Info, price - State.Money, t.Id);
                return false;
            }
            State.Wallet.TrySpend(price);
            var c = CreateCard(t);
            if (holding) State.Bag.Add(c); else State.Current = c;
            State.LastTypeId = t.Id;
            Emit(EventKind.CardBought, t.Name + ", " + NumberFormat.Lira(price) + ".", SpeechLevel.Result, price, t.Id);
            return true;
        }

        /// <summary>Otomatik alım: olay yayınlamaz, çantaya ekler.</summary>
        internal Card AutoBuyInto(CardTypeDef t)
        {
            long price = Stats.TicketPrice(t);
            if (State.Money < price) return null;
            State.Wallet.TrySpend(price);
            var c = CreateCard(t);
            State.Bag.Add(c);
            return c;
        }

        // ---- kazıma ----
        /// <summary>SIRALI mod: dokunuş başına ScratchPower hücre açar.</summary>
        public void ScratchTap()
        {
            var c = State.Current;
            if (c == null) { Emit(EventKind.NoCard, "Kartın yok. Yukarı kaydırıp dükkândan al.", SpeechLevel.Info); return; }
            if (c.Done) { Emit(EventKind.NoCard, "Kart bitti. Aşağı kaydırıp sıradakine geç.", SpeechLevel.Info); return; }
            var opened = c.ScratchSequential(Stats.ScratchPower);
            foreach (int i in opened) AnnounceCell(c, i);
            if (c.AllOpen) FinishCurrent();
        }

        /// <summary>SÜRÜKLEME modu: hücreyi amount (0..1) kadar kazır.</summary>
        public void ScratchDrag(int cell, double amount)
        {
            var c = State.Current;
            if (c == null || c.Done) return;
            var set = Config.Settings;
            var o = c.ScratchCell(cell, amount, set.OpenThreshold, set.PeekThreshold);
            if (o == ScratchOutcome.Open)
            {
                AnnounceCell(c, cell);
                if (c.AllOpen) FinishCurrent();
            }
            else if (o == ScratchOutcome.Peek)
            {
                bool trap = c.IsTrapCell(cell);
                Emit(EventKind.CellPeek, trap ? "Dikkat, tuzak gibi." : "", SpeechLevel.Result, 0, trap ? "tuzak" : "", cell);
            }
        }

        private void AnnounceCell(Card c, int i)
        {
            var sy = c.SymbolAt(i);
            Emit(EventKind.CellOpened, c.DescribeCell(i), SpeechLevel.Cell, 0, sy != null ? sy.Id : "carpan", i);
        }

        private void FinishCurrent()
        {
            var c = State.Current;
            if (c == null || c.Resolved) return;
            ApplyResult(c, false);
        }

        /// <summary>Çöpe at: yalnızca tuzaklı türlerde; ödeme/ceza yok, bilet bedelinin iade yüzdesi geri gelir.</summary>
        public bool DiscardCard()
        {
            var c = State.Current;
            if (c == null || c.Done) { Emit(EventKind.NoCard, "Bırakılacak kart yok.", SpeechLevel.Info); return false; }
            if (!c.Type.IsTrapType) { Emit(EventKind.Error, "Bu kartta tuzak yok, kazımaya devam et.", SpeechLevel.Info); return false; }
            long refund = DoDiscard(c);
            Emit(EventKind.CardDiscarded, "Kart bırakıldı, " + NumberFormat.Lira(refund) + " geri geldi.", SpeechLevel.Result, refund, c.Type.Id);
            return true;
        }

        internal long DoDiscard(Card c)
        {
            c.Discarded = true;
            long refund = (long)Math.Floor(c.Paid * Stats.Refund);
            State.Wallet.Add(refund);
            State.TotalCards++;
            return refund;
        }

        /// <summary>Sıradaki karta geç. Çantada kart varsa onu, yoksa son alınan türden yenisini alır.</summary>
        public bool NextCard()
        {
            var c = State.Current;
            if (c != null && !c.Done) { Emit(EventKind.Error, "Önce kartı kazı.", SpeechLevel.Info); return false; }
            if (State.Bag.Count > 0)
            {
                State.Current = State.Bag[0];
                State.Bag.RemoveAt(0);
                var nc = State.Current;
                Emit(EventKind.CardBought, nc.Type.Name + ".", SpeechLevel.Result, nc.Paid, nc.Type.Id);
                return true;
            }
            if (!string.IsNullOrEmpty(State.LastTypeId)) return BuyCard(State.LastTypeId);
            Emit(EventKind.NoCard, "Kart yok. Yukarı kaydırıp dükkândan al.", SpeechLevel.Info);
            return false;
        }

        // ---- ödeme ----
        internal Rarity RarityOf(CardTypeDef t, long gross)
        {
            if (gross <= 0) return Rarity.None;
            double m = (double)gross / t.Price;
            var r = Config.Settings.RarityMults;
            if (m < r[0]) return Rarity.Small;
            if (m < r[1]) return Rarity.Medium;
            if (m < r[2]) return Rarity.Big;
            return Rarity.Jackpot;
        }

        /// <summary>Kartı çözer, bakiyeyi günceller. Net değişimi (ödeme − ceza, bakiye sınırlı) döndürür.</summary>
        internal long ApplyResult(Card c, bool auto)
        {
            var t = c.Type;
            var r = c.Resolve();
            long gross = r.Win ? (long)Math.Round(t.Price * r.PayMult * Stats.PayoutMult, MidpointRounding.AwayFromZero) : 0;
            long penalty = r.PenaltyMult > 0 ? (long)Math.Ceiling(t.Price * r.PenaltyMult) : 0;
            long net = gross - penalty;
            long delta;
            if (net >= 0) { State.Wallet.Add(net); delta = net; }
            else { delta = -State.Wallet.SpendUpTo(-net); }
            State.RunEarned += gross; State.TotalEarned += gross; State.TotalCards++;
            Rarity rar = RarityOf(t, gross);

            if (t.IsGate)
            {
                if (r.GateHit) State.Pity[t.Id] = 0; else State.Pity[t.Id] = State.PityOf(t.Id) + 1;
            }

            if (auto) { autoCards++; if (gross > 0) autoWins++; autoNet += delta; }

            string speech;
            if (t.IsGate && r.GateHit) speech = "Son Şans tuttu! " + NumberFormat.Lira(gross) + " kazandın.";
            else if (penalty > 0 && gross > 0) speech = delta >= 0 ? "Kazandın ama tuzak ceza kesti, net " + NumberFormat.Lira(delta) + "." : "Tuzağa düştün, " + NumberFormat.Lira(-delta) + " kaybettin.";
            else if (penalty > 0) speech = "Tuzağa düştün, " + NumberFormat.Lira(-delta) + " ceza.";
            else if (gross > 0) speech = rar == Rarity.Jackpot ? "Jackpot! " + NumberFormat.Lira(gross) + "." : "Kazandın, " + NumberFormat.Lira(gross) + ".";
            else speech = "Bu sefer çıkmadı.";

            if (auto) Emit(EventKind.AutoResult, "", SpeechLevel.Cell, delta, t.Id, -1, rar);
            else
            {
                if (penalty > 0) Emit(EventKind.TrapHit, "", SpeechLevel.Cell, penalty, t.Id);
                Emit(EventKind.CardResult, speech, SpeechLevel.Result, delta, t.Id, -1, rar);
            }

            if (t.IsGate && r.GateHit) OnGateHit(t);
            return delta;
        }

        private void OnGateHit(CardTypeDef t)
        {
            if (t.Rule == CardRule.SonSans)
            {
                int next = t.Catalog + 1;
                if (next <= Config.CatalogCount && State.UnlockedCatalog < next)
                {
                    State.UnlockedCatalog = next;
                    Emit(EventKind.CatalogUnlocked, "Yeni katalog açıldı: " + Config.Catalogs[next - 1].Name + ".", SpeechLevel.Info, next, "");
                }
            }
            else
            {
                State.PrestigeOffer = true;
                Emit(EventKind.PrestigeOffer, "Altın Bilet tuttu! Sıfırlarsan " + PrestigeGain() + " Altın Bilet puanı kazanırsın. Dükkânda Prestij bölümünden karar ver.", SpeechLevel.Info, PrestigeGain());
            }
        }

        // ---- yardım parası ----
        internal long CheapestTicket()
        {
            long best = long.MaxValue;
            foreach (var t in Config.CardTypes)
                if (t.Catalog <= State.UnlockedCatalog) best = Math.Min(best, Stats.TicketPrice(t));
            return best;
        }

        internal void CheckHelp()
        {
            bool holding = State.Current != null && !State.Current.Done;
            if (holding || State.Bag.Count > 0) return;
            long cheap = CheapestTicket();
            if (State.Money >= cheap) return;
            long give = Math.Max(Config.Settings.HelpMoney + Stats.HelpMoney, cheap - State.Money);
            State.Wallet.Add(give);
            Emit(EventKind.HelpMoney, "Yardım parası, " + NumberFormat.Lira(give) + ".", SpeechLevel.Info, give);
        }

        // ---- zaman ----
        public void Tick(double dt)
        {
            if (dt <= 0 || double.IsNaN(dt)) return;
            State.PlaySeconds += dt; State.RunSeconds += dt;
            Auto.Advance(dt);
            CheckHelp();
            AutoReportTick(dt);
            notifyCheckAccum += dt;
            if (notifyCheckAccum >= 0.5) { notifyCheckAccum = 0; CheckAffordable(); }
        }

        // ---- otomasyon özeti (toplu) ----
        private int autoCards, autoWins; private long autoNet; private double autoReportAccum;

        private void AutoReportTick(double dt)
        {
            autoReportAccum += dt;
            if (autoReportAccum < Config.Settings.AutoReportSeconds) return;
            autoReportAccum = 0;
            FlushAutoReport();
        }

        public void FlushAutoReport()
        {
            if (autoCards <= 0) return;
            string sp = autoCards + " kart kazıldı, " + autoWins + " kazandı.";
            Emit(EventKind.AutoReport, sp, SpeechLevel.Info, autoNet, "", autoCards);
            autoCards = 0; autoWins = 0; autoNet = 0;
        }

        // ---- alınabilir yükseltme bildirimi ----
        private void CheckAffordable()
        {
            var fresh = new List<UpgradeDef>();
            foreach (var u in Config.Upgrades)
            {
                string key = u.Id + "#" + State.Level(u.Id);
                if (!UpgradeSystem.IsAvailable(Config, State, u)) { notified.Remove(key); continue; }
                long cost = UpgradeSystem.NextCost(Config, State, Stats, u);
                if (cost <= State.Money) { if (!notified.Contains(key)) fresh.Add(u); }
                else notified.Remove(key);
            }
            if (fresh.Count == 0) return;
            if (!notifyInit) { notifyInit = true; foreach (var u in fresh) notified.Add(u.Id + "#" + State.Level(u.Id)); return; }
            notifyInit = true;
            if (State.PlaySeconds - lastNotifyAt < Config.Settings.AffordableCooldownSeconds) return;
            lastNotifyAt = State.PlaySeconds;
            foreach (var u in fresh) notified.Add(u.Id + "#" + State.Level(u.Id));
            if (!State.NotifyAffordable) return;
            Emit(EventKind.UpgradeAffordable, "Alabilirsin: " + fresh[0].Name + ".", SpeechLevel.Info, 0, fresh[0].Id);
        }

        // ---- durum ----
        public string ReadStatus()
        {
            var parts = new List<string>();
            parts.Add("Bakiye " + NumberFormat.Lira(State.Money) + ".");
            parts.Add("Katalog " + State.UnlockedCatalog + ", " + Config.Catalogs[State.UnlockedCatalog - 1].Name + ".");
            var c = State.Current;
            if (c != null && !c.Done) parts.Add(c.Type.Name + ", " + c.OpenCount + " hücre açık, " + (c.CellCount - c.OpenCount) + " kapalı.");
            else if (c != null) parts.Add("Kart bitti.");
            else parts.Add("Elinde kart yok.");
            if (State.Bag.Count > 0) parts.Add("Çantada " + State.Bag.Count + " kart.");
            if (Stats.AutoOwned) parts.Add("Otomatik kazıyıcı dakikada " + NumberFormat.Dec(Math.Round(Stats.AutoCpm * Stats.Batch, 1)) + " kart.");
            string hint = LensHint();
            if (hint != "") parts.Add(hint);
            if (State.AB > 0 || State.PrestigeCount > 0) parts.Add("Altın Bilet puanı " + State.AB + ".");
            if (State.PrestigeOffer) parts.Add("Sıfırlama teklifi bekliyor.");
            string s = string.Join(" ", parts);
            Emit(EventKind.Status, s, SpeechLevel.Menu, State.Money);
            return s;
        }

        /// <summary>Mercek: tuzaklı kartta tuzak ihtimali ipucu (doğruluk mercek seviyesiyle artar).</summary>
        public string LensHint()
        {
            var c = State.Current;
            if (Stats.Lens <= 0 || c == null || c.Done || !c.Type.IsTrapType) return "";
            double acc = Math.Min(0.95, 0.6 + 0.1 * Stats.Lens);
            var r = new Rng(c.Seed ^ 0xA5A5A5A5UL);
            bool truth = c.TrapCount() > 0;
            bool say = r.NextDouble() < acc ? truth : !truth;
            return say ? "Mercek: bu kartta tuzak ihtimali yüksek." : "Mercek: tuzak ihtimali düşük.";
        }
    }
}
