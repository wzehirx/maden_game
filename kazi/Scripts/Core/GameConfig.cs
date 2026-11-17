using System;
using System.Collections.Generic;
using System.IO;

namespace Kazi.Core
{
    public enum CardRule { ThreeOfAKind, NumberHunt, Multiplier, Trap, Joker, Sequence, Jackpot, SonSans, AltinBilet }
    public enum SymbolKind { Normal, Trap, Joker, Gate }

    public sealed class SymbolDef
    {
        public string Id, Name;
        public double Weight;
        /// <summary>Ödeme çarpanı (bilet fiyatının katı). Tuzak sembollerinde ceza çarpanı.</summary>
        public double Pay;
        public SymbolKind Kind;
    }

    public sealed class MultiplierDef { public double Mult; public double Weight; }

    public sealed class CatalogDef { public int Id; public string Name; /** Bu kataloğun Son Şans kartının açılması için bu koşuda gereken toplam yükseltme seviyesi. */ public int GateLevels; }

    public sealed class CardTypeDef
    {
        public string Id, Name;
        public int Catalog;
        public CardRule Rule;
        public long Price;
        public int Cells, Cols;
        public double WinChance, MaxWinChance;
        public double DoubleChance;     // NumberHunt: ikinci eşleşme
        public double JokerChance;      // Joker: kazanan kartta joker ihtimali
        public double TrapChance, TrapDoubleChance, MaxTrapReduction;
        public int PityAfter;           // Son Şans: bu kadar kayıptan sonra garanti
        public List<SymbolDef> Symbols = new List<SymbolDef>();
        public List<MultiplierDef> Multipliers = new List<MultiplierDef>();

        public bool IsTrapType { get { return Rule == CardRule.Trap; } }
        public bool IsGate { get { return Rule == CardRule.SonSans || Rule == CardRule.AltinBilet; } }

        public int SymbolIndex(string id)
        {
            for (int i = 0; i < Symbols.Count; i++) if (Symbols[i].Id == id) return i;
            return -1;
        }
    }

    public enum EffectKind
    {
        ScratchPower, Luck, Payout, Discount, Bag, Offline, Refund,
        AutoUnlock, AutoSpeed, AutoQueue, AutoBuy, AutoTypes,
        Counter, Lens, Press,
        // yalnızca prestij düğümleri
        StartMoney, StartAuto, AbGain, UpgradeDiscount, StartCatalog, HelpMoney
    }

    public sealed class UpgradeDef
    {
        public string Id, Name, Desc, Category;
        public EffectKind Kind;
        public double BaseCost, CostMult, Effect;
        public int MaxLevel;
        public string Requires;
        public int MinCatalog;
    }

    public sealed class PrestigeNodeDef
    {
        public string Id, Name, Desc;
        public long Cost;
        public EffectKind Kind;
        public double Value;
        public string Requires;
    }

    public sealed class SettingsDef
    {
        public long HelpMoney = 10;
        public double DiscardRefundPct = 0.4;
        public double OfflineCapSeconds = 7200;
        public double AutosaveSeconds = 30;
        public long StartMoney = 20;
        public double OpenThreshold = 0.55, PeekThreshold = 0.25;
        public double LuckWinScale = 0.05, LuckSymbolScale = 0.02, LuckTrapScale = 0.03;
        public double[] RarityMults = { 2, 10, 50 };
        public double AutoBaseCpm = 6;
        public int BaseBag = 1;
        public double BaseOfflineSeconds = 600;
        public double MaxDiscount = 0.5;
        public double MaxRefund = 0.9;
        public double AffordableCooldownSeconds = 15;
        public double AutoReportSeconds = 60;
        public int AutoPeekCells = 3;
        public double AbBase = 5, AbRefEarned = 100000, AbExponent = 0.5;
        public double MinUpgradeCostMult = 0.3;
        public double AutoBufferFactor = 2;
    }

    /// <summary>Tüm sayılar JSON'dan gelir. Hatalı yapılandırmada FormatException fırlatır.</summary>
    public sealed class GameConfig
    {
        public List<CatalogDef> Catalogs = new List<CatalogDef>();
        public List<CardTypeDef> CardTypes = new List<CardTypeDef>();
        public Dictionary<string, CardTypeDef> TypeById = new Dictionary<string, CardTypeDef>();
        public List<UpgradeDef> Upgrades = new List<UpgradeDef>();
        public Dictionary<string, UpgradeDef> UpgradeById = new Dictionary<string, UpgradeDef>();
        public List<PrestigeNodeDef> Nodes = new List<PrestigeNodeDef>();
        public Dictionary<string, PrestigeNodeDef> NodeById = new Dictionary<string, PrestigeNodeDef>();
        public SettingsDef Settings = new SettingsDef();

