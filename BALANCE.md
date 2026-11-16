# BALANCE — Maden denge raporu

Kaynak: `Assets/StreamingAssets/economy.json` (ekonomi sayıları yalnızca burada).
Doğrulama: `dotnet run --project Sim -c Release` (Core'u `GameSession.Tick(1/3 sn)` ile oynar).

## Sonuç (20 tohum, kabul ölçütü TUTUYOR)

| Ölçüt | Hedef | Ölçülen |
|---|---|---|
| Bitiş süresi | 15–20 saat | **16.80 – 17.13 saat** |
| İki satın alma arası en uzun bekleme | <= 5 dk | **255 sn (4,3 dk)** en kötü tohumda; 3 tohumlu varsayılan koşuda 235 sn |
| Satın alma sayısı | - | ~896 (ortalama gap ~70 sn) |

Katman varışı (tohum 1): Kil 0,85 sa, Taş 1,94, Kömür 3,45, Demir 5,01, Gümüş 6,69, Altın 8,54, Kristal 10,53, Elmas 12,64, Kadim Hazine 14,40, bitiş 16,90.
En uzun beklemeler hep kazma kademeleri (Demir kazma ~3 sa'te 227 sn, Çelik kazma ~6 sa'te 190 sn, Elmas kazma ~10 sa'te 182 sn); diğer bekleme 190 sn altında.

## Sanal oyuncu (Sim/Program.cs)
- Saniyede 3 dokunuş (Tick 1/3 sn, her adımda bir Tap), tohumlu `SystemRandom`.
- Çanta dolunca satar (1 sn dokunuş kaybı); alınabilir bir şeye yetecek kadar çantada değer varsa da satar. Otomatik satışı alınca satmaz.
- Satın alma: her aday için "alırsam tahmini gelir artışı" hesaplanır (analitik: dokunuş + makine blok/sn x beklenen eşya değeri, çanta doluluğu kaybı dahil), `geri ödeme = maliyet / kazanç` en küçük olan hedef alınır. Hedef <= 25 sn uzaktaysa beklenir; değilse alınabilen en iyi geri ödemeli şey hemen alınır.
- Raporlar: bitiş saati, katman varış zamanları ve katman girişi gelir/sn, satın alma sayısı, en uzun 5 bekleme ve yeri. Çıkış kodu kabul ölçütüne göre 0/1.
- `--seeds N` tohum sayısı, `-v` tüm satın alma günlüğü, ortam değişkeni `SIM_RAW=1` ham sayılar.

## Ekonomi yapısı (neden böyle)
- Gelir ~ (dps / blok hp) x eşya değeri; hp ve eşya değeri katmanda x10 büyür, bu yüzden gelir esasen oyuncunun dps'iyle büyür. Dps büyümesi: kazma kademeleri (1, 3, 9, 27, 80), çarpımsal güç hatları (her biri seviye başına +%3 .. %4,5; `power`), satış çarpanları, nadir şansı, makineler.
- Çarpımsal etkiler: `power`, `sellBonus`, `<makine>Power`, `<makine>Speed` seviye başına (1+effectPerLevel)^seviye. Toplamsal: `crit`, `rareChance`, `bag`, `autosell`.
- Güç ve satış hatları her katmana bir yeni "kademe" olarak girer ve bir katman önceden dükkânda görünür (`minLayer`); böylece oyuncunun her an alabileceği ucuz bir şey vardır. Bu, katman geçişlerindeki "alacak bir şey yok" boşluklarını kapattı.
- Son iki katman hattı (guc9, guc10) daha çok seviyeli (80) ve ucuz büyüme (x1,05) ile son bölümde de sürekli alım sağlar.
- Maliyetler `baseCost x costMult^seviye`; katman başına maliyet tabanı oyuncunun o katmandaki ölçülen gelirinden türetildi (araçlar: `Sim/tuning/`).

## Elle ayar (araçlar)
`Sim/tuning/gen_economy.py` + `tune.py` + `final_params.json` economy.json'u üretir ve derinlik/maliyetleri sim ölçümüyle yakınsatır (`python3 Sim/tuning/gen_economy.py "$(cat Sim/tuning/final_params.json)"` mevcut dosyayı birebir üretir). Bu araçlar zorunlu değil; JSON elle de düzenlenebilir. Sonrasında mutlaka `dotnet run --project Sim -c Release -- --seeds 20` çalıştırın.

## Bilinen kusurlar / açık konular
1. **Blok sayıları büyük**: sistem dps'i her katmanda ~x15-30 büyüttüğünden, süreyi sabit tutmak için derinlik katman 1'de 2,2 bin, katman 10'da 7,6 milyon blok (toplam ~12,7 milyon). Bir vuruş geç oyunda yüzlerce blok kırar; olaylar bu yüzden özetlenir ("N eşya buldun"). İstenirse eşya değerleri ve blok hp'leri aynı oranda büyütülüp derinlik küçültülebilir (gelir değişmez).
2. **Büyük sayılar**: geç oyunda para katrilyonlar mertebesinde (toplam kazanç ~2,8e17). `long` sınırından uzak; `GameSession` 4e18'de kırpar. Konuşma "24 trilyon lira" gibi kısaltır.
3. **Dükkân uzunluğu**: katman 10'da gizli olmayan öğe sayısı 40'ı aşabilir; kör oyuncu için uzun liste. Sıralama ucuzdan pahalıya değil, tanım sırasındadır (kazma, güç/satış hatları, diğer yükseltmeler, makineler).
4. Sanal oyuncu "ideal" tek stratejidir; gerçek oyuncu daha geç/çok bekleyebilir. Bitiş süresi oyuncunun dokunuş hızına ve satın alma kalitesine duyarlıdır (hedef pencere 15-20 sa'e geniş marj bırakıldı: ~17 sa).
5. Çevrimdışı kazanç tavanı 2 saattir; makine yoksa kazanç yoktur.
6. Dengenin güveni 20 tohumlu sim'e dayanır; ayrı bir oyuncu sezgisiyle (ör. sadece en ucuzu alan) tekrar ölçülmesi önerilir.
