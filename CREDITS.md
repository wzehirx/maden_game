# Ses Efektleri Kredileri

Tüm ses dosyaları `Assets/Resources/Audio/sfx/` altındadır. Lisanslar, her kaynak sayfasında (opengameart.org) doğrulanmıştır. Sonniss kaynaklı dosyalar için lisans metni aşağıdaki "Sonniss lisansı" bölümündedir.

| Dosya | Başlık | Yazar | Kaynak URL | Lisans |
|---|---|---|---|---|
| dirt_hit.ogg | Mining sample (ilk vuruş, 0–1.6 sn kırpıldı) | jordan4ibanez | https://opengameart.org/content/mining-sample | CC0 |
| stone_hit.ogg | Breaking Rock (rock_break_0.ogg) | themightyglider | https://opengameart.org/content/breaking-rock | CC0 |
| metal_hit.ogg | 100 CC0 SFX (metal_04.ogg) | rubberduck | https://opengameart.org/content/100-cc0-sfx | CC0 |
| crystal_hit.ogg | 100 CC0 SFX (glass_02.ogg) | rubberduck | https://opengameart.org/content/100-cc0-sfx | CC0 |
| rare_find.ogg | Bell dings/chimes (bell_ding1.wav, 2 sn kırpıldı) | pwl | https://opengameart.org/content/bell-dingschimes | CC0 |
| sell.ogg | 12 Coin Sound Effects (coin.1.ogg) | starninjas | https://opengameart.org/content/12-coin-sound-effects | CC0 |
| buy.ogg | 50 CC0 retro / synth SFX (power_up_01.ogg) | rubberduck | https://opengameart.org/content/50-cc0-retro-synth-sfx | CC0 |
| dynamite.ogg | 50 CC0 retro / synth SFX (retro_explosion_01.ogg) | rubberduck | https://opengameart.org/content/50-cc0-retro-synth-sfx | CC0 |
| menu_open.ogg | Air whoosh (whoosh2_0.wav, 1.2 sn kırpıldı) | pyranostudios | https://opengameart.org/content/air-whoosh | CC0 |
| menu_close.ogg | Air whoosh (whoosh2_0.wav, ters çevrildi, 1.2 sn) | pyranostudios | https://opengameart.org/content/air-whoosh | CC0 (türetilmiş) |
| legend_find_1.ogg | 100 CC0 SFX (gong_02.ogg) | 2DPIXX, HauntedBees, Liberiton, aijikl, rubberduck, unitcodes | https://opengameart.org/content/100-cc0-sfx | CC0 |
| legend_find_2.ogg | 100 CC0 SFX (gong_01.ogg) | 2DPIXX, HauntedBees, Liberiton, aijikl, rubberduck, unitcodes | https://opengameart.org/content/100-cc0-sfx | CC0 |
| upgrade.ogg | 50 CC0 retro / synth SFX (power_up_04.ogg) | DaveZ, Neyuriki, Snabisch, farcodev, retrogemn_, rubberduck | https://opengameart.org/content/50-cc0-retro-synth-sfx | CC0 |
| error.ogg | 7 mechanical clicks and buzzes (buzzerr-01.flac) | PFNienow, qubodup | https://opengameart.org/content/7-mechanical-clicks-and-buzzes | CC0 |
| fuse.ogg | Simple Fuse Sound (fuse_0.ogg) | themightyglider | https://opengameart.org/content/simple-fuse-sound | CC0 |

## Notlar

