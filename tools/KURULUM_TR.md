# Maden - Telefona Kurulum (Android APK)

Bu belge ekran okuyucuyla okunacak şekilde yazıldı. Adımlar numaralıdır, cümleler kısadır.

## A. APK dosyasını telefona gönder

1. APK dosyasının adı Maden.apk. Bilgisayarda Builds klasöründe durur.
2. Dosyayı kendine e-posta ile gönder. Ek olarak ekle.
3. Ya da Google Drive'a yükle. Telefonda Drive uygulamasından indir.
4. Telefonda indirilen Maden.apk dosyasına dokun.
5. Telefon "bilinmeyen kaynaklardan yükleme" izni isteyebilir. İzin ekranı açılırsa, açtığın uygulama için izni aç. Bu uygulama Drive veya Gmail veya Dosyalar olabilir.
6. Geri dön. Yükle düğmesine dokun.
7. Play Protect uyarı verirse "Yine de yükle" seçeneğini seç. APK kendi anahtarımızla imzalıdır, Play Store'da değildir.

## B. Bilgisayardan kablo ile kurulum (adb)

1. Telefonda Ayarlar, Telefon hakkında bölümünde Derleme numarasına yedi kez dokun. Geliştirici seçenekleri açılır.
2. Geliştirici seçeneklerinde USB hata ayıklamayı aç.
3. Telefonu USB kablosuyla bilgisayara bağla. Telefonda çıkan izin sorusuna İzin ver de.
4. Bilgisayarda şu komutu çalıştır: adb install -r Builds/Maden.apk
5. Başarılı olursa ekranda Success yazar.

## C. TalkBack'i oyun sırasında kapat

Oyun kendi sesiyle konuşur. TalkBack açıkken dokunuşlar oyuna ulaşmaz. Bu yüzden oyun sırasında TalkBack kapalı olmalı.

TalkBack'i kapatmanın yolları:

1. Kısayol: Ses artır ve ses azaltı aynı anda üç saniye basılı tut. Kısayol açıksa TalkBack kapanır. Aynı hareket TalkBack'i tekrar açar.
2. Ayarlar yolu: Ayarlar, Erişilebilirlik, TalkBack, en üstteki anahtarı kapat. Onay sorusuna Tamam de.
3. Sesli komut: "Hey Google, TalkBack'i kapat" de.

Oyundan sonra TalkBack'i aynı yollarla geri aç.

Not: Telefon üreticisine göre menü adları biraz değişebilir.

## D. İlk açılış

1. Maden'i aç. Oyun kısa bir sesli öğretici anlatır.
2. Öğreticiyi atlamak için ekrana herhangi bir hareket yap.
3. Konuşma duyulmuyorsa telefonun medya sesini aç.
4. Türkçe konuşma yoksa: Ayarlar, Genel yönetim, Dil, Metin okuma çıkışı bölümünden Google metin okuma motorunu seç. Türkçe ses verisini indir.

## E. Oyun kontrolleri

1. Tek dokunuş: kaz.
2. Aşağı kaydır: çantadaki her şeyi sat.
3. Yukarı kaydır: dükkânı aç veya kapat.
4. Dükkânda sağa veya sola kaydır: ürünleri gez. Ad, fiyat ve etki okunur.
5. Dükkânda çift dokunuş: satın al.
6. İki parmakla dokun: durumu söyler.
7. Üç parmakla dokun: ayarlar menüsü. Sola veya sağa kaydır: ayar seç. Yukarı veya aşağı kaydır: değeri değiştir. Tekrar üç parmakla dokun: kapat.

## F. Bilgisayarda deneme (geliştirici için)

1. Godot 4.7.2 .NET sürümünü aç. Proje klasörü bu depodur.
2. Klavye: Boşluk kaz. S sat. B veya yukarı ok dükkân. Sol ve sağ ok gezin. Enter satın al. İki ve üç tuşları çok parmak.
3. Fare: Sol tuş parmak. Sağ tuş iki parmak dokunuşu. Orta tuş üç parmak dokunuşu.

## G. APK'yı kendin derle

1. Gerekenler: Godot 4.7.2 .NET, .NET SDK 8, JDK 17, Android SDK 35, Godot dışa aktarım şablonları.
2. Komut: tools/build_android.sh
3. Çıktı: Builds/Maden.apk

## H. Önemli not

Bu sürümde dokunma, titreşim ve konuşma gerçek telefonda henüz denenmedi. Sorun olursa konuşma metnini ve telefon modelini not et.
