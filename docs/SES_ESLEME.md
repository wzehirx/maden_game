# Ses eşleme tablosu (olay -> dosya)

Bu tablo, oyunda hangi olayda hangi ses dosyasının çaldığını gösterir. "Şu ses garip" dediğinde olayı bul, sağdaki dosya
adlarını (varyasyonlar rastgele seçilir) söyle; o dosyayı değiştiririz/sileriz/kısarız. Dosyalar:
`Assets/Resources/Audio/` (sfx, ambient, music). Kaynak ve lisanslar: `CREDITS.md`.

Dosya seçimi bu turda DEĞİŞTİRİLMEDİ; yalnızca çalma biçimi (seviye, süre sınırı, perde) düzeltildi.

## Genel seviye ve çalma kuralları (kod: `Assets/Scripts/Game/SoundPlayer.cs`)

- Kaydırıcı varsayılanlarında: **müzik veri yolu -18 dB**, **ortam (ambiyans) -20 dB**, **efekt -2 dB** (Efekt sesi %80).
  Müzik kaydırıcısı: adım 5 = -18 dB, adım 10 = -12 dB, adım 0 = kapalı. Ortam, Efekt sesi kaydırıcısına bağlıdır.
- Her dosyaya ffmpeg ölçümüyle **seviye düzeltmesi** uygulanır (tablolar aşağıda); tüm dosyalar benzer ses şiddetine gelir.
  Ölçümler `tools/gen_sfx_meta.py` ile üretilir, sonuç `Assets/Scripts/Game/SfxMeta.cs` içindedir.
- **Rastgele perde**: tüm efektlerde en çok ±%5 (eskiden vuruşlarda 0.92-1.27 arası kayıyordu). Kazma kademesi perdeyi
  kademe başına +%1 (en çok +%6) yükseltir; kritik vuruş ek olarak x1.08.
- **Süre sınırı + sönme**: vuruşlar en çok 0.5 sn, arayüz sesleri (sat, al, hata, menü) en çok 0.8 sn, bulgu/yükseltme/dinamit
  en çok 1.5 sn, fanfar 2.3 sn; sonunda 80 ms'lik sönme (çıtlama yok). Dosyaların başındaki sessizlik (>=20 ms) atlanır.
- **Polifoni**: vuruşlar için 4, diğer efektler için 5 oyuncu (ayrı havuz); doluysa en eski ses devralınır.
- Ana çıkışta **sınırlayıcı** (limiter, tavan -1 dB) üst üste binen seslerin kırpılmasını önler.
- Konuşma sırasında müzik/ortam **-7 dB** kısılır: yavaş iniş (14 dB/sn), 0.5 sn bekleme, yavaş geri çıkış (5 dB/sn).

## Olay -> dosya

