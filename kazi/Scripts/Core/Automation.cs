using System;

namespace Kazi.Core
{
    /// <summary>Otomatik kazıyıcı: çantadaki kartları hızla (kart/dk) kazır; otomatik alım çantayı doldurur; gadget'lar hız/parti/şans/ödeme verir.</summary>
    public sealed class Automation
    {
        private readonly GameSession s;
        public Automation(GameSession session) { s = session; }

        /// <summary>Otomatik alım için hedef tür: seçili tür geçerliyse o, değilse açık kataloglardaki en ucuz kart.</summary>
        public CardTypeDef TargetType()
        {
            var cfg = s.Config; var st = s.State; var stats = s.Stats;
            CardTypeDef t;
            if (stats.AutoTypeLevel > 0 && st.AutoType != "" && cfg.TypeById.TryGetValue(st.AutoType, out t)
                && t.Catalog <= Math.Min(st.UnlockedCatalog, stats.AutoTypeLevel)) return t;
            // Tür seçici yokken: açık kataloglardaki en çok kazandıran, bilet bedeli bakiyenin yarısını aşmayan tür.
            return Economy.BestType(cfg, stats, st.UnlockedCatalog, st.Money, 0.5);
        }

        public int BufferTarget()
        {
            var stats = s.Stats;
            int target = (int)Math.Ceiling(stats.Batch * s.Config.Settings.AutoBufferFactor);
            return Math.Max(1, Math.Min(stats.BagCap, Math.Max(stats.Batch, target)));
        }

        /// <summary>Zamanı ilerletir; kaç kart işlendiğini döner.</summary>
        public int Advance(double dt)
        {
            var st = s.State; var stats = s.Stats;
            if (!stats.AutoOwned || dt <= 0) return 0;
            st.AutoAccum += dt * stats.AutoCpm / 60.0;
            int total = 0, guard = 0;
            while (st.AutoAccum >= 1.0 && guard++ < 2000000)
            {
                int did = Cycle();
                if (did == 0) { st.AutoAccum = Math.Min(st.AutoAccum, 1.0); break; }
                total += did;
                st.AutoAccum -= 1.0;
            }
            if (st.AutoAccum > 1.0 && guard >= 2000000) st.AutoAccum = 1.0;
            return total;
        }

        private int Cycle()
        {
            var st = s.State; var stats = s.Stats;
            int done = 0;
            for (int k = 0; k < stats.Batch; k++)
            {
                if (st.AutoBuyOn && stats.AutoBuyOwned) TopUp();
                if (st.Bag.Count == 0) break;
                var c = st.Bag[0];
                st.Bag.RemoveAt(0);
                Process(c);
                done++;
            }
            return done;
        }

        private void TopUp()
        {
            var st = s.State;
            int target = BufferTarget();
            var t = TargetType();
            if (t == null) return;
            while (st.Bag.Count < target)
            {
                if (s.AutoBuyInto(t) != null) continue;
                // seçili tür pahalı kaldıysa en ucuz kartla devam et (oyuncu tıkanmasın)
                if (st.Bag.Count == 0)
                {
                    var cheap = s.Config.CheapestInCatalogs(st.UnlockedCatalog);
                    if (cheap != null && cheap != t && s.AutoBuyInto(cheap) != null) continue;
                }
                break;
            }
        }

        private void Process(Card c)
        {
            var st = s.State; var stats = s.Stats;
            if (st.AutoDiscardTrap && c.Type.IsTrapType)
            {
                c.ScratchSequential(s.Config.Settings.AutoPeekCells + stats.Lens);
                if (c.TrapRevealed()) { s.DoDiscard(c); s.CountAutoDiscard(); return; }
            }
            c.OpenAll();
            s.ApplyResult(c, true);
        }
    }
}
