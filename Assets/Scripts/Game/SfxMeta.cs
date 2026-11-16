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
            { "sfx/buy", new Info(-10.0f, 0.0f, 0.524f) },
            { "sfx/buy_1", new Info(-4.6f, 0.0f, 0.277f) },
            { "sfx/buy_2", new Info(3.0f, 0.0f, 0.537f) },
            { "sfx/coal_hit_1", new Info(3.7f, 0.0f, 0.5f) },
            { "sfx/coal_hit_2", new Info(3.8f, 0.0f, 0.196f) },
            { "sfx/coal_hit_3", new Info(3.1f, 0.0f, 0.132f) },
            { "sfx/crystal_hit", new Info(0.7f, 0.0f, 0.388f) },
            { "sfx/crystal_hit_1", new Info(2.5f, 0.0f, 0.473f) },
            { "sfx/crystal_hit_2", new Info(0.9f, 0.0f, 0.279f) },
            { "sfx/crystal_hit_3", new Info(2.8f, 0.0f, 0.5f) },
            { "sfx/dirt_hit", new Info(-3.1f, 0.13f, 0.5f) },
            { "sfx/dirt_hit_1", new Info(1.6f, 0.0f, 0.5f) },
            { "sfx/dirt_hit_2", new Info(-1.5f, 0.0f, 0.5f) },
            { "sfx/dirt_hit_3", new Info(-4.3f, 0.0f, 0.5f) },
            { "sfx/dirt_hit_4", new Info(-4.5f, 0.0f, 0.5f) },
            { "sfx/drill_loop", new Info(-1.5f, 0.0f, 0.0f) },
            { "sfx/drill_loop_2", new Info(-1.4f, 0.0f, 0.0f) },
            { "sfx/dynamite", new Info(1.7f, 0.0f, 1.246f) },
            { "sfx/dynamite_1", new Info(0.0f, 0.0f, 0.609f) },
            { "sfx/dynamite_2", new Info(3.8f, 0.0f, 0.249f) },
            { "sfx/error", new Info(-0.7f, 0.0f, 0.235f) },
            { "sfx/fanfare", new Info(-1.6f, 0.198f, 2.3f) },
            { "sfx/fuse", new Info(4.1f, 0.0f, 0.542f) },
            { "sfx/legend_find_1", new Info(-0.7f, 0.0f, 0.825f) },
            { "sfx/legend_find_2", new Info(2.1f, 0.0f, 1.251f) },
            { "sfx/menu_close", new Info(-1.7f, 0.0f, 0.8f) },
            { "sfx/menu_open", new Info(-1.8f, 0.0f, 0.8f) },
            { "sfx/metal_hit", new Info(1.7f, 0.0f, 0.5f) },
            { "sfx/metal_hit_1", new Info(3.8f, 0.0f, 0.129f) },
            { "sfx/metal_hit_2", new Info(-1.0f, 0.0f, 0.26f) },
            { "sfx/metal_hit_3", new Info(3.7f, 0.0f, 0.278f) },
            { "sfx/metal_hit_4", new Info(0.0f, 0.0f, 0.193f) },
            { "sfx/rare_find", new Info(4.5f, 0.0f, 1.5f) },
            { "sfx/rare_find_1", new Info(3.6f, 0.0f, 1.206f) },
            { "sfx/rare_find_2", new Info(1.7f, 0.0f, 1.317f) },
            { "sfx/rare_find_3", new Info(2.5f, 0.0f, 1.201f) },
            { "sfx/sell", new Info(-4.7f, 0.086f, 0.453f) },
            { "sfx/sell_1", new Info(-0.9f, 0.026f, 0.247f) },
            { "sfx/sell_2", new Info(-4.0f, 0.0f, 0.349f) },
            { "sfx/sell_3", new Info(-3.2f, 0.0f, 0.325f) },
            { "sfx/stone_hit", new Info(-0.3f, 0.0f, 0.464f) },
            { "sfx/stone_hit_1", new Info(4.2f, 0.0f, 0.425f) },
            { "sfx/stone_hit_2", new Info(3.9f, 0.0f, 0.5f) },
            { "sfx/stone_hit_3", new Info(3.9f, 0.0f, 0.5f) },
            { "sfx/stone_hit_4", new Info(4.1f, 0.0f, 0.396f) },
            { "sfx/upgrade", new Info(-10.0f, 0.0f, 0.445f) },
            { "ambient/layer_1", new Info(-9.3f, 0.0f, 0.0f) },
            { "ambient/layer_2", new Info(6f, 0.0f, 0.0f) },
            { "ambient/layer_3", new Info(-4.3f, 0.0f, 0.0f) },
            { "ambient/layer_4", new Info(-2.9f, 0.0f, 0.0f) },
            { "ambient/layer_5", new Info(-1.0f, 0.0f, 0.0f) },
            { "music/ambient1", new Info(3.7f, 0.0f, 0.0f) },
            { "music/ambient2", new Info(1.6f, 0.0f, 0.0f) },
            { "music/ambient3", new Info(-5.9f, 0.0f, 0.0f) },
            { "music/ambient4", new Info(5.4f, 0.0f, 0.0f) },
        };
    }
}