| Olay | Çalan dosya(lar) | Seviye / not |
|---|---|---|
| Dokunuş: kazma vuruşu, katman 0-1 (toprak, kil) | `sfx/dirt_hit`, `dirt_hit_1`, `_2`, `_3`, `_4` (rastgele) | vuruş havuzu, 0.5 sn sınır, perde ±%5 |
| Vuruş, katman 2 (taş) | `sfx/stone_hit`, `stone_hit_1` .. `_4` | aynı |
| Vuruş, katman 3 (kömür) | `sfx/coal_hit_1`, `_2`, `_3` | aynı |
| Vuruş, katman 4-6 (demir, gümüş, altın) | `sfx/metal_hit`, `metal_hit_1` .. `_4` | aynı |
| Vuruş, katman 7-9 (kristal, elmas, kadim) | `sfx/crystal_hit`, `crystal_hit_1` .. `_3` | aynı |
| Kritik vuruş | yukarıdaki vuruş sesi, perde x1.08 | |
| Eşya bulundu (sıradan/uncommon) | ses yok (yalnızca konuşma) | |
| Nadir buluş | `sfx/rare_find`, `rare_find_1` .. `_3` | 1.5 sn sınır |
| Kadim hazine (oyun sonu) | `sfx/legend_find_1` / `legend_find_2` + `sfx/fanfare` | |
| Satış (aşağı kaydır, otomatik satış) | `sfx/sell`, `sell_1` .. `_3` | |
| Çanta dolu | `sfx/error` | ses x0.7, perde x0.9 |
| Yetersiz para / hata mesajı | `sfx/error` | |
| Yeni katman | `sfx/fanfare` + ortam geçişi (aşağıda) | |
| Satın alma (sıradan yükseltme) | `sfx/buy`, `buy_1`, `buy_2` | |
| Satın alma (kazma kademesi yükseldi) | `sfx/upgrade` | |
| Dinamit patlaması (makine) | `sfx/fuse`, 0.45 sn sonra `sfx/dynamite`, `dynamite_1`, `_2` | fuse ses x0.8 |
| Matkap raporu (makine) | `sfx/drill_loop` / `sfx/drill_loop_2` (sırayla), 1.8 sn döngü | 0.12 sn aç, 0.25 sn sön |
| Dükkân açıldı | `sfx/menu_open` | |
| Dükkân kapandı | `sfx/menu_close` | |
| Dükkânda öğe/kategori gezinme | ses yok (konuşma + hafif titreşim) | |
| Ayarlar açıldı / kapandı | `sfx/menu_open` / `sfx/menu_close` | |
| Ayarlarda sola-sağa gezinme | `sfx/menu_open` | ses x0.5, perde x1.12 |
| Ayarlarda "Efekt sesi" değişimi | toprak vuruşu (`dirt_hit` ailesi) | |
| Açılışta konuşma sesi yoksa | `sfx/error` | |
| Müzik | `music/ambient1` .. `ambient4` (rastgele sırayla, 6 sn çapraz geçiş) | veri yolu -18 dB |
| Ortam (ambiyans) | katman 0-1: `ambient/layer_1`; 2-3: `layer_2`; 4-5: `layer_3`; 6-7: `layer_4`; 8-9: `layer_5` | veri yolu -20 dB |

## Efekt dosyaları (ffmpeg ölçümü)

