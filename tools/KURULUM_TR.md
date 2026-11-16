# Maden - Kurulum Rehberi (Windows)

Bu rehber ekran okuyucuyla okunmak için yazıldı. Her adım kısa ve numaralıdır.

## Bölüm 1: Unity kurulumu (bir kez yapılır)

1. unity.com/download adresine git. Unity Hub'ı indir ve kur.
2. Unity Hub'ı aç. Unity hesabınla giriş yap. Hesabın yoksa ücretsiz oluştur.
3. Hub'da kişisel lisansı etkinleştir. Bu ücretsizdir.
4. Hub'da "Installs" (Kurulumlar) bölümüne git. "Install Editor" düğmesine bas.
5. Sürüm olarak 6000.3 LTS seç. Listede 6000.3.28f1 varsa onu seç. Yoksa en yeni 6000.3 sürümünü seç.
6. Modül listesinde şunu işaretle: "Android Build Support".
7. "Android Build Support" altındaki iki kutuyu da işaretle: "OpenJDK" ve "Android SDK & NDK Tools".
8. "Install" düğmesine bas. İndirme birkaç GB'dır. Bitmesini bekle.

## Bölüm 2: APK derleme

1. Maden klasörünü aç. İçinde "tools" klasörü var.
2. "build.bat" dosyasına çift tıkla. Bir komut penceresi açılır.
3. Betik Unity'yi kendisi bulur. İki adım çalışır: önce ayarlar, sonra derleme.
4. İlk derleme 10 ile 30 dakika sürebilir. Sonraki derlemeler daha kısadır.
5. Bitince bilgisayar "Derleme tamamlandi" der. Pencerede "BASARILI" yazar.
6. APK dosyası şu yerdedir: Maden klasörü, "Builds" klasörü, "Maden.apk".
7. Hata olursa pencere "HATA" yazar. Klasördeki "build.log" dosyasını aç. Son satırlarda hatanın nedeni yazar.
8. Unity başka bir yere kuruluysa, "UNITY_PATH" adlı ortam değişkenine Unity.exe'nin tam yolunu yaz. Sonra build.bat'ı tekrar çalıştır.

## Bölüm 3A: APK'yı telefona kur - USB ile

1. Telefonda Ayarlar, Telefon Hakkında bölümüne git.
2. "Yapı numarası" öğesine arka arkaya 7 kez dokun. Geliştirici seçenekleri açılır.
3. Ayarlar, Sistem, Geliştirici Seçenekleri bölümüne git. "USB hata ayıklama" seçeneğini aç.
4. Telefonu USB kabloyla bilgisayara bağla.
5. Telefon "USB hata ayıklamaya izin verilsin mi" diye sorar. "İzin ver" de.
6. Bilgisayarda "tools" klasöründeki "install_apk.bat" dosyasına çift tıkla.
7. "BASARILI" yazarsa Maden kurulmuştur. Uygulama listesinde "Maden" adıyla görünür.

## Bölüm 3B: APK'yı telefona kur - dosya göndererek

1. "Builds\Maden.apk" dosyasını Google Drive'a yükle. Ya da kendine e-posta ile gönder.
2. Telefonda dosyayı indir. Drive uygulaması veya e-posta uygulaması bunu yapar.
3. İndirilen "Maden.apk" dosyasını aç.
4. Telefon "Bilinmeyen kaynaklardan yükleme" izni isteyebilir. Açılan ayarda, o uygulamaya (Drive, Gmail veya Dosyalar) izin ver.
5. Geri dön. "Yükle" düğmesine bas.
6. Google Play Protect uyarı verirse "Yine de yükle" seç. Bu uygulamayı kendin derledin, güvenlidir.

## Bölüm 4: TalkBack ile deneme

1. Maden'i aç. Oyun kendisi Türkçe konuşur. Önce kısa bir öğretici dinlersin.
2. Öğreticiyi geçmek için ekrana bir kez dokun.
3. Tek parmakla dokun: kazar.
4. Parmağı aşağı kaydır: hepsini satar.
5. Parmağı yukarı kaydır: dükkân açılır ve kapanır.
6. Dükkânda sola ve sağa kaydır: ürünler arasında gezer. Çift dokun: satın alır.
7. İki parmakla dokun: durumunu söyler.
8. Üç parmakla dokun: ayarlar menüsü açılır. Tekrar üç parmakla dokun: kapanır.
9. TalkBack açıkken kaydırma çalışmayabilir. Bunun nedeni, TalkBack'in bazı hareketleri kendine ayırmasıdır. Telefon modeline göre değişir. Bu bölümü gerçek telefonda mutlaka dene.
10. Sorun olursa TalkBack'i geçici olarak duraklat. TalkBack menüsünde "TalkBack'i duraklat" seçeneği vardır.

## Bölüm 5: Ses tuşu yedeği

Dokunma hiç çalışmazsa oyunu ses tuşlarıyla oynayabilirsin.

1. Ses artır tuşu: kazar.
2. Ses azalt tuşu: hepsini satar.
3. İki tuşa birden kısa bas: dükkân açılır veya kapanır.
4. Dükkânda ses artır: sonraki ürün. Ses azalt: önceki ürün.
5. Dükkânda ses artır tuşunu yarım saniye basılı tut: satın alır.
6. İki ses tuşunu 3 saniyeden uzun basılı tutma. Android bunu erişilebilirlik kısayolu sayıp TalkBack'i açıp kapatabilir.
7. Maden açıkken ses tuşları telefonun sesini değiştirmez. Ses seviyesini oyunun ayarlar menüsünden değiştir.

## Bölüm 6: Telefonu uzun süre açık bırakma

1. Maden açıkken ekran kapanmaz. Pil tüketimi için telefonu şarja tak.
2. Ekranı elle kilitlersen oyun duraklar. Geri açınca, yokken kazandığını söyler.
3. Telefonun pil tasarrufu modu uygulamayı kapatabilir. Ayarlar, Pil bölümünden Maden'e "Kısıtlama yok" izni ver.
