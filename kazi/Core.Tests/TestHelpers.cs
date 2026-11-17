using System;
using System.Collections.Generic;
using System.IO;
using Kazi.Core;

namespace Kazi.Core.Tests
{
    public static class T
    {
        private static GameConfig cfg;
        public static GameConfig Cfg
        {
            get
            {
                if (cfg == null) cfg = GameConfig.LoadDirectory(Path.Combine(AppContext.BaseDirectory, "data"));
                return cfg;
            }
        }

        public static GameSession NewSession(long money = 20, ulong seed = 42)
        {
            var st = SaveSystem.NewGame(Cfg);
            st.Money = money; st.RngState = seed;
            return new GameSession(Cfg, st);
        }

        public static List<GameEvent> Capture(GameSession s)
        {
            var l = new List<GameEvent>();
            s.OnEvent += e => l.Add(e);
            return l;
        }

        public static CardTypeDef Type(string id) { return Cfg.TypeById[id]; }

        /// <summary>Verilen sembol kimlikleriyle kart kurar.</summary>
        public static Card Make(string typeId, params string[] symbolIds)
        {
            var t = Type(typeId);
            var cells = new int[symbolIds.Length];
            for (int i = 0; i < cells.Length; i++) cells[i] = t.SymbolIndex(symbolIds[i]);
            return Card.FromCells(t, cells);
        }

        /// <summary>Çoklu çarpan kartı için: son hücre çarpan indeksi.</summary>
        public static Card MakeMul(string typeId, int mulIndex, params string[] symbolIds)
        {
            var t = Type(typeId);
            var cells = new int[symbolIds.Length + 1];
            for (int i = 0; i < symbolIds.Length; i++) cells[i] = t.SymbolIndex(symbolIds[i]);
            cells[symbolIds.Length] = mulIndex;
            return Card.FromCells(t, cells);
        }
    }
}