        public int CatalogCount { get { return Catalogs.Count; } }

        public static GameConfig LoadDirectory(string dir)
        {
            return FromJson(
                File.ReadAllText(Path.Combine(dir, "cards.json")),
                File.ReadAllText(Path.Combine(dir, "upgrades.json")),
                File.ReadAllText(Path.Combine(dir, "prestige.json")),
                File.ReadAllText(Path.Combine(dir, "settings.json")));
        }

        public static GameConfig FromJson(string cardsJson, string upgradesJson, string prestigeJson, string settingsJson)
        {
            var c = new GameConfig();
            c.ParseSettings(MiniJson.AsObject(MiniJson.Parse(settingsJson)));
            c.ParseCards(MiniJson.AsObject(MiniJson.Parse(cardsJson)));
            c.ParseUpgrades(MiniJson.AsObject(MiniJson.Parse(upgradesJson)));
            c.ParsePrestige(MiniJson.AsObject(MiniJson.Parse(prestigeJson)));
            return c;
        }

        private static T ParseEnum<T>(string s, string what) where T : struct
        {
            T v;
            if (!Enum.TryParse(s, true, out v)) throw new FormatException("Bilinmeyen " + what + ": " + s);
            return v;
        }

        private void ParseSettings(Dictionary<string, object> d)
        {
            if (d == null) throw new FormatException("settings.json geçersiz.");
            var s = Settings;
            s.HelpMoney = MiniJson.GetLong(d, "helpMoney", s.HelpMoney);
            s.DiscardRefundPct = MiniJson.GetDouble(d, "discardRefundPct", s.DiscardRefundPct);
            s.OfflineCapSeconds = MiniJson.GetDouble(d, "offlineCapSeconds", s.OfflineCapSeconds);
            s.AutosaveSeconds = MiniJson.GetDouble(d, "autosaveSeconds", s.AutosaveSeconds);
            s.StartMoney = MiniJson.GetLong(d, "startMoney", s.StartMoney);
            var th = MiniJson.GetObject(d, "scratchThresholds");
            if (th != null)
            {
                s.OpenThreshold = MiniJson.GetDouble(th, "open", s.OpenThreshold);
                s.PeekThreshold = MiniJson.GetDouble(th, "peek", s.PeekThreshold);
            }
            s.LuckWinScale = MiniJson.GetDouble(d, "luckWinScale", s.LuckWinScale);
            s.LuckSymbolScale = MiniJson.GetDouble(d, "luckSymbolScale", s.LuckSymbolScale);
            s.LuckTrapScale = MiniJson.GetDouble(d, "luckTrapScale", s.LuckTrapScale);
            var rm = MiniJson.GetArray(d, "rarityMults");
            if (rm != null && rm.Count == 3) s.RarityMults = new[] { (double)rm[0], (double)rm[1], (double)rm[2] };
            s.AutoBaseCpm = MiniJson.GetDouble(d, "autoBaseCpm", s.AutoBaseCpm);
            s.BaseBag = MiniJson.GetInt(d, "baseBag", s.BaseBag);
            s.BaseOfflineSeconds = MiniJson.GetDouble(d, "baseOfflineSeconds", s.BaseOfflineSeconds);
            s.MaxDiscount = MiniJson.GetDouble(d, "maxDiscount", s.MaxDiscount);
            s.MaxRefund = MiniJson.GetDouble(d, "maxRefund", s.MaxRefund);
            s.AffordableCooldownSeconds = MiniJson.GetDouble(d, "affordableCooldownSeconds", s.AffordableCooldownSeconds);
            s.AutoReportSeconds = MiniJson.GetDouble(d, "autoReportSeconds", s.AutoReportSeconds);
            s.AutoPeekCells = MiniJson.GetInt(d, "autoPeekCells", s.AutoPeekCells);
            s.AbBase = MiniJson.GetDouble(d, "abBase", s.AbBase);
            s.AbRefEarned = MiniJson.GetDouble(d, "abRefEarned", s.AbRefEarned);
            s.AbExponent = MiniJson.GetDouble(d, "abExponent", s.AbExponent);
            s.MinUpgradeCostMult = MiniJson.GetDouble(d, "minUpgradeCostMult", s.MinUpgradeCostMult);
            s.AutoBufferFactor = MiniJson.GetDouble(d, "autoBufferFactor", s.AutoBufferFactor);
        }

