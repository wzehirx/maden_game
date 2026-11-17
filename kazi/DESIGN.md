# Kazı Kazan İmparatorluğu — Teknik Tasarım (v1)

Motor: Godot 4.7.2 .NET (Unity değil; gerekçe PROGRESS.md). Proje kökü `kazi/`. Maden'in yığınıyla aynı: saf C# çekirdek + Godot oyun katmanı + xUnit + denge simülasyonu.

## Klasörler
- `kazi/Scripts/Core/` (namespace `Kazi.Core`, Godot/Unity bağımsız, LangVersion 9): kart üretimi, kazıma durumu, ödeme, yükseltmeler, otomasyon, prestij, kayıt, çevrimdışı kazanç, hareket tanıma.
- `kazi/Scripts/Game/`: Godot katmanı (giriş, TTS köprüsü, titreşim, ses, ayarlar, öğretici).
- `kazi/Core.Tests/`, `kazi/Sim/`: testler ve denge simülasyonu.
- `kazi/data/*.json`: TÜM sayılar. `cards.json` (kataloglar, kart türleri, semboller, ihtimaller), `upgrades.json` (normal yükseltmeler + gadget'lar + otomasyon), `prestige.json` (kalıcı ağaç), `settings.json` (yardım parası, çöp iadesi, çevrimdışı tavan vb.).
- `kazi/Audio/{sfx,music,ambient}` gerçek kayıtlar, `kazi/CREDITS.md`.

## Çekirdek kavramlar
- **Kart türü** (CardType): id, ad, katalog, fiyat, ızgara (rows×cols; 3x3 varsayılan, bazıları 2x3), kural türü, sembol tablosu (sembol → ağırlık → ödeme çarpanı), özel hücre ihtimalleri.
- **Kart örneği** (Card): oluşturulurken TÜM hücreler önceden belirlenir (RNG tohumu kayda girer, kazılırken hile/sapma olmaz), her hücrenin sembolü, kazınma yüzdesi (0..1), açıldı bayrağı.
- **Kazıma**: iki mod. SIRALI (varsayılan): her dokunuş `ScratchPower` kadar sıradaki hücreyi açar. SÜRÜKLEME: ekran 3x3'e bölünür, parmağın hücre üstündeki yolu hücre kazınma yüzdesini artırır; eşiği (JSON) geçen hücre açılır. Sürükleme ses/titreşim geri bildirimi (hız→ton, sütun→pan, sınır geçişi ipucu) Godot katmanında; çekirdek yalnızca `ScratchCell(index, amount)` bilir.
- **Ödeme**: kart tamamen açılınca `Resolve()` kural türüne göre ödemeyi hesaplar, bakiyeye eklenir, olay yayınlanır. Bakiye ASLA negatif olmaz (ceza `min(ceza, bakiye)`; tuzak cezası ödemeden düşer, ödeme sıfırın altına inemez).
- **Kural türleri (≥10 kart türü, 5 katalog)**:
  1. ThreeOfAKind: 6 hücrede aynı sembolden 3 → o sembolün ödülü.
  2. NumberHunt: oyuncu sayıları ↔ kazanan sayılar; eşleşen her sayının ödülü.
  3. Multiplier: eşleşme + çarpan hücresi (x2..x10).
  4. Trap: bazı hücreler ceza sembolü; tam açılırsa ceza ödemeden düşer; KISMİ KAZIMA ("göz atma"): oyuncu bırakabilir; `Discard()` → ödeme yok, ceza yok, bilet bedelinin `discardRefundPct`'i iade (settings.json); yarı açılmış kartta kazanç da kaybolur.
  5. Joker: joker her sembolün yerine geçer.
  6. Sequence: satır/sütun tamamlama.
  7. Jackpot: çok nadir, çok yüksek ödül.
  8. Katalog finali "Son Şans" (kataloğu açan kapı) ve katalog 5 finali "Altın Bilet Son Şansı" (prestij tetikler).
  Her katalog 2–3 tür; katalog açma = bir önceki katalogun Son Şans kartını kazınca (ya da JSON eşiği). Fiyat basamakları JSON'da (örn. 10 ₺ … milyonlar), ihtimaller JSON'da ve RTP (beklenen getiri) birim testle denetlenir: her kartın RTP'si [0.85, 1.6] aralığında, yükseltmelerle artar; asla otomatik "kaybeden" kartı olmasın (oyuncu tıkanmaz).
- **Yükseltmeler** (upgrades.json): kazıma gücü, şans (nadir sembol/yüksek ödül ağırlığı kayması), ödeme çarpanı, bilet indirimi, kuyruk uzunluğu, çanta (biriken kart), çevrimdışı süre. Maliyet = taban × çarpan^seviye. Alınabilir hale gelince sesli bildirim (ayardan kapatılır).
- **Otomasyon** (ilk 30–45 dk içinde): Otomatik Kazıyıcı (kuyruktan kart alır; kuyruk uzunluğu, hız (kart/dk), kart türü seçimi ayrı yükseltme), Otomatik Alım (seçili türden bakiye yettikçe kuyruğa ekler), gadget'lar (Mekanik Sayaç: hız; Mercek: tuzaklı kartlarda tuzak ihtimali ipucu; Şans Çıkrığı: şans; Dişli Pres: tek turda birden fazla kart; Kasa Zili: ödemeye küçük bonus) — her birinin ayrı sesi. Ödemeler her zaman otomatik bakiyeye eklenir (elle toplama yok). Otomatik kazıyıcı tuzaklı kartları varsayılan TAM açar (ayar: "tuzakta çöpe at").
- **Yardım parası**: bakiye < en ucuz kartın fiyatı ve kuyruk boş ise `helpMoney` (JSON) verilir; sesli söylenir. Oyuncu asla tıkanmaz.
- **Prestij**: "Altın Bilet Son Şansı" kartı kazınınca oyuncu sıfırlama teklifini DUYAR (isteğe bağlı; Reddetmek serbest). Sıfırlanınca bakiye/kartlar/yükseltmeler/katalog ilerlemesi sıfırlanır; "Altın Bilet puanı" (AB) kazanılır; kalıcı ağaç (prestige.json ~24 düğüm: başlangıç parası, kalıcı şans, otomasyonun erken açılması, çevrimdışı süre, kazıma gücü, çarpanlar…) AB ile alınır. Hiçbir tek düğüm tam bir koşu gerektirmez.
- **Çevrimdışı**: en fazla `offlineCapSeconds` (varsayılan 7200); yalnızca otomasyon; açılışta tek cümle.
- **Kayıt**: JSON, sürümlü, bozuk veriye dayanıklı; her 30 sn + arka plana geçince.

## Konuşma/ses (Godot katmanı)
- Konuşma ayrıntısı: "tam" (her sembol) / "kısa" (sadece sonuç) / "sessiz" (yalnızca sesler). Kart sonucu tek cümle: "Kazandın, 250 lira" / "Bu sefer çıkmadı".
- Earcon'lar: eşleşme, kazanç (küçük/orta/büyük/jackpot), boş (nazik), tuzak, yükseltme alındı, yardım parası, katalog açıldı, otomasyon.
- Konuşma motoru seçimi: ortak Android TTS köprüsü (`tools/android_tts`), ayarlarda motor + ses seçimi.
- Sayılar Türkçe kısaltılır ("1,2 bin lira", "3,4 milyon lira").

## Kontroller (tam ekran, buton yok) — şartnameye aynen
Kart ekranı: dokun/sürükle = kazı; aşağı kaydır = kartı bırak (tuzaklı ve yarım açıksa çöp iadesiyle) / sıradaki karta geç; yukarı kaydır = dükkân. Dükkân: sola/sağa gez (ad, fiyat, etki), çift dokunuş satın al, eksik para söylenir; 2 parmakla sola/sağa kategori (Kartlar, Kazıma, Otomasyon, Gadget, Prestij). 2 parmak dokunuş = durum; 3 parmak dokunuş = ayarlar (müzik, efekt, konuşma hızı/ayrıntı, titreşim, kazıma modu, konuşma motoru/ses, öğretici). Ekran açık kalır; kare hızı kazıma sırasında yüksek, boşta düşük.

## Denge hedefleri (Sim zorunlu)
İlk prestij 4–6 sa; tüm ağaç 15–20 sa; ilerleme arası en uzun bekleme < 5 dk; ilk otomasyon 30–45 dk içinde. Sanal oyuncu: sıralı modda ~10 kart/dk elle; en verimli yükseltmeyi alır; otomasyon açılınca ona geçer; prestij fırsatında kullanır (strateji parametreli). Rapor: ilk prestij süresi, tüm ağaç süresi, katalog açılış zamanları, en uzun bekleme.
