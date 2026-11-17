using System;
using System.Collections.Generic;
using System.IO;

namespace Kazi.Core
{
    public static class SaveSystem
    {
        public static string Serialize(GameState s)
        {
            var d = new Dictionary<string, object>();
            d["version"] = s.Version;
            d["money"] = s.Money;
            d["upgrades"] = new Dictionary<string, int>(s.Upgrades);
            d["nodes"] = new Dictionary<string, int>(s.Nodes);
            d["catalog"] = s.UnlockedCatalog;
            if (s.Current != null) d["current"] = s.Current.ToDict();
            var bag = new List<object>(); foreach (var c in s.Bag) bag.Add(c.ToDict());
            d["bag"] = bag;
            d["lastType"] = s.LastTypeId;
            d["autoBuyOn"] = s.AutoBuyOn;
            d["autoDiscard"] = s.AutoDiscardTrap;
            d["autoType"] = s.AutoType;
            d["autoAccum"] = s.AutoAccum;
            d["pity"] = new Dictionary<string, int>(s.Pity);
            d["ab"] = s.AB; d["abTotal"] = s.ABTotal; d["prestige"] = s.PrestigeCount; d["offer"] = s.PrestigeOffer;
            d["runEarned"] = s.RunEarned; d["totalEarned"] = s.TotalEarned; d["totalCards"] = s.TotalCards;
            d["playSeconds"] = s.PlaySeconds; d["runSeconds"] = s.RunSeconds;
            d["rng"] = s.RngState.ToString();
            d["lastSaveUnix"] = s.LastSaveUnix;
            d["notify"] = s.NotifyAffordable;
            d["detail"] = (int)s.Detail; d["mode"] = (int)s.Mode;
            d["speechRate"] = s.SpeechRate;
            return MiniJson.Serialize(d);
        }

        /// <summary>Başarısızsa false döner ve state yeni oyundur; ASLA istisna fırlatmaz.</summary>
        public static bool TryDeserialize(string json, GameConfig cfg, out GameState state)
        {
            state = NewGame(cfg);
            try
            {
                if (string.IsNullOrWhiteSpace(json)) return false;
                var d = MiniJson.AsObject(MiniJson.Parse(json));
                if (d == null) return false;
                var s = new GameState();
                s.Version = MiniJson.GetInt(d, "version", GameState.CurrentVersion);
                s.Money = MiniJson.GetLong(d, "money", 0);
                ReadInts(MiniJson.GetObject(d, "upgrades"), s.Upgrades);
                ReadInts(MiniJson.GetObject(d, "nodes"), s.Nodes);
                s.UnlockedCatalog = MiniJson.GetInt(d, "catalog", 1);
                var cur = MiniJson.GetObject(d, "current");
                if (cur != null) s.Current = Card.FromDict(cfg, cur);
                var bag = MiniJson.GetArray(d, "bag");
                if (bag != null)
                    foreach (object o in bag)
                    {
                        var c = Card.FromDict(cfg, MiniJson.AsObject(o));
                        if (c != null && !c.Done) s.Bag.Add(c);
                    }
                s.LastTypeId = MiniJson.GetString(d, "lastType", "");
                s.AutoBuyOn = MiniJson.GetBool(d, "autoBuyOn", true);
                s.AutoDiscardTrap = MiniJson.GetBool(d, "autoDiscard", false);
                s.AutoType = MiniJson.GetString(d, "autoType", "");
                s.AutoAccum = MiniJson.GetDouble(d, "autoAccum", 0);
                ReadInts(MiniJson.GetObject(d, "pity"), s.Pity);
                s.AB = MiniJson.GetLong(d, "ab", 0); s.ABTotal = MiniJson.GetLong(d, "abTotal", 0);
                s.PrestigeCount = MiniJson.GetInt(d, "prestige", 0); s.PrestigeOffer = MiniJson.GetBool(d, "offer", false);
                s.RunEarned = MiniJson.GetLong(d, "runEarned", 0); s.TotalEarned = MiniJson.GetLong(d, "totalEarned", 0);
                s.TotalCards = MiniJson.GetLong(d, "totalCards", 0);
                s.PlaySeconds = MiniJson.GetDouble(d, "playSeconds", 0); s.RunSeconds = MiniJson.GetDouble(d, "runSeconds", 0);
                ulong rs;
                if (ulong.TryParse(MiniJson.GetString(d, "rng", ""), out rs)) s.RngState = rs;
                s.LastSaveUnix = MiniJson.GetLong(d, "lastSaveUnix", 0);
                s.NotifyAffordable = MiniJson.GetBool(d, "notify", true);
                s.Detail = (SpeechDetail)MiniJson.GetInt(d, "detail", (int)SpeechDetail.Short);
                s.Mode = (ScratchMode)MiniJson.GetInt(d, "mode", 0);
                s.SpeechRate = MiniJson.GetDouble(d, "speechRate", GameState.DefaultRate);
                s.Sanitize(cfg);
                state = s;
                return true;
            }
            catch (Exception)
            {
                state = NewGame(cfg);
                return false;
            }
        }

        private static void ReadInts(Dictionary<string, object> src, Dictionary<string, int> dst)
        {
            if (src == null) return;
            foreach (var kv in src)
                if (kv.Value is double)
                {
                    double v = (double)kv.Value;
                    if (double.IsNaN(v) || double.IsInfinity(v)) continue;
                    dst[kv.Key] = (int)Math.Max(-1, Math.Min(1000000, v));
                }
        }

        public static GameState NewGame(GameConfig cfg)
        {
            var s = new GameState();
            s.Money = cfg.Settings.StartMoney;
            return s;
        }

        public static GameState LoadOrNew(string json, GameConfig cfg)
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

        public static GameState LoadFromFile(string path, GameConfig cfg)
        {
            try { return File.Exists(path) ? LoadOrNew(File.ReadAllText(path), cfg) : NewGame(cfg); }
            catch (Exception) { return NewGame(cfg); }
        }
    }
}