        private void ParseCards(Dictionary<string, object> d)
        {
            if (d == null) throw new FormatException("cards.json geçersiz.");
            var cats = MiniJson.GetArray(d, "catalogs");
            if (cats == null || cats.Count == 0) throw new FormatException("katalog yok.");
            foreach (object o in cats)
            {
                var co = MiniJson.AsObject(o);
                Catalogs.Add(new CatalogDef { Id = MiniJson.GetInt(co, "id", Catalogs.Count + 1), Name = MiniJson.GetString(co, "name", "Katalog"), GateLevels = MiniJson.GetInt(co, "gateLevels", 0) });
            }
            var types = MiniJson.GetArray(d, "cardTypes");
            if (types == null || types.Count == 0) throw new FormatException("kart türü yok.");
            foreach (object o in types)
            {
                var to = MiniJson.AsObject(o);
                var t = new CardTypeDef();
                t.Id = MiniJson.GetString(to, "id", null);
                if (string.IsNullOrEmpty(t.Id)) throw new FormatException("kart id yok.");
                if (TypeById.ContainsKey(t.Id)) throw new FormatException("yinelenen kart id: " + t.Id);
                t.Name = MiniJson.GetString(to, "name", t.Id);
                t.Catalog = MiniJson.GetInt(to, "catalog", 1);
                t.Rule = ParseEnum<CardRule>(MiniJson.GetString(to, "rule", ""), "kural");
                t.Price = MiniJson.GetLong(to, "price", 10);
                t.Cells = MiniJson.GetInt(to, "cells", DefaultCells(t.Rule));
                t.Cols = MiniJson.GetInt(to, "cols", 3);
                t.WinChance = MiniJson.GetDouble(to, "winChance", 0.3);
                t.MaxWinChance = MiniJson.GetDouble(to, "maxWinChance", 0.8);
                t.DoubleChance = MiniJson.GetDouble(to, "doubleChance", 0);
                t.JokerChance = MiniJson.GetDouble(to, "jokerChance", 0);
                t.TrapChance = MiniJson.GetDouble(to, "trapChance", 0);
                t.TrapDoubleChance = MiniJson.GetDouble(to, "trapDoubleChance", 0);
                t.MaxTrapReduction = MiniJson.GetDouble(to, "maxTrapReduction", 0.5);
                t.PityAfter = MiniJson.GetInt(to, "pityAfter", 0);
                var syms = MiniJson.GetArray(to, "symbols");
                if (syms != null)
                    foreach (object so in syms)
                    {
                        var sd = MiniJson.AsObject(so);
                        var sy = new SymbolDef
                        {
                            Id = MiniJson.GetString(sd, "id", ""),
                            Name = MiniJson.GetString(sd, "name", ""),
                            Weight = MiniJson.GetDouble(sd, "weight", 1),
                            Pay = MiniJson.GetDouble(sd, "pay", 0),
                            Kind = ParseEnum<SymbolKind>(MiniJson.GetString(sd, "kind", "Normal"), "sembol türü")
                        };
                        t.Symbols.Add(sy);
                    }
                var mls = MiniJson.GetArray(to, "multipliers");
                if (mls != null)
                    foreach (object mo in mls)
                    {
                        var md = MiniJson.AsObject(mo);
                        t.Multipliers.Add(new MultiplierDef { Mult = MiniJson.GetDouble(md, "mult", 1), Weight = MiniJson.GetDouble(md, "weight", 1) });
                    }
                ValidateType(t);
                CardTypes.Add(t);
                TypeById[t.Id] = t;
            }
        }

        public static int DefaultCells(CardRule r)
        {
            switch (r)
            {
                case CardRule.NumberHunt: return 9;
                case CardRule.Trap: return 9;
                case CardRule.Sequence: return 9;
                case CardRule.Multiplier: return 7;
                default: return 6;
            }
        }

        private static int Count(CardTypeDef t, SymbolKind k) { int n = 0; foreach (var s in t.Symbols) if (s.Kind == k) n++; return n; }