- `menu_close.ogg`, `menu_open.ogg` dosyasının zaman tersine çevrilmiş halidir.
- `stone_hit.ogg`, `crystal_hit.ogg`, `metal_hit.ogg` 44.1 kHz'e yeniden örneklendi (önceki sürümler 24/48 kHz idi); adları korundu.
- `menu_close.ogg` önceki sürümünde sessizdi (tepe -91 dBFS); `menu_open.ogg`'nin ters çevrilmiş hali olarak yeniden üretildi.
- Eski dosyalar (`dirt_hit`, `stone_hit`, `metal_hit`, `crystal_hit`, `rare_find`, `sell`, `buy`, `dynamite`, `fanfare`, `menu_open`, `menu_close`, `drill_loop`) yalnızca kazançla hedef seviyeye getirildi; adları ve içerikleri değişmedi.
- Tüm yeni sfx dosyaları: ogg (libvorbis q5), 44.1 kHz; baştaki/sondaki sessizlik kırpıldı (`fuse`, `error`, `drill_loop_2` hariç). Tepe hedefi -2.5 dBFS. Ogg kodlama sapması nedeniyle çoğu dosya -1..-3 dBFS aralığında; sınır dışında kalanlar: `sell` (-0.3), `stone_hit` (-0.7), `rare_find` (-3.5), `crystal_hit` (-3.1), `drill_loop` (-3.1).
- Bu oturumda ses dinlenemedi; seçimler dosya adı, süre ve seviye ölçümlerine göre yapıldı. Oyunda kulakla kontrol edilmeli.
- Dikkat: `coal_hit_*` kaya düşme/kırılma seslerinden alındı (kömüre özgü ses bulunamadı). `coal_hit_3` ve `metal_hit_1` yaklaşık 0.13 sn ile çok kısa.
- Kristal çınlama ambiyansı (katman) için uygun CC0/CC-BY kaynak BULUNAMADI; eklenmedi.
- Dikkat: `dirt_hit_3/4` ve mevcut `dirt_hit` aynı kayıttan (mining_1) alındı; `dirt_hit_1/2` farklı kayıtlar. `dirt_hit_3/4` düşük seviyeli kayıtlardan geldiği için yaklaşık +6–7 dB yükseltildi, gürültü tabanı artmış olabilir.
- `rare_find_3.ogg` (bell_ding4) düşük seviyeliydi, yaklaşık +7 dB yükseltildi.
- `bag_full.ogg` (çanta dolu uyarısı) için uygun CC0/CC-BY aday bulunamadı; yeni dosya üretilmedi. Git'teki mevcut `bag_full.ogg` kaynağı belirsiz olduğu için doğrulanmalı.
- `error.ogg`: kaynak ağacında tek bir kısa buzzer (0.24 sn) kullanıldı.
- `fanfare` için yeni varyasyon eklenmedi (mevcut dosya kullanılıyor).
- Ses kaynakları freesound/pixabay/kenney yerine opengameart.org'dan alındı; hiçbir ses sentezlenmedi (sadece kırpma, normalize, yeniden örnekleme, çapraz karartma ve ters çevirme kullanıldı).

## Müzik ve Ortam Sesleri

Dosyalar `Assets/Resources/Audio/music/` ve `Assets/Resources/Audio/ambient/` altındadır (.ogg, libvorbis q4). Lisanslar kaynak sayfalarında (opengameart.org) doğrulanmıştır.

