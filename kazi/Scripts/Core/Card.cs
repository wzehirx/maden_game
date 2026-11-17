using System;
using System.Collections.Generic;

namespace Kazi.Core
{
    public enum ScratchOutcome { None, Peek, Open }

    /// <summary>Bir kart örneği. Hücreler oluşturulurken belirlenir; kazıma yalnızca açığa çıkarır.</summary>
    public sealed class Card
    {
        public CardTypeDef Type;
        public ulong Seed;
        public int[] Cells;
        public bool[] Open;
        public double[] Scratch;     // 0..1
        public bool[] Peeked;
        public long Paid;            // ödenen bilet bedeli
        public bool Resolved;
        public bool Discarded;
        public CardResult Result;

        public Card(CardTypeDef type, ulong seed, int[] cells)
        {
            Type = type; Seed = seed; Cells = cells;
            Open = new bool[cells.Length];
            Scratch = new double[cells.Length];
            Peeked = new bool[cells.Length];
        }

        /// <summary>Testler için: elle hücre vererek kart kurar.</summary>
        public static Card FromCells(CardTypeDef type, int[] cells) { return new Card(type, 0, (int[])cells.Clone()); }

        public bool Done { get { return Resolved || Discarded; } }
        public int CellCount { get { return Cells.Length; } }

        public int OpenCount
        {
            get { int n = 0; foreach (bool b in Open) if (b) n++; return n; }
        }

        public bool AllOpen { get { foreach (bool b in Open) if (!b) return false; return true; } }

        public CardResult Preview() { return RuleEngine.Resolve(Type, Cells); }

        public CardResult Resolve()
        {
            Result = RuleEngine.Resolve(Type, Cells);
            Resolved = true;
            return Result;
        }

        private void OpenCell(int i) { Open[i] = true; Scratch[i] = 1.0; Peeked[i] = true; }

        /// <summary>SIRALI mod: sıradaki 'power' kapalı hücreyi açar. Açılan indeksleri döner.</summary>
        public List<int> ScratchSequential(int power)
        {
            var opened = new List<int>();
            if (Done) return opened;
            for (int i = 0; i < Cells.Length && opened.Count < power; i++)
                if (!Open[i]) { OpenCell(i); opened.Add(i); }
            return opened;
        }

        /// <summary>SÜRÜKLEME modu: hücreyi 'amount' (0..1) kadar kazır; eşikler ayardan.</summary>
        public ScratchOutcome ScratchCell(int index, double amount, double openThreshold, double peekThreshold)
        {
            if (Done || index < 0 || index >= Cells.Length || Open[index] || amount <= 0 || double.IsNaN(amount)) return ScratchOutcome.None;
            Scratch[index] = Math.Min(1.0, Scratch[index] + amount);
            if (Scratch[index] >= openThreshold) { OpenCell(index); return ScratchOutcome.Open; }
            if (!Peeked[index] && Scratch[index] >= peekThreshold) { Peeked[index] = true; return ScratchOutcome.Peek; }
            return ScratchOutcome.None;
        }

        public List<int> OpenAll()
        {
            var l = new List<int>();
            for (int i = 0; i < Cells.Length; i++) if (!Open[i]) { OpenCell(i); l.Add(i); }
            return l;
        }

        public SymbolDef SymbolAt(int i)
        {
            if (Type.Rule == CardRule.Multiplier && i == 6) return null;
            int s = Cells[i];
            return (s >= 0 && s < Type.Symbols.Count) ? Type.Symbols[s] : null;
        }

        public bool IsTrapCell(int i)
        {
            var s = SymbolAt(i);
            return s != null && s.Kind == SymbolKind.Trap;
        }

        public bool TrapRevealed()
        {
            for (int i = 0; i < Cells.Length; i++) if (Open[i] && IsTrapCell(i)) return true;
            return false;
        }

        public int TrapCount()
        {
            int n = 0;
            for (int i = 0; i < Cells.Length; i++) if (IsTrapCell(i)) n++;
            return n;
        }

        /// <summary>Hücre açılınca okunacak kısa Türkçe ifade.</summary>
        public string DescribeCell(int i)
        {
            if (Type.Rule == CardRule.Multiplier && i == 6)
            {
                int mi = Cells[6];
                double m = (mi >= 0 && mi < Type.Multipliers.Count) ? Type.Multipliers[mi].Mult : 1;
                return m <= 1 ? "Çarpan yok" : "Çarpan " + NumberFormat.Dec(m) + " kat";
            }
            var sy = SymbolAt(i);
            if (sy == null) return "";
            if (Type.Rule == CardRule.NumberHunt)
                return (i < 3 ? "Kazanan sayı " : "Senin sayın ") + sy.Name;
            if (sy.Kind == SymbolKind.Trap) return "Dikkat, " + sy.Name;
            return sy.Name;
        }

        // ---- kayıt ----
        public Dictionary<string, object> ToDict()
        {
            var d = new Dictionary<string, object>();
            d["type"] = Type.Id;
            d["seed"] = Seed.ToString();
            var c = new List<object>(); foreach (int x in Cells) c.Add(x);
            d["cells"] = c;
            var o = new List<object>(); foreach (bool b in Open) o.Add(b ? 1 : 0);
            d["open"] = o;
            var s = new List<object>(); foreach (double v in Scratch) s.Add(Math.Round(v, 3));
            d["scratch"] = s;
            d["paid"] = Paid;
            d["resolved"] = Resolved;
            d["discarded"] = Discarded;
            return d;
        }

        public static Card FromDict(GameConfig cfg, Dictionary<string, object> d)
        {
            try
            {
                if (d == null) return null;
                CardTypeDef t;
                if (!cfg.TypeById.TryGetValue(MiniJson.GetString(d, "type", ""), out t)) return null;
                var cl = MiniJson.GetArray(d, "cells");
                if (cl == null || cl.Count != t.Cells) return null;
                var cells = new int[t.Cells];
                for (int i = 0; i < cells.Length; i++)
                {
                    if (!(cl[i] is double)) return null;
                    int v = (int)(double)cl[i];
                    int limit = (t.Rule == CardRule.Multiplier && i == 6) ? t.Multipliers.Count : t.Symbols.Count;
                    if (v < 0 || v >= limit) return null;
                    cells[i] = v;
                }
                ulong seed;
                ulong.TryParse(MiniJson.GetString(d, "seed", "0"), out seed);
                var card = new Card(t, seed, cells);
                var ol = MiniJson.GetArray(d, "open");
                var sl = MiniJson.GetArray(d, "scratch");
                for (int i = 0; i < cells.Length; i++)
                {
                    if (ol != null && i < ol.Count && ol[i] is double && (double)ol[i] > 0) { card.Open[i] = true; card.Scratch[i] = 1; card.Peeked[i] = true; }
                    else if (sl != null && i < sl.Count && sl[i] is double)
                    {
                        double sv = (double)sl[i];
                        card.Scratch[i] = (double.IsNaN(sv) || sv < 0) ? 0 : Math.Min(0.99, sv);
                    }
                }
                card.Paid = Math.Max(0, MiniJson.GetLong(d, "paid", 0));
                card.Discarded = MiniJson.GetBool(d, "discarded", false);
                if (MiniJson.GetBool(d, "resolved", false)) { card.OpenAll(); card.Resolve(); }
                return card;
            }
            catch (Exception) { return null; }
        }
    }
}
