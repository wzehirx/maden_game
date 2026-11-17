using System;
using System.Collections.Generic;

namespace Kazi.Core
{
    /// <summary>Kart üretimi: tüm hücreler tohumdan ÖNCEDEN belirlenir. Sonuç önce seçilir, yerleşim ona göre kurulur.</summary>
    public static class CardFactory
    {
        public static Card Create(CardTypeDef t, ulong seed, double luck, SettingsDef set, bool forceWin)
        {
            var rng = new Rng(seed);
            int n = t.Cells;
            var cells = new int[n];
            for (int i = 0; i < n; i++) cells[i] = -1;

            double wc = Odds.WinChance(t, luck, set);
            bool win = forceWin || rng.NextDouble() < wc;
            double[] ww = Odds.WinWeights(t, luck, set);

            switch (t.Rule)
            {
                case CardRule.ThreeOfAKind:
                case CardRule.Jackpot:
                    GenTriple(t, rng, cells, 6, win, ww);
                    break;
                case CardRule.Multiplier:
                    GenTriple(t, rng, cells, 6, win, ww);
                    cells[6] = rng.Pick(Odds.MultiplierWeights(t, luck, set));
                    if (cells[6] < 0) cells[6] = 0;
                    break;
                case CardRule.SonSans:
                case CardRule.AltinBilet:
                    GenGate(t, rng, cells, win);
                    break;
                case CardRule.Joker:
                    GenJoker(t, rng, cells, win, ww);
                    break;
                case CardRule.Trap:
                    GenTrap(t, rng, cells, win, ww, Odds.TrapChance(t, luck, set));
                    break;
                case CardRule.Sequence:
                    GenSequence(t, rng, cells, win, ww);
                    break;
                case CardRule.NumberHunt:
                    GenNumbers(t, rng, cells, win, ww);
                    break;
            }
            return new Card(t, seed, cells);
        }

        // ---- yardımcılar ----
        private static List<int> KindIdx(CardTypeDef t, SymbolKind k)
        {
            var l = new List<int>();
            for (int i = 0; i < t.Symbols.Count; i++) if (t.Symbols[i].Kind == k) l.Add(i);
            return l;
        }

        private static List<int> FreeCells(int[] cells, int from, int to)
        {
            var l = new List<int>();
            for (int i = from; i < to; i++) if (cells[i] < 0) l.Add(i);
            return l;
        }

        /// <summary>Boş hücreleri izin verilen sembollerle doldurur; her sembol en fazla maxEach kez (zaten yerleştirilenler dahil).</summary>
        private static void Fill(Rng rng, CardTypeDef t, int[] cells, int from, int to, List<int> allowed, int maxEach, int[] extraCount = null)
        {
            var cnt = new int[t.Symbols.Count];
            for (int i = 0; i < cells.Length; i++) if (cells[i] >= 0 && cells[i] < cnt.Length) cnt[cells[i]]++;
            if (extraCount != null) for (int i = 0; i < cnt.Length; i++) cnt[i] += extraCount[i];
            var w = new double[allowed.Count];
            for (int i = from; i < to; i++)
            {
                if (cells[i] >= 0) continue;
                for (int a = 0; a < allowed.Count; a++) w[a] = cnt[allowed[a]] < maxEach ? Math.Max(0.0001, t.Symbols[allowed[a]].Weight) : 0;
                int pick = rng.Pick(w);
                if (pick < 0)
                {
                    // kapasite bitti (yapılandırma çok az sembol içeriyor): en az kullanılanı seç
                    int bi = 0;
                    for (int a = 1; a < allowed.Count; a++) if (cnt[allowed[a]] < cnt[allowed[bi]]) bi = a;
                    pick = bi;
                }
                cells[i] = allowed[pick];
                cnt[allowed[pick]]++;
            }
        }

        private static void PlaceCopies(Rng rng, int[] cells, int from, int to, int sym, int count)
        {
            var free = FreeCells(cells, from, to);
            rng.Shuffle(free);
            for (int i = 0; i < count && i < free.Count; i++) cells[free[i]] = sym;
        }

        private static List<int> WithoutSym(List<int> l, int sym)
        {
            var r = new List<int>();
            foreach (int x in l) if (x != sym) r.Add(x);
            return r;
        }

        // ---- kural başına yerleşim ----
        private static void GenTriple(CardTypeDef t, Rng rng, int[] cells, int upto, bool win, double[] ww)
        {
            var normal = KindIdx(t, SymbolKind.Normal);
            if (win)
            {
                int s = rng.Pick(ww);
                PlaceCopies(rng, cells, 0, upto, s, 3);
                Fill(rng, t, cells, 0, upto, WithoutSym(normal, s), 2);
            }
            else Fill(rng, t, cells, 0, upto, normal, 2);
        }

        private static void GenGate(CardTypeDef t, Rng rng, int[] cells, bool win)
        {
            int g = KindIdx(t, SymbolKind.Gate)[0];
            var decoys = KindIdx(t, SymbolKind.Normal);
            if (win)
            {
                PlaceCopies(rng, cells, 0, 6, g, 3);
                Fill(rng, t, cells, 0, 6, decoys, 2);
            }
            else
            {
                double r = rng.NextDouble();
                int near = r < 0.5 ? 0 : (r < 0.8 ? 1 : 2);
                PlaceCopies(rng, cells, 0, 6, g, near);
                Fill(rng, t, cells, 0, 6, decoys, 2);
            }
        }

