using System;
using System.Collections.Generic;

namespace Maden.Core
{
    public sealed class GameState
    {
        public const int CurrentVersion = 1;

        public int Version = CurrentVersion;
        public long Money;
        public long TotalBlocks;
        public int LayerIndex;
        /// <summary>Mevcut bloğun kalan dayanıklılığı; 0 = yeni blok.</summary>
        public double BlockHp;
        public int PickaxeTier;
        public Dictionary<string, int> UpgradeLevels = new Dictionary<string, int>();
        public Dictionary<string, double> MachineTimers = new Dictionary<string, double>(); // sahip olunan makine -> sayaç
        /// <summary>Çanta içeriği: "katman:eşya" -> adet.</summary>
        public Dictionary<string, int> Bag = new Dictionary<string, int>();
        public bool Finished;
        public int PrestigeCount;

        // İstatistikler
        public long TotalEarned;
        public long TotalTaps;
        public long TotalItems;
        public long RareCount;
        public double PlaySeconds;
        public long LastSaveUnix;

        private int bagCount;
        public int BagCount { get { return bagCount; } }

        public int GetLevel(string id)
        {
            int v;
            return UpgradeLevels.TryGetValue(id, out v) ? v : 0;
        }

        public bool OwnsMachine(string id) { return MachineTimers.ContainsKey(id); }

        public void AddToBag(string key, int n)
        {
            int v;
            Bag.TryGetValue(key, out v);
            Bag[key] = v + n;
            bagCount += n;
        }

        public void ClearBag()
        {
            Bag.Clear();
            bagCount = 0;
        }

        public void RecomputeBag()
        {
            int s = 0;
            foreach (var kv in Bag) s += kv.Value;
            bagCount = s;
        }

        public static string BagKey(int layer, int item) { return layer + ":" + item; }

        /// <summary>Bozuk/uyumsuz verileri config'e göre düzeltir.</summary>
        public void Sanitize(EconomyConfig cfg)
        {
            if (Money < 0) Money = 0;
            if (TotalBlocks < 0) TotalBlocks = 0;
            if (TotalBlocks > cfg.TotalDepth) TotalBlocks = cfg.TotalDepth;
            LayerIndex = cfg.LayerForBlocks(TotalBlocks);
            if (Finished) { LayerIndex = cfg.Layers.Count - 1; }
            if (double.IsNaN(BlockHp) || BlockHp < 0) BlockHp = 0;
            if (PickaxeTier < 0) PickaxeTier = 0;
            if (PickaxeTier >= cfg.Pickaxes.Count) PickaxeTier = cfg.Pickaxes.Count - 1;
            if (PrestigeCount < 0) PrestigeCount = 0;

            var lv = new Dictionary<string, int>();
            foreach (var kv in UpgradeLevels)
            {
                var u = cfg.FindUpgrade(kv.Key);
                if (u == null || kv.Value <= 0) continue;
                lv[kv.Key] = Math.Min(kv.Value, u.MaxLevel);
            }
            UpgradeLevels = lv;

            var mt = new Dictionary<string, double>();
            foreach (var kv in MachineTimers)
            {
                var m = cfg.FindMachine(kv.Key);
                if (m == null) continue;
                double t = kv.Value;
                if (double.IsNaN(t) || t < 0) t = 0;
                if (t > m.IntervalSec) t = m.IntervalSec;
                mt[kv.Key] = t;
            }
            MachineTimers = mt;

            var bag = new Dictionary<string, int>();
            foreach (var kv in Bag)
            {
                var parts = kv.Key.Split(':');
                int li, ii;
                if (parts.Length != 2 || !int.TryParse(parts[0], out li) || !int.TryParse(parts[1], out ii)) continue;
                if (li < 0 || li >= cfg.Layers.Count || ii < 0 || ii >= cfg.Layers[li].Items.Count) continue;
                if (kv.Value <= 0) continue;
                bag[kv.Key] = kv.Value;
            }
            Bag = bag;
            RecomputeBag();
            if (TotalItems < 0) TotalItems = 0;
            if (TotalTaps < 0) TotalTaps = 0;
            if (TotalEarned < 0) TotalEarned = 0;
            if (RareCount < 0) RareCount = 0;
            if (double.IsNaN(PlaySeconds) || PlaySeconds < 0) PlaySeconds = 0;
        }
    }
}