| Dosya | Başlık | Yazar | Kaynak URL | Lisans |
|---|---|---|---|---|
| music/ambient1.ogg | Biolumina (312 sn) | Tsorthan Grove | https://opengameart.org/content/biolumina | CC0 |
| music/ambient2.ogg | Dark Ambient Drone (loop, 307 sn) | Tsorthan Grove | https://opengameart.org/content/dark-ambient-drone | CC0 |
| music/ambient3.ogg | Drifting Through the Nebula (Ambient) (192 sn) | Hitctrl, mo-official | https://opengameart.org/content/drifting-through-the-nebula-ambient | CC-BY 3.0 (atıf zorunlu) |
| music/ambient4.ogg | Manaos Drones (145 sn) | Tozan | https://opengameart.org/content/manaos-drones | CC0 |
| ambient/layer_1.ogg | windy ambient noise (windy_ambiance_0.ogg, 171 sn) | Some Weirdo | https://opengameart.org/content/windy-ambient-noise | CC0 (döngü dikişi doğrulanmadı) |
| ambient/layer_2.ogg | Dripping water loop (atmosbasement, 20 sn) | qubodup, supergamez2014 | https://opengameart.org/content/dripping-water-loop | CC0 |
| ambient/layer_3.ogg | Background Drones Vol 1 (temple_of_the_digital_monk_loop, 154 sn) | Tsorthan Grove | https://opengameart.org/content/background-drones-vol-1 | CC0 |
| ambient/layer_4.ogg | Background Drones Vol 1 (light_through_a_pale_window_loop, 154 sn) | Tsorthan Grove | https://opengameart.org/content/background-drones-vol-1 | CC0 (kristal değil, yumuşak tınlayan drone) |
| ambient/layer_5.ogg | Dark Ambient Drone #2 (loop, 307 sn) | Tsorthan Grove | https://opengameart.org/content/dark-ambient-drone-2 | CC0 |
| dirt_hit_1.ogg | Sonniss GDC 2017 - Rock,Brick and Dirt Part 02 (Rock Hit and Bounce Dirt_RBDII 06 (0.05-0.6 sn)) | Mechanical Wave | https://archive.org/details/sonniss-gdc-2017-game-audio-bundle-normalized | Sonniss #GameAudioGDC Bundle lisansı (royalty-free, ticari kullanım serbest, atıfsız) |
| dirt_hit_2.ogg | Sonniss GDC 2016 - Rocks (soil and dirt debris heavy 13) | Alexander Kopeikin | https://archive.org/details/sonniss-gdc-2016-game-audio-bundle-normalized | Sonniss #GameAudioGDC Bundle lisansı (royalty-free, ticari kullanım serbest, atıfsız) |
| dirt_hit_3.ogg | Sonniss GDC 2019 - You Me & Debris (impact_gritty_grit_sandy_dirt_dig_shovel_shingle_roof_02) | Matt Script | https://archive.org/details/sonniss-gdc-2019-game-audio-bundle-normalized | Sonniss #GameAudioGDC Bundle lisansı (royalty-free, ticari kullanım serbest, atıfsız) |
| dirt_hit_4.ogg | Sonniss GDC 2017 - Rock,Brick and Dirt Part 02 (Multiple Rock Hit Dirt_RBDII 07 (ilk vuruş)) | Mechanical Wave | https://archive.org/details/sonniss-gdc-2017-game-audio-bundle-normalized | Sonniss #GameAudioGDC Bundle lisansı (royalty-free, ticari kullanım serbest, atıfsız) |
| stone_hit_1.ogg | Sonniss GDC 2015 - Rock Brick and Dirt (Hit Rock Debris_RBD 02) | Mechanical Wave | https://archive.org/details/sonniss-gdc-2015-game-audio-bundle-normalized | Sonniss #GameAudioGDC Bundle lisansı (royalty-free, ticari kullanım serbest, atıfsız) |
| stone_hit_2.ogg | Sonniss GDC 2017 - Rock,Brick and Dirt Part 02 (Hit Rock into Gravel_RBDII 06) | Mechanical Wave | https://archive.org/details/sonniss-gdc-2017-game-audio-bundle-normalized | Sonniss #GameAudioGDC Bundle lisansı (royalty-free, ticari kullanım serbest, atıfsız) |
| stone_hit_3.ogg | Sonniss GDC 2018 - Foley Elements Rocks, Dirt And Stone (boulder_impact_on_stones_14) | Soundrangers | https://archive.org/details/sonniss-gdc-2018-game-audio-bundle-normalized | Sonniss #GameAudioGDC Bundle lisansı (royalty-free, ticari kullanım serbest, atıfsız) |
| stone_hit_4.ogg | Sonniss GDC 2016 - Rocks (rock impact medium 26) | Alexander Kopeikin | https://archive.org/details/sonniss-gdc-2016-game-audio-bundle-normalized | Sonniss #GameAudioGDC Bundle lisansı (royalty-free, ticari kullanım serbest, atıfsız) |
| coal_hit_1.ogg | Sonniss GDC 2018 - Foley Elements Rocks, Dirt And Stone (brick_impact_debris_02) | Soundrangers | https://archive.org/details/sonniss-gdc-2018-game-audio-bundle-normalized | Sonniss #GameAudioGDC Bundle lisansı (royalty-free, ticari kullanım serbest, atıfsız) |
| coal_hit_2.ogg | Sonniss GDC 2015 - Rock Brick and Dirt (Debris Brick_RBD 02) | Mechanical Wave | https://archive.org/details/sonniss-gdc-2015-game-audio-bundle-normalized | Sonniss #GameAudioGDC Bundle lisansı (royalty-free, ticari kullanım serbest, atıfsız) |
| coal_hit_3.ogg | Sonniss GDC 2015 - Rock Brick and Dirt (Smash Rock On Debris_RBD 04) | Mechanical Wave | https://archive.org/details/sonniss-gdc-2015-game-audio-bundle-normalized | Sonniss #GameAudioGDC Bundle lisansı (royalty-free, ticari kullanım serbest, atıfsız) |
| metal_hit_1.ogg | Sonniss GDC 2017 - MEDIEVAL, Smithy (MEDIEVAL, Smithy - 08 - Anvil hits (1. vuruş)) | Mononeshot | https://archive.org/details/sonniss-gdc-2017-game-audio-bundle-normalized | Sonniss #GameAudioGDC Bundle lisansı (royalty-free, ticari kullanım serbest, atıfsız) |
| metal_hit_2.ogg | Sonniss GDC 2017 - MEDIEVAL, Smithy (MEDIEVAL, Smithy - 08 - Anvil hits (2. vuruş)) | Mononeshot | https://archive.org/details/sonniss-gdc-2017-game-audio-bundle-normalized | Sonniss #GameAudioGDC Bundle lisansı (royalty-free, ticari kullanım serbest, atıfsız) |
| metal_hit_3.ogg | Sonniss GDC 2015 - Ilmarinen (Ilmarinen, blacksmith, forge, hammer, anvil, strike, loose, ringing) | Eiravaein Works | https://archive.org/details/sonniss-gdc-2015-game-audio-bundle-normalized | Sonniss #GameAudioGDC Bundle lisansı (royalty-free, ticari kullanım serbest, atıfsız) |
| metal_hit_4.ogg | Sonniss GDC 2017 - The Blacksmith (Metal_Plate_Hit_02_06) | SoundBits | https://archive.org/details/sonniss-gdc-2017-game-audio-bundle-normalized | Sonniss #GameAudioGDC Bundle lisansı (royalty-free, ticari kullanım serbest, atıfsız) |
| crystal_hit_1.ogg | Sonniss GDC 2016 - The Source Collection SFX Pack (Wine Glass Hit Higher Pitch (1. vuruş)) | George Karagioules | https://archive.org/details/sonniss-gdc-2016-game-audio-bundle-normalized | Sonniss #GameAudioGDC Bundle lisansı (royalty-free, ticari kullanım serbest, atıfsız) |
| crystal_hit_2.ogg | Sonniss GDC 2016 - The Source Collection SFX Pack (Wine Glass Hit Higher Pitch (2. vuruş)) | George Karagioules | https://archive.org/details/sonniss-gdc-2016-game-audio-bundle-normalized | Sonniss #GameAudioGDC Bundle lisansı (royalty-free, ticari kullanım serbest, atıfsız) |
| crystal_hit_3.ogg | Sonniss GDC 2016 - The Source Collection SFX Pack (Wine Glass Hit Higher Pitch (3. vuruş)) | George Karagioules | https://archive.org/details/sonniss-gdc-2016-game-audio-bundle-normalized | Sonniss #GameAudioGDC Bundle lisansı (royalty-free, ticari kullanım serbest, atıfsız) |
| rare_find_1.ogg | Sonniss GDC 2015 - Percussion 01 (Bell_Waiter_Fienup_001) | Soundopolis | https://archive.org/details/sonniss-gdc-2015-game-audio-bundle-normalized | Sonniss #GameAudioGDC Bundle lisansı (royalty-free, ticari kullanım serbest, atıfsız) |
| rare_find_2.ogg | Sonniss GDC 2016 - The Source Collection SFX Pack (Wine Glass Hit Higher Pitch (4. vuruş)) | George Karagioules | https://archive.org/details/sonniss-gdc-2016-game-audio-bundle-normalized | Sonniss #GameAudioGDC Bundle lisansı (royalty-free, ticari kullanım serbest, atıfsız) |
| rare_find_3.ogg | Sonniss GDC 2016 - The Source Collection SFX Pack (Wine Glass Hit Higher Pitch (5. vuruş)) | George Karagioules | https://archive.org/details/sonniss-gdc-2016-game-audio-bundle-normalized | Sonniss #GameAudioGDC Bundle lisansı (royalty-free, ticari kullanım serbest, atıfsız) |
| sell_1.ogg | Sonniss GDC 2017 - Money (Money,Coins,Drop In Cash Register) | Hzandbits | https://archive.org/details/sonniss-gdc-2017-game-audio-bundle-normalized | Sonniss #GameAudioGDC Bundle lisansı (royalty-free, ticari kullanım serbest, atıfsız) |
| sell_2.ogg | Sonniss GDC 2018 - COINAGE (Coins_Pouch_Leather_Drop_Into_Takes_3) | Effectsworks | https://archive.org/details/sonniss-gdc-2018-game-audio-bundle-normalized | Sonniss #GameAudioGDC Bundle lisansı (royalty-free, ticari kullanım serbest, atıfsız) |
| sell_3.ogg | Sonniss GDC 2017 - Money (Money,Coins,Handle) | Hzandbits | https://archive.org/details/sonniss-gdc-2017-game-audio-bundle-normalized | Sonniss #GameAudioGDC Bundle lisansı (royalty-free, ticari kullanım serbest, atıfsız) |
| buy_1.ogg | Sonniss GDC 2018 - COINAGE (Coin_Wood_Table_Singles_Drop_Spin_Takes_5) | Effectsworks | https://archive.org/details/sonniss-gdc-2018-game-audio-bundle-normalized | Sonniss #GameAudioGDC Bundle lisansı (royalty-free, ticari kullanım serbest, atıfsız) |
| buy_2.ogg | Sonniss GDC 2018 - COINAGE (Coins_Hand_Jingle_Movement_Takes_6) | Effectsworks | https://archive.org/details/sonniss-gdc-2018-game-audio-bundle-normalized | Sonniss #GameAudioGDC Bundle lisansı (royalty-free, ticari kullanım serbest, atıfsız) |
| dynamite_1.ogg | Sonniss GDC 2015 - Guns (EFX EXT Mortar Explosion 08) | Coll Anderson | https://archive.org/details/sonniss-gdc-2015-game-audio-bundle-normalized | Sonniss #GameAudioGDC Bundle lisansı (royalty-free, ticari kullanım serbest, atıfsız) |
| dynamite_2.ogg | Sonniss GDC 2018 - Distant Blast (Distant Blast 17) | Lukas Tvrdon | https://archive.org/details/sonniss-gdc-2018-game-audio-bundle-normalized | Sonniss #GameAudioGDC Bundle lisansı (royalty-free, ticari kullanım serbest, atıfsız) |
| fanfare.ogg | Sonniss GDC 2018 - Audience Reactions (audience_cheers_13) | Soundrangers | https://archive.org/details/sonniss-gdc-2018-game-audio-bundle-normalized | Sonniss #GameAudioGDC Bundle lisansı (royalty-free, ticari kullanım serbest, atıfsız) |
| drill_loop.ogg | Sonniss GDC 2017 - Jackhammer (construction_jackhammer_drill_concrete_wall_01 (10-14.5 sn, 0.5 sn çapraz karartmalı döngü)) | RedSonic | https://archive.org/details/sonniss-gdc-2017-game-audio-bundle-normalized | Sonniss #GameAudioGDC Bundle lisansı (royalty-free, ticari kullanım serbest, atıfsız) |
| drill_loop_2.ogg | Sonniss GDC 2015 - Tools:Construction 01 (Jackhammer_Fienup_001 (40-44.5 sn, 0.5 sn çapraz karartmalı döngü)) | Soundopolis | https://archive.org/details/sonniss-gdc-2015-game-audio-bundle-normalized | Sonniss #GameAudioGDC Bundle lisansı (royalty-free, ticari kullanım serbest, atıfsız) |