        private static void GenJoker(CardTypeDef t, Rng rng, int[] cells, bool win, double[] ww)
        {
            int joker = KindIdx(t, SymbolKind.Joker)[0];
            var normal = KindIdx(t, SymbolKind.Normal);
            if (win)
            {
                int s = rng.Pick(ww);
                int j = rng.NextDouble() < t.JokerChance ? 1 : 0;
                PlaceCopies(rng, cells, 0, 6, s, 3 - j);
                PlaceCopies(rng, cells, 0, 6, joker, j);
                // başka sembol joker yardımıyla üçlü olmasın: j=1 iken diğerleri en fazla 1
                Fill(rng, t, cells, 0, 6, WithoutSym(normal, s), j == 1 ? 1 : 2);
            }
            else
            {
                int j = rng.NextDouble() < t.JokerChance ? 1 : 0;
                PlaceCopies(rng, cells, 0, 6, joker, j);
                Fill(rng, t, cells, 0, 6, normal, j == 1 ? 1 : 2);
            }
        }

        private static void GenTrap(CardTypeDef t, Rng rng, int[] cells, bool win, double[] ww, double trapChance)
        {
            var normal = KindIdx(t, SymbolKind.Normal);
            var traps = KindIdx(t, SymbolKind.Trap);
            var tw = new double[traps.Count];
            for (int i = 0; i < tw.Length; i++) tw[i] = t.Symbols[traps[i]].Weight;
            int tc = 0;
            if (rng.NextDouble() < trapChance) tc = rng.NextDouble() < t.TrapDoubleChance ? 2 : 1;
            for (int k = 0; k < tc; k++)
            {
                int ts = traps[Math.Max(0, rng.Pick(tw))];
                PlaceCopies(rng, cells, 0, 9, ts, 1);
            }
            if (win)
            {
                int s = rng.Pick(ww);
                PlaceCopies(rng, cells, 0, 9, s, 3);
                Fill(rng, t, cells, 0, 9, WithoutSym(normal, s), 2);
            }
            else Fill(rng, t, cells, 0, 9, normal, 2);
        }

        private static bool UniformLines(int[] cells, out int count)
        {
            count = 0;
            foreach (var ln in RuleEngine.SequenceLines)
                if (cells[ln[0]] == cells[ln[1]] && cells[ln[1]] == cells[ln[2]]) count++;
            return count > 0;
        }

        private static void GenSequence(CardTypeDef t, Rng rng, int[] cells, bool win, double[] ww)
        {
            var normal = KindIdx(t, SymbolKind.Normal);
            for (int attempt = 0; attempt < 200; attempt++)
            {
                for (int i = 0; i < 9; i++) cells[i] = -1;
                if (win)
                {
                    int s = rng.Pick(ww);
                    var ln = RuleEngine.SequenceLines[rng.Next(6)];
                    foreach (int c in ln) cells[c] = s;
                    Fill(rng, t, cells, 0, 9, WithoutSym(normal, s), 9);
                }
                else Fill(rng, t, cells, 0, 9, normal, 9);
                int uc; UniformLines(cells, out uc);
                if (uc == (win ? 1 : 0)) return;
            }
            // yedek: belirleyici desen (satırlar farklı, sütunlar farklı)
            for (int i = 0; i < 9; i++) cells[i] = normal[(i % 3 + (i / 3)) % normal.Count];
            if (win)
            {
                int s = rng.Pick(ww);
                cells[0] = cells[1] = cells[2] = s;
                for (int i = 3; i < 9; i++) if (cells[i] == s) cells[i] = normal[(normal.IndexOf(s) + 1) % normal.Count];
                int uc; UniformLines(cells, out uc);
            }
        }

        private static void GenNumbers(CardTypeDef t, Rng rng, int[] cells, bool win, double[] ww)
        {
            var nums = KindIdx(t, SymbolKind.Normal);
            var winSet = new List<int>();
            var matched = new List<int>();
            if (win)
            {
                int m1 = rng.Pick(ww);
                matched.Add(m1);
                if (rng.NextDouble() < t.DoubleChance)
                {
                    var w2 = (double[])ww.Clone(); w2[m1] = 0;
                    int m2 = rng.Pick(w2);
                    if (m2 >= 0) matched.Add(m2);
                }
            }
            winSet.AddRange(matched);
            var rest = new List<int>(nums);
            foreach (int m in matched) rest.Remove(m);
            rng.Shuffle(rest);
            int ri = 0;
            while (winSet.Count < 3) winSet.Add(rest[ri++]);
            rng.Shuffle(winSet);
            for (int i = 0; i < 3; i++) cells[i] = winSet[i];
            // oyuncu sayıları
            var nonWin = new List<int>();
            foreach (int x in nums) if (!winSet.Contains(x)) nonWin.Add(x);
            var pos = new List<int> { 3, 4, 5, 6, 7, 8 };
            rng.Shuffle(pos);
            int pi = 0;
            foreach (int m in matched) cells[pos[pi++]] = m;
            var allowed = nonWin;
            Fill(rng, t, cells, 3, 9, allowed, 2);
        }
    }
}
