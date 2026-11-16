using System.Collections.Generic;
using Maden.Core;

namespace Maden.Core.Tests
{
    public static class T
    {
        public const string SmallJson = @"{
 ""offlineCapSeconds"": 7200, ""autosaveSeconds"": 30, ""bagBase"": 3,
 ""critBase"": 0, ""critMultiplier"": 3, ""critCap"": 0.75, ""prestigeBonusPerRun"": 0.5, ""drillReportSeconds"": 6,
 ""layers"": [
  { ""id"": ""toprak"", ""name"": ""Toprak"", ""depthBlocks"": 3, ""blockHp"": 2, ""items"": [
     { ""name"": ""Çivi"", ""value"": 10, ""rarity"": ""common"", ""weight"": 90 },
     { ""name"": ""Düğme"", ""value"": 20, ""rarity"": ""uncommon"", ""weight"": 9 },
     { ""name"": ""Sikke"", ""value"": 100, ""rarity"": ""rare"", ""weight"": 1 } ] },
  { ""id"": ""kil"", ""name"": ""Kil"", ""depthBlocks"": 2, ""blockHp"": 2, ""items"": [
     { ""name"": ""Kil parçası"", ""value"": 30, ""rarity"": ""common"", ""weight"": 1 } ] },
  { ""id"": ""kadim"", ""name"": ""Kadim Hazine"", ""depthBlocks"": 2, ""blockHp"": 2, ""items"": [
     { ""name"": ""Kadim sikke"", ""value"": 50, ""rarity"": ""common"", ""weight"": 1 } ] }
 ],
 ""pickaxes"": [
  { ""name"": ""Tahta kazma"", ""power"": 1, ""cost"": 0 },
  { ""name"": ""Taş kazma"", ""power"": 2, ""cost"": 100 } ],
 ""upgrades"": [
  { ""id"": ""guc"", ""name"": ""Güçlü Kollar"", ""desc"": ""Güç artar"", ""baseCost"": 10, ""costMult"": 2, ""maxLevel"": 3, ""effectPerLevel"": 0.5, ""kind"": ""power"" },
  { ""id"": ""krit"", ""name"": ""Keskin Göz"", ""desc"": ""Kritik"", ""baseCost"": 10, ""costMult"": 2, ""maxLevel"": 3, ""effectPerLevel"": 0.1, ""kind"": ""crit"" },
  { ""id"": ""sans"", ""name"": ""Şans"", ""desc"": ""Nadir"", ""baseCost"": 10, ""costMult"": 2, ""maxLevel"": 3, ""effectPerLevel"": 1.0, ""kind"": ""rareChance"" },
  { ""id"": ""canta"", ""name"": ""Çanta"", ""desc"": ""Büyür"", ""baseCost"": 10, ""costMult"": 2, ""maxLevel"": 3, ""effectPerLevel"": 5, ""kind"": ""bag"" },
  { ""id"": ""oto"", ""name"": ""Otomatik Satış"", ""desc"": ""Satar"", ""baseCost"": 50, ""costMult"": 2, ""maxLevel"": 1, ""effectPerLevel"": 1, ""kind"": ""autosell"" },
  { ""id"": ""pazar"", ""name"": ""Pazarlık"", ""desc"": ""Gelir"", ""baseCost"": 10, ""costMult"": 2, ""maxLevel"": 3, ""effectPerLevel"": 0.5, ""kind"": ""sellBonus"" },
  { ""id"": ""dynamitePower"", ""name"": ""Dinamit Gücü"", ""desc"": ""Güç"", ""baseCost"": 10, ""costMult"": 2, ""maxLevel"": 3, ""effectPerLevel"": 1.0, ""kind"": ""dynamitePower"" },
  { ""id"": ""dynamiteSpeed"", ""name"": ""Dinamit Hızı"", ""desc"": ""Hız"", ""baseCost"": 10, ""costMult"": 2, ""maxLevel"": 3, ""effectPerLevel"": 1.0, ""kind"": ""dynamiteSpeed"" },
  { ""id"": ""gizli"", ""name"": ""Gizli"", ""desc"": ""Kil katmanında açılır"", ""baseCost"": 5, ""costMult"": 1, ""maxLevel"": 1, ""effectPerLevel"": 0.1, ""kind"": ""power"", ""minLayer"": 1 } ],
 ""machines"": [
  { ""id"": ""dynamite"", ""name"": ""Dinamit"", ""kind"": ""dynamite"", ""unlockCost"": 100, ""intervalSec"": 10, ""power"": 4 },
  { ""id"": ""drill"", ""name"": ""Matkap"", ""kind"": ""drill"", ""unlockCost"": 50, ""intervalSec"": 1, ""power"": 1 } ]
}";

        // Tek geniş katman: çevrimdışı testleri için.
        public const string BigJson = @"{
 ""offlineCapSeconds"": 7200, ""bagBase"": 100000000, ""critBase"": 0,
 ""layers"": [ { ""id"": ""a"", ""name"": ""Toprak"", ""depthBlocks"": 100000000, ""blockHp"": 1, ""items"": [
     { ""name"": ""Çivi"", ""value"": 1, ""rarity"": ""common"", ""weight"": 1 } ] } ],
 ""pickaxes"": [ { ""name"": ""Tahta kazma"", ""power"": 1, ""cost"": 0 } ],
 ""upgrades"": [],
 ""machines"": [ { ""id"": ""drill"", ""name"": ""Matkap"", ""kind"": ""drill"", ""unlockCost"": 0, ""intervalSec"": 1, ""power"": 1 } ]
}";

        public static EconomyConfig Small() { return EconomyConfig.FromJson(SmallJson); }
        public static EconomyConfig Big() { return EconomyConfig.FromJson(BigJson); }

        public static GameSession Session(double rnd = 0.5, GameState st = null, EconomyConfig cfg = null, List<GameEvent> log = null)
        {
            var s = new GameSession(cfg ?? Small(), st ?? new GameState(), new FixedRandom(rnd));
            if (log != null) s.OnEvent += e => log.Add(e);
            return s;
        }

        public static void Tap(GameSession s, int n) { for (int i = 0; i < n; i++) s.Tap(); }
    }
}
