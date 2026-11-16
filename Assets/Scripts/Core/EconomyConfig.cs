using System;
using System.Collections.Generic;
using System.IO;

namespace Maden.Core
{
    public sealed class ItemDef
    {
        public string Name;
        public double Value;
        public Rarity Rarity;
        public double Weight;
    }

    public sealed class LayerDef
    {
        public string Id;
        public string Name;
        public int DepthBlocks;
        public double BlockHp;
        public List<ItemDef> Items = new List<ItemDef>();
    }

    public sealed class PickaxeDef
    {
        public string Name;
        public double Power;
        public long Cost;
        public int MinLayer;
    }

    public sealed class UpgradeDef
    {
        public string Id;
        public string Name;
        public string Desc;
        public double BaseCost;
        public double CostMult;
        public int MaxLevel;
        public double EffectPerLevel;
        public string Kind;
        public int MinLayer;
    }

    public sealed class MachineDef
    {
        public string Id;
        public string Name;
        /// <summary>"dynamite" (patlama, MachineBlast) veya "drill" (MachineDrill).</summary>
        public string Kind;
        public long UnlockCost;
        public int MinLayer;
        public double IntervalSec;
        public double Power;
    }

    /// <summary>economy.json içeriği. Ekonomi sayıları yalnızca JSON'dadır.</summary>
    public sealed class EconomyConfig
    {
        public List<LayerDef> Layers = new List<LayerDef>();
        public List<PickaxeDef> Pickaxes = new List<PickaxeDef>();
        public List<UpgradeDef> Upgrades = new List<UpgradeDef>();
        public List<MachineDef> Machines = new List<MachineDef>();

        public double OfflineCapSeconds = 7200;
        public double AutosaveSeconds = 30;
        public int BagBase = 20;
        public double CritBase = 0.05;
        public double CritMultiplier = 3;
        public double CritCap = 0.75;
        public double PrestigeBonusPerRun = 0.5;
        public double DrillReportSeconds = 6;
        public string TreasureName = "Kadim hazine sandığı";

        public int TotalDepth
        {
            get
            {
                int t = 0;
                foreach (var l in Layers) t += l.DepthBlocks;
                return t;
            }
        }

        /// <summary>Verilen toplam blok sayısına karşılık gelen katman indeksi.</summary>
        public int LayerForBlocks(long blocks)
        {
            long acc = 0;
            for (int i = 0; i < Layers.Count; i++)
            {
                acc += Layers[i].DepthBlocks;
                if (blocks < acc) return i;
            }
            return Layers.Count - 1;
        }

        public UpgradeDef FindUpgrade(string id)
        {
            foreach (var u in Upgrades) if (u.Id == id) return u;
            return null;
        }

        public MachineDef FindMachine(string id)
        {
            foreach (var m in Machines) if (m.Id == id) return m;
            return null;
        }

        public static EconomyConfig LoadFromFile(string path)
        {
            return FromJson(File.ReadAllText(path));
        }

