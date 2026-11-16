using System;
using System.Collections.Generic;
using System.IO;

namespace Maden.Core
{
    public static class SaveSystem
    {
        public static string Serialize(GameState s)
        {
            var d = new Dictionary<string, object>();
            d["version"] = s.Version;
            d["money"] = s.Money;
            d["totalBlocks"] = s.TotalBlocks;
            d["layerIndex"] = s.LayerIndex;
            d["blockHp"] = s.BlockHp;
            d["pickaxeTier"] = s.PickaxeTier;
            d["upgradeLevels"] = new Dictionary<string, int>(s.UpgradeLevels);
            d["machineTimers"] = new Dictionary<string, double>(s.MachineTimers);
            d["bag"] = new Dictionary<string, int>(s.Bag);
            d["finished"] = s.Finished;
            d["prestige"] = s.PrestigeCount;
            d["totalEarned"] = s.TotalEarned;
            d["totalTaps"] = s.TotalTaps;
            d["totalItems"] = s.TotalItems;
            d["rareCount"] = s.RareCount;
            d["playSeconds"] = s.PlaySeconds;
            d["lastSaveUnix"] = s.LastSaveUnix;
            return MiniJson.Serialize(d);
        }

        /// <summary>Başarısızsa false döner ve state yeni bir oyun olur; asla istisna fırlatmaz.</summary>
        public static bool TryDeserialize(string json, EconomyConfig cfg, out GameState state)
        {
            state = new GameState();
            try
            {
                if (string.IsNullOrWhiteSpace(json)) return false;
                var d = MiniJson.AsObject(MiniJson.Parse(json));
                if (d == null) return false;
                var s = new GameState();
                s.Version = MiniJson.GetInt(d, "version", GameState.CurrentVersion);
                s.Money = MiniJson.GetLong(d, "money", 0);
                s.TotalBlocks = MiniJson.GetLong(d, "totalBlocks", 0);
                s.LayerIndex = MiniJson.GetInt(d, "layerIndex", 0);
                s.BlockHp = MiniJson.GetDouble(d, "blockHp", 0);
                s.PickaxeTier = MiniJson.GetInt(d, "pickaxeTier", 0);
                s.Finished = MiniJson.GetBool(d, "finished", false);
                s.PrestigeCount = MiniJson.GetInt(d, "prestige", 0);
                s.TotalEarned = MiniJson.GetLong(d, "totalEarned", 0);
                s.TotalTaps = MiniJson.GetLong(d, "totalTaps", 0);
                s.TotalItems = MiniJson.GetLong(d, "totalItems", 0);
                s.RareCount = MiniJson.GetLong(d, "rareCount", 0);
                s.PlaySeconds = MiniJson.GetDouble(d, "playSeconds", 0);
                s.LastSaveUnix = MiniJson.GetLong(d, "lastSaveUnix", 0);
                var ul = MiniJson.GetObject(d, "upgradeLevels");
                if (ul != null) foreach (var kv in ul) if (kv.Value is double) s.UpgradeLevels[kv.Key] = (int)(double)kv.Value;
                var mt = MiniJson.GetObject(d, "machineTimers");
                if (mt != null) foreach (var kv in mt) if (kv.Value is double) s.MachineTimers[kv.Key] = (double)kv.Value;
                var bag = MiniJson.GetObject(d, "bag");
                if (bag != null) foreach (var kv in bag) if (kv.Value is double) s.Bag[kv.Key] = (int)(double)kv.Value;
                s.Sanitize(cfg);
                state = s;
                return true;
            }
            catch (Exception)
            {
                state = new GameState();
                return false;
            }
        }

        public static GameState LoadOrNew(string json, EconomyConfig cfg)
        {
            GameState s;
            TryDeserialize(json, cfg, out s);
            return s;
        }

        public static void SaveToFile(GameState s, string path)
        {
            string tmp = path + ".tmp";
            File.WriteAllText(tmp, Serialize(s));
            if (File.Exists(path)) File.Delete(path);
            File.Move(tmp, path);
        }

        public static GameState LoadFromFile(string path, EconomyConfig cfg)
        {
            try { return File.Exists(path) ? LoadOrNew(File.ReadAllText(path), cfg) : new GameState(); }
            catch (Exception) { return new GameState(); }
        }
    }
}
