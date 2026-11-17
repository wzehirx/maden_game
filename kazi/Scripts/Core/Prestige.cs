using System;

namespace Kazi.Core
{
    /// <summary>Altın Bilet puanı (AB) hesabı ve koşu sıfırlama.</summary>
    public static class Prestige
    {
        public static long ComputeGain(GameConfig cfg, GameState st, Stats stats)
        {
            var s = cfg.Settings;
            if (st.RunEarned <= 0) return 0;
            double g = s.AbBase * Math.Pow(st.RunEarned / s.AbRefEarned, s.AbExponent) * stats.AbGainMult;
            long r = (long)Math.Floor(g);
            return r < 1 ? 1 : r;
        }

        /// <summary>Koşuyu sıfırlar; AB, düğümler, istatistikler ve kullanıcı ayarları kalır.</summary>
        public static long Reset(GameConfig cfg, GameState st, Stats before)
        {
            long gain = ComputeGain(cfg, st, before);
            st.AB += gain; st.ABTotal += gain; st.PrestigeCount++;
            st.Upgrades.Clear();
            st.Current = null; st.Bag.Clear();
            st.LastTypeId = ""; st.AutoType = ""; st.AutoAccum = 0;
            st.Pity.Clear();
            st.PrestigeOffer = false;
            st.RunEarned = 0; st.RunSeconds = 0;
            var after = Stats.Compute(cfg, st);
            st.UnlockedCatalog = after.StartCatalog;
            st.Money = cfg.Settings.StartMoney + after.StartMoney;
            return gain;
        }
    }
}
