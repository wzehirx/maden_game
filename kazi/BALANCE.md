# BALANCE — Kazı Kazan İmparatorluğu (denge simülasyonu sonucu)

Son ölçüm: `dotnet run --project kazi/Sim -c Release -- --seeds 5` (10 tohumla da aynı aralıklar; ~28 sn).
Sanal oyuncu gerçek `GameSession` API'sini kullanır (BuyCard, ScratchTap, BuyUpgrade, Tick...), hiçbir kısayol yoktur.

## Sonuç (x1 hız, sıralı mod, 5 tohum)

| Ölçüt | Hedef | Ölçülen (ortalama / aralık) |
|---|---|---|
| İlk otomasyon (Otomatik Kazıyıcı alındı) | 30–45 dk | 37,9 dk (27,8 – 42,7) |
| İlk prestij | 4–6 sa | 5,09 sa (4,96 – 5,19) |
| Tüm kalıcı ağaç (24 düğüm) | 15–20 sa | 19,4 sa (19,3 – 19,5) |
| İki ilerleme arası en uzun bekleme | < 5 dk | ort 4,05 dk, en kötü tohum 4,18 dk |
| Katalog açılışları (koşu 1, ort.) | – | K2 49 dk, K3 105 dk, K4 172 dk, K5 238 dk |

Not: Kabul ölçütü tohumlar arası ORTALAMAdır; ilk otomasyon süresi tek tohumda 27,8 dk (30'un biraz altı) çıkabiliyor, diğer ölçütler tüm tohumlarda aralıkta. Koşu süreleri: 5,0 → 4,1 → 3,3 → 2,8 → 2,1 → 2,1 sa; Altın Bilet puanı koşu başına ~29–45.

## Sanal oyuncu (`kazi/Sim/Program.cs`)
- Elle oynarken 1,7 eylem/sn (satın al + 6 dokunuş + sonraki ≈ 10 kart/dk); otomatik kazıyıcı + otomatik alım açılınca elle oynamayı bırakır (yalnızca Son Şans kartını elle oynar). `--manual-after-auto` ile ikisi birden denenebilir.
- Kart seçimi: bilet bedeli bakiyenin yarısını aşmayan en yüksek beklenen net kazançlı tür (`Economy.BestType`); Son Şans kartına bakiye ≥ 2,5×fiyat olunca girer.
- Yükseltme seçimi: (gelir artışı / maliyet) en yüksek olan, 60 sn'lik gelirle ulaşılabilen adaylardan; Otomatik Kazıyıcı/Alım zorunlu (100 sn ufuk). Oynanan kartın 2 katı bakiye tamponu korunur.
- Prestij teklifi gelince hemen kabul; kalıcı düğümler en ucuzdan başlayarak alınır.
- Parametreler: `Strategy` sınıfı (BankrollFraction, GateMoneyFactor, HorizonSeconds, PaybackFactor, ManualAfterAuto). CLI: `--seeds N --hours H --f 0.5 --manual-after-auto --verbose --only K --rtp`.

## Ekonomi tasarımının özeti
- Katalog kapıları: bir önceki katalogun "Son Şans" kartı hem toplam yükseltme seviyesi eşiğini (cards.json `gateLevels`, koşu başına) hem bakiye şartını ister; böylece her katalog yükseltme merdiveni bitmeden açılamaz ve bekleme süreleri YOĞUN merdivenle (çok sayıda ucuz seviye) doldurulur (en uzun bekleme < 5 dk için).
- Son Şans kartlarında `pityAfter` = 4: en fazla 5 denemede garanti kazanç (oyuncu tıkanmaz).
- Her kartın taban RTP'si 1,02–1,25 (testle [0,85–1,6] denetlenir); şans (kazanma ihtimali ×, kartta tavan `maxWinChance`) ve ödeme çarpanları RTP'yi artırır. Çarpan etkisi tüm oyunda ~×50.
- Sayılar uzun oyunda katrilyon mertebesine çıkar (long taşmasına karşı ödemeler/toplamlar doygunlaştırılır, `NumberFormat` kentilyona kadar okur).

## JSON'u yeniden ayarlamak
`kazi/tools/balance/` içinde üreticiler vardır (yalnızca JSON üretir, kod değişmez):
- `params.json`: `us` (kademe başına yükseltme maliyet ölçeği), `gates` (Son Şans fiyatları), `glf` (kapı seviye eşiği oranları), `lvf` (merdiven yoğunluğu), `ncs` (prestij düğüm maliyet ölçeği), `s` (settings.json üzerine yazılanlar, örn. `abRefEarned`), `u.<id>.base` (tek yükseltme maliyeti).
- `./run.sh --seeds 8 --hours 40` JSON'ları yeniden üretir ve simülasyonu koşturur. Doğrudan `kazi/data/*.json` düzenleyip `dotnet run --project kazi/Sim -c Release -- --seeds 5` de yeterlidir.

## Bilinen sınırlar
- Sanal oyuncu rasyonel/kararlıdır; gerçek oyuncu daha yavaş (veya prestij kararını geciktirebilir).
- Çevrimdışı kazanç simüle edilmez (Core testleriyle doğrulanır: tavan 7200 sn).
- Hızlı/uzun oturumlarda en uzun bekleme, kademelerin sonundaki pahalı seviyelerde 4 dk civarındadır; eşiğe (5 dk) yakın.
