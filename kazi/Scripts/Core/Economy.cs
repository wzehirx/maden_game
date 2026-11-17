using System;

namespace Kazi.Core
{
    /// <summary>Beklenen kazanç hesapları (otomatik tür seçimi ve denge simülasyonu için).</summary>
    public static class Economy
    {
        /// <summary>Bir kartın ortalama net kazancı (lira): ödeme çarpanı ve şans dahil, ceza ve bilet bedeli düşülmüş.</summary>
        public static double ExpectedNet(GameConfig cfg, Stats st, CardTypeDef t)
        {
            var set = cfg.Settings;
            double gross = Odds.ExpectedPayMult(t, st.Luck, set) * t.Price * st.PayoutMult;
            if (t.IsGate && t.PityAfter > 0)
            {
                double p = Odds.WinChance(t, st.Luck, set);
                double trials = Odds.ExpectedTrialsPerWin(t, st.Luck, set);
                gross = Odds.ExpectedPayMult(t, st.Luck, set) / Math.Max(1e-9, p) / trials * t.Price * st.PayoutMult;
            }
            double pen = Odds.ExpectedPenaltyMult(t, st.Luck, set) * t.Price;
            return gross - pen - st.TicketPrice(t);
        }

        /// <summary>Verilen katalog sınırı ve bakiye için en iyi (en yüksek net kazançlı) kapı olmayan tür: bilet bedeli bakiyenin 'fraction' katını aşmaz. Hiçbiri sığmazsa en ucuz.</summary>
        public static CardTypeDef BestType(GameConfig cfg, Stats st, int maxCatalog, double bankroll, double fraction)
        {
            CardTypeDef best = null, cheap = null; double bestNet = double.NegativeInfinity;
            foreach (var t in cfg.CardTypes)
            {
                if (t.Catalog > maxCatalog || t.IsGate) continue;
                long price = st.TicketPrice(t);
                if (cheap == null || price < st.TicketPrice(cheap)) cheap = t;
                if (price > bankroll * fraction) continue;
                double n = ExpectedNet(cfg, st, t);
                if (n > bestNet) { bestNet = n; best = t; }
            }
            return best ?? cheap;
        }
    }
}