## Müzik ve Ortam Notları

- CC-BY 3.0 ile lisanslanan `music/ambient3.ogg` için atıf: "Drifting Through the Nebula (Ambient)" by Hitctrl and mo-official, CC BY 3.0, https://opengameart.org/content/drifting-through-the-nebula-ambient . Oyunda gösterilmelidir.
- Kristal/çan tınlaması (layer_4 için): BULUNAMADI. CC0/CC-BY olarak bulunan kristal adayları (ör. "Crystal voices", Spellweaver) CC-BY-SA lisanslı olduğu için kullanılmadı; layer_4 yerine yumuşak tınlayan CC0 drone kullanıldı.
- Kevin MacLeod (incompetech) parçaları bu turda kullanılmadı; müzik için yeterli CC0/CC-BY aday OpenGameArt'ta bulundu.
- OpenGameArt'ta "loop" olarak etiketlenen dosyalar: layer_2, layer_3, layer_5, music/ambient2. Diğerleri (music/ambient1, ambient3, ambient4, layer_1, layer_4) "loop" etiketi taşımıyor; döngü dikişi doğrulanmadı, oyunda kontrol edilmeli.

## Sonniss lisansı

Kaynak: Sonniss #GameAudioGDC Bundle (2015-2020 yılları), Internet Archive aynaları (`sonniss-gdc-<yıl>-game-audio-bundle-normalized`); paketteki `License.pdf` okunmuştur. Özet: "worldwide, non-exclusive, royalty-free license"; kişisel ve ticari projelerde (oyunlar dahil) kullanım ve değiştirme serbest, atıf gerekmez; orijinal ses dosyaları olduğu gibi satılamaz, yazarlık iddia edilemez; yapay zeka eğitimi için kullanılamaz. Telif hakkı ilgili kayıt sahiplerindedir (listelenen yazarlar). Bu oturumda yalnızca kırpma, fade, tepe -2.5 dBFS normalizasyonu, ogg dönüşümü ve (drill_loop*) çapraz karartmalı döngü uygulandı; hiçbir ses sentezlenmedi.
- Dikkat: `drill_loop.ogg` artık CC-BY (dklon) değil, Sonniss/RedSonic kaynaklıdır; önceki atıf gerekliliği ortadan kalktı.
