# Maden — Teknik Tasarım

## Mimari
- `Core/` (Unity'siz saf C#, netstandard2.1 / net8 testlenebilir): Ekonomi, eşya düşürme, kayıt, çevrimdışı kazanç, oyun durumu, komut/olay sistemi.
- `Core.Tests/` (xUnit): Core için testler. `Sim/` : denge simülasyonu (konsol).
- `Assets/Scripts/` : Core dosyaları Unity'ye `Assets/Scripts/Core` olarak SEMBOLİK DEĞİL, kopya/derleme-ilişkisiyle girer (Core klasörü doğrudan Assets/Scripts/Core altında durur; dotnet projeleri oraya dosya referansı verir).
- Unity katmanı: `Bootstrap` (RuntimeInitializeOnLoadMethod ile sahne kurulumu), `InputRouter` (hareket tanıma), `Speech` (Android TTS), `Haptics`, `AudioManager`, `GameController`.
- Veri: `Assets/StreamingAssets/economy.json` (tek ekonomi dosyası). Sim ve oyun aynısını okur.

## Erişilebilirlik kararı (en sağlam yol)
Araştırma: Unity AssistiveSupport node'ları ekran okuyucu içindir; özel hareketler (kaydırma/çok parmak) TalkBack açıkken doğrulanmış biçimde oyuna ulaşmıyor. Bu yüzden:
1. Oyun KENDİ kendini Android TextToSpeech (tr-TR) ile seslendirir; TalkBack konuşması gerekmez.
2. Dokunma: Özel bir Android plugin (UnityPlayerActivity alt sınıfı, `.androidlib`) hem normal `onTouchEvent` hem de TalkBack'in tek parmak dokunuşları dönüştürdüğü `onGenericMotionEvent` HOVER olaylarını yakalar ve aynı hareket tanıyıcıya besler (hover'daki basıp-bırak = dokunuş, hover kaydırma = kaydırma). Çok parmaklı dokunuşlar ham `MotionEvent` olarak da dinlenir.
3. Güvenlik ağı (ses tuşları): Ses artır = kaz, ses azalt = hepsini sat, ikisi birden = dükkân. (TalkBack bu tuşları engellemez.) Bu, TalkBack'te dokunma geçişi cihazda çalışmazsa oyunu yine oynanabilir kılar.
4. Açılışta `isTouchExplorationEnabled` algılanırsa oyun bunu sesli söyler ve ses tuşlarını anlatır.
RİSK: Hover dönüşümü cihaz/Android sürümüne göre değişebilir; gerçek cihazda TalkBack açıkken test edilmeli. Çalışmazsa tavsiye: TalkBack'i oyun sırasında duraklat veya ses tuşlarını kullan.

## Ekonomi (economy.json'dan; sayılar simülasyonla ayarlanır)
- 10 katman: Toprak, Kil, Taş, Kömür, Demir, Gümüş, Altın, Kristal, Elmas, Kadim Hazine.
- Her katman: derinlik (blok sayısı), blok dayanıklılığı, eşya havuzu (sık/seyrek/çok nadir + değer).
- Yükseltmeler: vuruş gücü, kritik şansı, nadir şansı, çanta boyutu, kazma kademeleri (tahta→elmas), otomatik satış, dinamit (güç/hız), matkap (güç/hız), güçlü sürümler. Maliyet = taban × çarpan^seviye.
- Hedef: bitiş 15–20 saat, en uzun bekleme < 5 dk (Sim doğrular).

## Dosya düzeni
Core/ , Core.Tests/ , Sim/ , Assets/ (Scripts, Editor, Plugins/Android, StreamingAssets, Resources/Audio), ProjectSettings/ , Packages/ , tools/ (build.bat, build.sh), .github/workflows/build.yml