| Dosya | Süre (sn) | Seviye düzeltmesi (dB) | Çalış başı (sn) | Kesilip sönme sonu (sn) |
|---|---|---|---|---|
| sfx/buy.ogg | 0.52 | -10.0 | 0.0 | 0.524 |
| sfx/buy_1.ogg | 0.76 | -4.6 | 0.0 | 0.277 |
| sfx/buy_2.ogg | 0.86 | 3.0 | 0.0 | 0.537 |
| sfx/coal_hit_1.ogg | 0.60 | 3.7 | 0.0 | 0.5 |
| sfx/coal_hit_2.ogg | 0.60 | 3.8 | 0.0 | 0.196 |
| sfx/coal_hit_3.ogg | 0.68 | 3.1 | 0.0 | 0.132 |
| sfx/crystal_hit.ogg | 0.39 | 0.7 | 0.0 | 0.388 |
| sfx/crystal_hit_1.ogg | 0.77 | 2.5 | 0.0 | 0.473 |
| sfx/crystal_hit_2.ogg | 0.78 | 0.9 | 0.0 | 0.279 |
| sfx/crystal_hit_3.ogg | 0.78 | 2.8 | 0.0 | 0.5 |
| sfx/dirt_hit.ogg | 1.60 | -3.1 | 0.13 | 0.5 |
| sfx/dirt_hit_1.ogg | 0.57 | 1.6 | 0.0 | 0.5 |
| sfx/dirt_hit_2.ogg | 0.70 | -1.5 | 0.0 | 0.5 |
| sfx/dirt_hit_3.ogg | 0.70 | -4.3 | 0.0 | 0.5 |
| sfx/dirt_hit_4.ogg | 0.57 | -4.5 | 0.0 | 0.5 |
| sfx/drill_loop.ogg | 3.99 | -1.5 | 0.0 | döngü/tam |
| sfx/drill_loop_2.ogg | 4.00 | -1.4 | 0.0 | döngü/tam |
| sfx/dynamite.ogg | 1.25 | 1.7 | 0.0 | 1.246 |
| sfx/dynamite_1.ogg | 2.38 | 0.0 | 0.0 | 0.609 |
| sfx/dynamite_2.ogg | 2.97 | 3.8 | 0.0 | 0.249 |
| sfx/error.ogg | 0.24 | -0.7 | 0.0 | 0.235 |
| sfx/fanfare.ogg | 3.17 | -1.6 | 0.198 | 2.3 |
| sfx/fuse.ogg | 0.54 | 4.1 | 0.0 | 0.542 |
| sfx/legend_find_1.ogg | 0.83 | -0.7 | 0.0 | 0.825 |
| sfx/legend_find_2.ogg | 1.25 | 2.1 | 0.0 | 1.251 |
| sfx/menu_close.ogg | 1.20 | -1.7 | 0.0 | 0.8 |
| sfx/menu_open.ogg | 1.20 | -1.8 | 0.0 | 0.8 |
| sfx/metal_hit.ogg | 1.00 | 1.7 | 0.0 | 0.5 |
| sfx/metal_hit_1.ogg | 0.53 | 3.8 | 0.0 | 0.129 |
| sfx/metal_hit_2.ogg | 0.48 | -1.0 | 0.0 | 0.26 |
| sfx/metal_hit_3.ogg | 0.43 | 3.7 | 0.0 | 0.278 |
| sfx/metal_hit_4.ogg | 0.79 | 0.0 | 0.0 | 0.193 |
| sfx/rare_find.ogg | 2.00 | 4.5 | 0.0 | 1.5 |
| sfx/rare_find_1.ogg | 1.50 | 3.6 | 0.0 | 1.206 |
| sfx/rare_find_2.ogg | 1.28 | 1.7 | 0.0 | 1.317 |
| sfx/rare_find_3.ogg | 1.06 | 2.5 | 0.0 | 1.201 |
| sfx/sell.ogg | 0.45 | -4.7 | 0.086 | 0.453 |
| sfx/sell_1.ogg | 0.77 | -0.9 | 0.026 | 0.247 |
| sfx/sell_2.ogg | 0.96 | -4.0 | 0.0 | 0.349 |
| sfx/sell_3.ogg | 0.77 | -3.2 | 0.0 | 0.325 |
| sfx/stone_hit.ogg | 0.46 | -0.3 | 0.0 | 0.464 |
| sfx/stone_hit_1.ogg | 0.58 | 4.2 | 0.0 | 0.425 |
| sfx/stone_hit_2.ogg | 0.73 | 3.9 | 0.0 | 0.5 |
| sfx/stone_hit_3.ogg | 0.70 | 3.9 | 0.0 | 0.5 |
| sfx/stone_hit_4.ogg | 0.68 | 4.1 | 0.0 | 0.396 |
| sfx/upgrade.ogg | 0.44 | -10.0 | 0.0 | 0.445 |

Not: "Çalış başı" sıfırdan büyükse dosyanın başındaki sessizlik o kadar atlanır; "sönme sonu" dosyanın o saniyede
kesilip 80 ms içinde söndürüldüğü yerdir. Dosyayı beğenmediğinde adını söylemen yeterli.

## Müzik ve ortam dosyaları

| Dosya | Süre (sn) | Seviye düzeltmesi (dB) |
|---|---|---|
| ambient/layer_1.ogg | 171 | -9.3 |
| ambient/layer_2.ogg | 20 | 6 |
| ambient/layer_3.ogg | 154 | -4.3 |
| ambient/layer_4.ogg | 154 | -2.9 |
| ambient/layer_5.ogg | 307 | -1.0 |
| music/ambient1.ogg | 312 | 3.7 |
| music/ambient2.ogg | 307 | 1.6 |
| music/ambient3.ogg | 192 | -5.9 |
| music/ambient4.ogg | 145 | 5.4 |

Not: `ambient/layer_2.ogg` kaynakta çok sessiz (-34.5 LUFS, en çok +6 dB kazanç verilebilir); hâlâ diğer katmanlardan
belirgin biçimde kısık duyulabilir. Katman 2-3'te ortam "yok gibi" geliyorsa bu dosyadır.
