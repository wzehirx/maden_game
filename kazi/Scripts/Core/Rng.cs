using System;
using System.Collections.Generic;

namespace Kazi.Core
{
    /// <summary>Tohumlu, durumu kaydedilebilir RNG (SplitMix64). Aynı tohum aynı diziyi üretir.</summary>
    public sealed class Rng
    {
        private ulong s;
        public Rng(ulong seed) { s = seed; }
        public ulong State { get { return s; } set { s = value; } }

        public ulong NextULong()
        {
            unchecked
            {
                s += 0x9E3779B97F4A7C15UL;
                ulong z = s;
                z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
                z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
                return z ^ (z >> 31);
            }
        }

        public double NextDouble() { return (NextULong() >> 11) * (1.0 / 9007199254740992.0); }
        public int Next(int n) { if (n <= 1) return 0; int v = (int)(NextDouble() * n); return v >= n ? n - 1 : v; }

        /// <summary>Ağırlıklara göre indeks seçer; ağırlık sıfırsa o indeks asla seçilmez. Hepsi sıfırsa -1.</summary>
        public int Pick(IList<double> weights)
        {
            double total = 0;
            for (int i = 0; i < weights.Count; i++) if (weights[i] > 0) total += weights[i];
            if (total <= 0) return -1;
            double r = NextDouble() * total;
            int last = -1;
            for (int i = 0; i < weights.Count; i++)
            {
                if (weights[i] <= 0) continue;
                last = i;
                r -= weights[i];
                if (r < 0) return i;
            }
            return last;
        }

        public void Shuffle<T>(IList<T> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = Next(i + 1);
                T t = list[i]; list[i] = list[j]; list[j] = t;
            }
        }
    }
}
