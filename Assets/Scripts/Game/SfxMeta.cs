// OTOMATIK URETILDI: tools/gen_sfx_meta.py (ffmpeg olcumu). Elle degistirme; scripti yeniden calistir.
// gain_db: seviye dengeleme; start/end: bas sessizlik ve kuyruk kirpma (saniye, 0 = dokunma).
using System.Collections.Generic;

namespace Maden.Game
{
    public static class SfxMeta
    {
        public readonly struct Info { public readonly float GainDb, Start, End; public Info(float g, float s, float e) { GainDb = g; Start = s; End = e; } }
        public static readonly Dictionary<string, Info> Table = new Dictionary<string, Info>
        {
            { "sfx/buy_1", new Info(3.5f, 0.0f, 0.757f) },
            { "sfx/buy_2", new Info(3.3f, 0.0f, 0.8f) },
            { "sfx/coal_hit_1", new Info(3.5f, 0.0f, 0.5f) },
            { "sfx/coal_hit_2", new Info(0.9f, 0.0f, 0.5f) },
            { "sfx/coal_hit_3", new Info(3.0f, 0.0f, 0.5f) },
            { "sfx/crystal_hit_1", new Info(3.7f, 0.0f, 0.5f) },
            { "sfx/crystal_hit_2", new Info(3.4f, 0.0f, 0.5f) },
            { "sfx/crystal_hit_3", new Info(3.6f, 0.0f, 0.5f) },
            { "sfx/dirt_hit_1", new Info(0.8f, 0.0f, 0.5f) },
            { "sfx/dirt_hit_2", new Info(0.3f, 0.0f, 0.5f) },
            { "sfx/dirt_hit_3", new Info(3.8f, 0.0f, 0.5f) },
            { "sfx/dirt_hit_4", new Info(2.1f, 0.0f, 0.5f) },
            { "sfx/drill_loop", new Info(-0.4f, 0.0f, 0.0f) },
            { "sfx/drill_loop_2", new Info(-2.0f, 0.0f, 0.0f) },
            { "sfx/dynamite_1", new Info(0.1f, 0.0f, 1.5f) },
            { "sfx/dynamite_2", new Info(-0.4f, 0.0f, 1.5f) },
            { "sfx/error", new Info(3.0f, 0.0f, 0.383f) },
            { "sfx/fanfare", new Info(3.6f, 0.0f, 2.3f) },
            { "sfx/fuse", new Info(3.4f, 0.0f, 0.8f) },
            { "sfx/legend_find_1", new Info(2.2f, 0.0f, 1.5f) },
            { "sfx/legend_find_2", new Info(-1.0f, 0.0f, 1.5f) },
            { "sfx/menu_close", new Info(3.5f, 0.0f, 0.703f) },
            { "sfx/menu_open", new Info(1.5f, 0.0f, 0.8f) },
            { "sfx/metal_hit_1", new Info(3.4f, 0.0f, 0.5f) },
            { "sfx/metal_hit_2", new Info(1.9f, 0.0f, 0.479f) },
            { "sfx/metal_hit_3", new Info(-1.8f, 0.0f, 0.429f) },
            { "sfx/metal_hit_4", new Info(-2.1f, 0.0f, 0.5f) },
            { "sfx/rare_find_1", new Info(0.8f, 0.0f, 1.5f) },
            { "sfx/rare_find_2", new Info(3.4f, 0.0f, 1.282f) },
            { "sfx/rare_find_3", new Info(3.6f, 0.0f, 1.061f) },
            { "sfx/sell_1", new Info(2.8f, 0.0f, 0.773f) },
            { "sfx/sell_2", new Info(3.1f, 0.0f, 0.8f) },
            { "sfx/sell_3", new Info(3.5f, 0.0f, 0.765f) },
            { "sfx/stone_hit_1", new Info(2.1f, 0.0f, 0.5f) },
            { "sfx/stone_hit_2", new Info(3.6f, 0.0f, 0.5f) },
            { "sfx/stone_hit_3", new Info(-2.8f, 0.0f, 0.5f) },
            { "sfx/stone_hit_4", new Info(3.4f, 0.0f, 0.5f) },
            { "sfx/upgrade", new Info(3.2f, 0.0f, 1.003f) },
            { "ambient/layer_1", new Info(-2.1f, 0.0f, 0.0f) },
            { "ambient/layer_2", new Info(6f, 0.0f, 0.0f) },
            { "ambient/layer_3", new Info(-4.3f, 0.0f, 0.0f) },
            { "ambient/layer_4", new Info(-0.3f, 0.0f, 0.0f) },
            { "ambient/layer_5", new Info(-0.6f, 0.0f, 0.0f) },
            { "music/ambient1", new Info(3.7f, 0.0f, 0.0f) },
            { "music/ambient2", new Info(1.6f, 0.0f, 0.0f) },
            { "music/ambient3", new Info(-5.9f, 0.0f, 0.0f) },
            { "music/ambient4", new Info(5.4f, 0.0f, 0.0f) },
        };
    }
}
