using System;
using System.Collections.Generic;

namespace Kazi.Core
{
    public struct CardResult
    {
        public bool Win;            // brüt ödeme > 0
        public double PayMult;      // brüt ödeme çarpanı (bilet fiyatının katı), ceza hariç
        public double PenaltyMult;  // ceza çarpanı
        public int WinSymbol;       // en iyi ödeyen sembol indeksi (-1 yok)
        public int Matches;         // sayı avı eşleşme sayısı
        public double Multiplier;   // çarpan kartı: uygulanan çarpan (1 = yok)
        public int TrapCount;
        public bool GateHit;        // Son Şans anahtarı tuttu
    }

    /// <summary>Kural çözücüler: hücrelerden ödemeyi hesaplar (hücreler zaten belirlidir).</summary>
    public static class RuleEngine
    {
        public static CardResult Resolve(CardTypeDef t, int[] cells)
        {
            var r = new CardResult { WinSymbol = -1, Multiplier = 1 };
            switch (t.Rule)
            {
                case CardRule.ThreeOfAKind:
                case CardRule.Jackpot:
                    Triples(t, cells, 0, 6, ref r);
                    break;
                case CardRule.SonSans:
                case CardRule.AltinBilet:
                    Triples(t, cells, 0, 6, ref r);
                    r.GateHit = r.PayMult > 0;
                    break;
                case CardRule.Multiplier:
                    Triples(t, cells, 0, 6, ref r);
                    if (r.Win)
                    {
                        int mi = cells[6];
                        double m = (mi >= 0 && mi < t.Multipliers.Count) ? t.Multipliers[mi].Mult : 1.0;
                        r.Multiplier = m;
                        r.PayMult *= m;
                    }
                    break;
                case CardRule.Joker:
                    JokerRule(t, cells, ref r);
                    break;
                case CardRule.Trap:
                    Triples(t, cells, 0, 9, ref r);
                    foreach (int c in cells)
                    {
                        var sy = t.Symbols[c];
                        if (sy.Kind == SymbolKind.Trap) { r.PenaltyMult += sy.Pay; r.TrapCount++; }
                    }
                    break;
                case CardRule.Sequence:
                    SequenceRule(t, cells, ref r);
                    break;
                case CardRule.NumberHunt:
                    NumberRule(t, cells, ref r);
                    break;
            }
            r.Win = r.PayMult > 0;
            return r;
        }

        private static void Triples(CardTypeDef t, int[] cells, int from, int to, ref CardResult r)
        {
            var cnt = new int[t.Symbols.Count];
            for (int i = from; i < to; i++) cnt[cells[i]]++;
            double best = -1;
            for (int s = 0; s < cnt.Length; s++)
            {
                var sy = t.Symbols[s];
                if (cnt[s] >= 3 && sy.Pay > 0 && sy.Kind != SymbolKind.Trap && sy.Kind != SymbolKind.Joker)
                {
                    r.PayMult += sy.Pay;
                    if (sy.Pay > best) { best = sy.Pay; r.WinSymbol = s; }
                }
            }
        }

        private static void JokerRule(CardTypeDef t, int[] cells, ref CardResult r)
        {
            var cnt = new int[t.Symbols.Count];
            int jokers = 0;
            foreach (int c in cells)
            {
                if (t.Symbols[c].Kind == SymbolKind.Joker) jokers++; else cnt[c]++;
            }
            double best = 0; int bi = -1;
            for (int s = 0; s < cnt.Length; s++)
            {
                var sy = t.Symbols[s];
                if (sy.Kind == SymbolKind.Joker || sy.Kind == SymbolKind.Trap || sy.Pay <= 0) continue;
                if (cnt[s] + jokers >= 3 && sy.Pay > best) { best = sy.Pay; bi = s; }
            }
            r.PayMult = best; r.WinSymbol = bi;
        }

        private static readonly int[][] Lines =
        {
            new[] {0,1,2}, new[] {3,4,5}, new[] {6,7,8},
            new[] {0,3,6}, new[] {1,4,7}, new[] {2,5,8}
        };

        public static int[][] SequenceLines { get { return Lines; } }

        private static void SequenceRule(CardTypeDef t, int[] cells, ref CardResult r)
        {
            double best = -1;
            foreach (var ln in Lines)
            {
                int a = cells[ln[0]];
                if (cells[ln[1]] == a && cells[ln[2]] == a)
                {
                    var sy = t.Symbols[a];
                    if (sy.Pay > 0 && sy.Kind == SymbolKind.Normal)
                    {
                        r.PayMult += sy.Pay;
                        if (sy.Pay > best) { best = sy.Pay; r.WinSymbol = a; }
                    }
                }
            }
        }

        private static void NumberRule(CardTypeDef t, int[] cells, ref CardResult r)
        {
            var win = new HashSet<int>();
            for (int i = 0; i < 3; i++) win.Add(cells[i]);
            double best = -1;
            for (int i = 3; i < 9; i++)
            {
                if (win.Contains(cells[i]))
                {
                    var sy = t.Symbols[cells[i]];
                    if (sy.Pay > 0)
                    {
                        r.PayMult += sy.Pay; r.Matches++;
                        if (sy.Pay > best) { best = sy.Pay; r.WinSymbol = cells[i]; }
                    }
                }
            }
        }
    }
}
