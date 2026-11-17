using System;

namespace Kazi.Core
{
    /// <summary>Yükseltme maliyeti ve uygunluk. Maliyet = taban × çarpan^seviye (× prestij indirimi).</summary>
    public static class UpgradeSystem
    {
        public static long Cost(UpgradeDef u, int level, double costMult)
        {
            double c = Math.Ceiling(u.BaseCost * Math.Pow(u.CostMult, level) * costMult);
            if (double.IsNaN(c) || double.IsInfinity(c) || c > 9.0e18) return long.MaxValue;
            return c < 1 ? 1 : (long)c;
        }

        /// <summary>Gereksinimleri sağlanmış ve azami seviyeye ulaşmamış mı?</summary>
        public static bool IsAvailable(GameConfig cfg, GameState st, UpgradeDef u)
        {
            if (st.Level(u.Id) >= u.MaxLevel) return false;
            if (st.UnlockedCatalog < u.MinCatalog) return false;
            if (!string.IsNullOrEmpty(u.Requires) && st.Level(u.Requires) < 1) return false;
            return true;
        }

        public static long NextCost(GameConfig cfg, GameState st, Stats stats, UpgradeDef u)
        {
            return Cost(u, st.Level(u.Id), stats.UpgradeCostMult);
        }

        public static bool NodeAvailable(GameConfig cfg, GameState st, PrestigeNodeDef n)
        {
            if (st.NodeLevel(n.Id) > 0) return false;
            if (!string.IsNullOrEmpty(n.Requires) && st.NodeLevel(n.Requires) < 1) return false;
            return true;
        }
    }
}