        public static EconomyConfig FromJson(string json)
        {
            object root = MiniJson.Parse(json);
            var d = MiniJson.AsObject(root);
            if (d == null) throw new FormatException("economy.json kök nesne olmalı.");
            var c = new EconomyConfig();
            c.OfflineCapSeconds = MiniJson.GetDouble(d, "offlineCapSeconds", c.OfflineCapSeconds);
            c.AutosaveSeconds = MiniJson.GetDouble(d, "autosaveSeconds", c.AutosaveSeconds);
            c.BagBase = MiniJson.GetInt(d, "bagBase", c.BagBase);
            c.CritBase = MiniJson.GetDouble(d, "critBase", c.CritBase);
            c.CritMultiplier = MiniJson.GetDouble(d, "critMultiplier", c.CritMultiplier);
            c.CritCap = MiniJson.GetDouble(d, "critCap", c.CritCap);
            c.PrestigeBonusPerRun = MiniJson.GetDouble(d, "prestigeBonusPerRun", c.PrestigeBonusPerRun);
            c.DrillReportSeconds = MiniJson.GetDouble(d, "drillReportSeconds", c.DrillReportSeconds);
            c.TreasureName = MiniJson.GetString(d, "treasureName", c.TreasureName);

            var layers = MiniJson.GetArray(d, "layers");
            if (layers == null || layers.Count == 0) throw new FormatException("layers eksik.");
            foreach (object lo in layers)
            {
                var ld = MiniJson.AsObject(lo);
                if (ld == null) throw new FormatException("layer nesne olmalı.");
                var l = new LayerDef();
                l.Id = MiniJson.GetString(ld, "id", "layer" + c.Layers.Count);
                l.Name = MiniJson.GetString(ld, "name", l.Id);
                l.DepthBlocks = Math.Max(1, MiniJson.GetInt(ld, "depthBlocks", 1));
                l.BlockHp = Math.Max(0.0001, MiniJson.GetDouble(ld, "blockHp", 1));
                var items = MiniJson.GetArray(ld, "items");
                if (items == null || items.Count == 0) throw new FormatException("Katman '" + l.Id + "' eşyasız.");
                foreach (object io in items)
                {
                    var id = MiniJson.AsObject(io);
                    var it = new ItemDef();
                    it.Name = MiniJson.GetString(id, "name", "?");
                    it.Value = Math.Max(0, MiniJson.GetDouble(id, "value", 1));
                    it.Weight = Math.Max(0, MiniJson.GetDouble(id, "weight", 1));
                    it.Rarity = ParseRarity(MiniJson.GetString(id, "rarity", "common"));
                    l.Items.Add(it);
                }
                c.Layers.Add(l);
            }

            var picks = MiniJson.GetArray(d, "pickaxes");
            if (picks == null || picks.Count == 0) throw new FormatException("pickaxes eksik.");
            foreach (object po in picks)
            {
                var pd = MiniJson.AsObject(po);
                var p = new PickaxeDef();
                p.Name = MiniJson.GetString(pd, "name", "Kazma");
                p.Power = MiniJson.GetDouble(pd, "power", 1);
                p.Cost = MiniJson.GetLong(pd, "cost", 0);
                p.MinLayer = MiniJson.GetInt(pd, "minLayer", 0);
                c.Pickaxes.Add(p);
            }

            var ups = MiniJson.GetArray(d, "upgrades");
            if (ups != null)
            {
                foreach (object uo in ups)
                {
                    var ud = MiniJson.AsObject(uo);
                    var u = new UpgradeDef();
                    u.Id = MiniJson.GetString(ud, "id", null);
                    if (string.IsNullOrEmpty(u.Id)) throw new FormatException("upgrade id eksik.");
                    u.Name = MiniJson.GetString(ud, "name", u.Id);
                    u.Desc = MiniJson.GetString(ud, "desc", "");
                    u.BaseCost = MiniJson.GetDouble(ud, "baseCost", 100);
                    u.CostMult = MiniJson.GetDouble(ud, "costMult", 1.5);
                    u.MaxLevel = Math.Max(1, MiniJson.GetInt(ud, "maxLevel", 10));
                    u.EffectPerLevel = MiniJson.GetDouble(ud, "effectPerLevel", 0.1);
                    u.Kind = MiniJson.GetString(ud, "kind", "power");
                    u.MinLayer = MiniJson.GetInt(ud, "minLayer", 0);
                    c.Upgrades.Add(u);
                }
            }

            var macs = MiniJson.GetArray(d, "machines");
            if (macs != null)
            {
                foreach (object mo in macs)
                {
                    var md = MiniJson.AsObject(mo);
                    var m = new MachineDef();
                    m.Id = MiniJson.GetString(md, "id", null);
                    if (string.IsNullOrEmpty(m.Id)) throw new FormatException("machine id eksik.");
                    m.Name = MiniJson.GetString(md, "name", m.Id);
                    m.Kind = MiniJson.GetString(md, "kind", "dynamite");
                    m.UnlockCost = MiniJson.GetLong(md, "unlockCost", 1000);
                    m.MinLayer = MiniJson.GetInt(md, "minLayer", 0);
                    m.IntervalSec = Math.Max(0.05, MiniJson.GetDouble(md, "intervalSec", 10));
                    m.Power = Math.Max(0, MiniJson.GetDouble(md, "power", 1));
                    c.Machines.Add(m);
                }
            }
            return c;
        }

        private static Rarity ParseRarity(string s)
        {
            if (s == "rare") return Rarity.Rare;
            if (s == "uncommon") return Rarity.Uncommon;
            return Rarity.Common;
        }
    }
}
