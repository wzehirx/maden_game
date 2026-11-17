using System;
using System.Collections.Generic;

namespace Kazi.Core
{
    /// <summary>İhtimal ve beklenen getiri hesapları. Kart üreticisiyle AYNI formülleri kullanır; RTP testi ikisini karşılaştırır.</summary>
    public static class Odds
    {
        private const double MaxShift = 1.0;

        public static double WinChance(CardTypeDef t, double luck, SettingsDef s)
        {
            double w = t.WinChance * (1.0 + Math.Max(0, luck) * s.LuckWinScale);
            return Math.Min(t.MaxWinChance, w);
        }

        private static double Shift(double luck, SettingsDef s) { return Math.Min(MaxShift, Math.Max(0, luck) * s.LuckSymbolScale); }

        /// <summary>Kazanan sembol ağırlıkları (şansla yüksek ödüllü sembollere kayar). Ödemeyen/özel semboller 0.</summary>
        public static double[] WinWeights(CardTypeDef t, double luck, SettingsDef s)
        {
            double sh = Shift(luck, s);
            var w = new double[t.Symbols.Count];
            for (int i = 0; i < w.Length; i++)
            {
                var sy = t.Symbols[i];
                if ((sy.Kind == SymbolKind.Normal || sy.Kind == SymbolKind.Gate) && sy.Pay > 0 && sy.Weight > 0)
                    w[i] = sy.Weight * Math.Pow(sy.Pay, sh);
            }
            return w;
        }

        public static double[] MultiplierWeights(CardTypeDef t, double luck, SettingsDef s)
        {
            double sh = Shift(luck, s);
            var w = new double[t.Multipliers.Count];
            for (int i = 0; i < w.Length; i++) w[i] = t.Multipliers[i].Weight * Math.Pow(Math.Max(1.0, t.Multipliers[i].Mult), sh);
            return w;
        }

        public static double TrapChance(CardTypeDef t, double luck, SettingsDef s)
        {
            double red = Math.Min(t.MaxTrapReduction, 1.0 - 1.0 / (1.0 + Math.Max(0, luck) * s.LuckTrapScale));
            return t.TrapChance * (1.0 - red);
        }

        private static double Sum(double[] a) { double x = 0; foreach (double v in a) x += v; return x; }

        /// <summary>Ortalama brüt ödeme çarpanı (bilet fiyatının katı), ceza HARİÇ, Son Şans garantisi (pity) HARİÇ.</summary>
        public static double ExpectedPayMult(CardTypeDef t, double luck, SettingsDef s)
        {
            double wc = WinChance(t, luck, s);
            if (t.Rule == CardRule.SonSans || t.Rule == CardRule.AltinBilet)
            {
                foreach (var sy in t.Symbols) if (sy.Kind == SymbolKind.Gate) return wc * sy.Pay;
                return 0;
            }
            double[] w = WinWeights(t, luck, s);
            double tw = Sum(w);
            if (tw <= 0) return 0;
            double e1 = 0;
            for (int i = 0; i < w.Length; i++) e1 += w[i] / tw * t.Symbols[i].Pay;
            switch (t.Rule)
            {
                case CardRule.NumberHunt:
                    {
                        double e2 = 0;
                        for (int i = 0; i < w.Length; i++)
                        {
                            if (w[i] <= 0) continue;
                            double rest = tw - w[i];
                            if (rest <= 0) continue;
                            double inner = 0;
                            for (int j = 0; j < w.Length; j++) if (j != i) inner += w[j] / rest * t.Symbols[j].Pay;
                            e2 += w[i] / tw * inner;
                        }
                        return wc * (e1 + t.DoubleChance * e2);
                    }
                case CardRule.Multiplier:
                    {
                        double[] mw = MultiplierWeights(t, luck, s);
                        double tm = Sum(mw), em = 0;
                        for (int i = 0; i < mw.Length; i++) em += mw[i] / tm * t.Multipliers[i].Mult;
                        return wc * e1 * em;
                    }
                default:
                    return wc * e1;
            }
        }

        /// <summary>Ortalama ceza çarpanı (yalnızca Tuzak kartları).</summary>
        public static double ExpectedPenaltyMult(CardTypeDef t, double luck, SettingsDef s)
        {
            if (t.Rule != CardRule.Trap) return 0;
            double tw = 0, avg = 0;
            foreach (var sy in t.Symbols) if (sy.Kind == SymbolKind.Trap) { tw += sy.Weight; avg += sy.Weight * sy.Pay; }
            if (tw <= 0) return 0;
            avg /= tw;
            double tc = TrapChance(t, luck, s);
            return tc * (1.0 + t.TrapDoubleChance) * avg;
        }

        /// <summary>Net getiri (ödeme − ceza) / bilet fiyatı; ödeme çarpanı ve indirim hariç temel RTP.</summary>
        public static double Rtp(CardTypeDef t, double luck, SettingsDef s)
        {
            return ExpectedPayMult(t, luck, s) - ExpectedPenaltyMult(t, luck, s);
        }

        /// <summary>Son Şans kartı için garantili (pity) etkin kazanma olasılığı ve bir kazanıma kadar beklenen deneme.</summary>
        public static double ExpectedTrialsPerWin(CardTypeDef t, double luck, SettingsDef s)
        {
            double p = WinChance(t, luck, s);
            if (p <= 0) return t.PityAfter > 0 ? t.PityAfter + 1 : double.PositiveInfinity;
            if (t.PityAfter <= 0) return 1.0 / p;
            return (1.0 - Math.Pow(1.0 - p, t.PityAfter + 1)) / p;
        }
    }
}