        private void ValidateType(CardTypeDef t)
        {
            if (t.Price <= 0) throw new FormatException(t.Id + ": fiyat > 0 olmalı.");
            if (t.Cells != DefaultCells(t.Rule)) throw new FormatException(t.Id + ": hücre sayısı kurala uymuyor.");
            if (t.Catalog < 1 || t.Catalog > Catalogs.Count) throw new FormatException(t.Id + ": katalog geçersiz.");
            int normal = Count(t, SymbolKind.Normal);
            if (Count(t, SymbolKind.Normal) + Count(t, SymbolKind.Gate) < 4) throw new FormatException(t.Id + ": en az 4 sembol gerekir.");
            if (t.WinChance < 0 || t.WinChance > 1 || t.MaxWinChance < t.WinChance || t.MaxWinChance > 1) throw new FormatException(t.Id + ": winChance geçersiz.");
            if (t.Rule == CardRule.Trap && Count(t, SymbolKind.Trap) == 0) throw new FormatException(t.Id + ": tuzak sembolü yok.");
            if (t.Rule == CardRule.Joker && Count(t, SymbolKind.Joker) != 1) throw new FormatException(t.Id + ": tam bir joker sembolü gerekir.");
            if (t.Rule == CardRule.Joker && normal < 6) throw new FormatException(t.Id + ": joker kartı en az 6 normal sembol ister.");
            if (t.Rule == CardRule.Trap && normal < 5) throw new FormatException(t.Id + ": tuzak kartı en az 5 normal sembol ister.");
            if (t.Rule == CardRule.Sequence && normal < 4) throw new FormatException(t.Id + ": sıra kartı en az 4 sembol ister.");
            if (t.Rule == CardRule.NumberHunt && normal < 5) throw new FormatException(t.Id + ": sayı avı en az 5 sayı ister.");
            if (t.Rule == CardRule.Multiplier && t.Multipliers.Count == 0) throw new FormatException(t.Id + ": çarpan listesi boş.");
            if (t.IsGate && Count(t, SymbolKind.Gate) != 1) throw new FormatException(t.Id + ": tam bir kapı sembolü gerekir.");
            bool anyPay = false;
            foreach (var s in t.Symbols) if (s.Kind != SymbolKind.Trap && s.Pay > 0) anyPay = true;
            if (!anyPay) throw new FormatException(t.Id + ": ödeyen sembol yok.");
        }

        private void ParseUpgrades(Dictionary<string, object> d)
        {
            var arr = MiniJson.GetArray(d, "upgrades");
            if (arr == null) throw new FormatException("upgrades.json geçersiz.");
            foreach (object o in arr)
            {
                var u = MiniJson.AsObject(o);
                var ud = new UpgradeDef
                {
                    Id = MiniJson.GetString(u, "id", null),
                    Name = MiniJson.GetString(u, "name", ""),
                    Desc = MiniJson.GetString(u, "desc", ""),
                    Category = MiniJson.GetString(u, "category", "scratch"),
                    Kind = ParseEnum<EffectKind>(MiniJson.GetString(u, "kind", ""), "etki"),
                    BaseCost = MiniJson.GetDouble(u, "baseCost", 10),
                    CostMult = MiniJson.GetDouble(u, "costMult", 1.5),
                    Effect = MiniJson.GetDouble(u, "effect", 1),
                    MaxLevel = MiniJson.GetInt(u, "maxLevel", 1),
                    Requires = MiniJson.GetString(u, "requires", ""),
                    MinCatalog = MiniJson.GetInt(u, "minCatalog", 1)
                };
                if (string.IsNullOrEmpty(ud.Id) || UpgradeById.ContainsKey(ud.Id)) throw new FormatException("yükseltme id geçersiz/yinelenen.");
                Upgrades.Add(ud);
                UpgradeById[ud.Id] = ud;
            }
            foreach (var ud in Upgrades)
                if (!string.IsNullOrEmpty(ud.Requires) && !UpgradeById.ContainsKey(ud.Requires))
                    throw new FormatException(ud.Id + ": bilinmeyen gereksinim " + ud.Requires);
        }

        private void ParsePrestige(Dictionary<string, object> d)
        {
            var arr = MiniJson.GetArray(d, "nodes");
            if (arr == null) throw new FormatException("prestige.json geçersiz.");
            foreach (object o in arr)
            {
                var u = MiniJson.AsObject(o);
                var n = new PrestigeNodeDef
                {
                    Id = MiniJson.GetString(u, "id", null),
                    Name = MiniJson.GetString(u, "name", ""),
                    Desc = MiniJson.GetString(u, "desc", ""),
                    Cost = MiniJson.GetLong(u, "cost", 1),
                    Kind = ParseEnum<EffectKind>(MiniJson.GetString(u, "kind", ""), "etki"),
                    Value = MiniJson.GetDouble(u, "value", 1),
                    Requires = MiniJson.GetString(u, "requires", "")
                };
                if (string.IsNullOrEmpty(n.Id) || NodeById.ContainsKey(n.Id)) throw new FormatException("düğüm id geçersiz/yinelenen.");
                Nodes.Add(n);
                NodeById[n.Id] = n;
            }
            foreach (var n in Nodes)
                if (!string.IsNullOrEmpty(n.Requires) && !NodeById.ContainsKey(n.Requires))
                    throw new FormatException(n.Id + ": bilinmeyen gereksinim " + n.Requires);
        }

        public CardTypeDef CheapestInCatalogs(int maxCatalog)
        {
            CardTypeDef best = null;
            foreach (var t in CardTypes)
                if (t.Catalog <= maxCatalog && !t.IsGate && (best == null || t.Price < best.Price)) best = t;
            return best;
        }

        public CardTypeDef GateOfCatalog(int catalog)
        {
            foreach (var t in CardTypes) if (t.IsGate && t.Catalog == catalog) return t;
            return null;
        }
    }
}
