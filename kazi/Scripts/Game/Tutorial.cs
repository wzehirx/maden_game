namespace Kazi.Game
{
    /// <summary>Kısa sesli öğretici metinleri (Türkçe).</summary>
    public static class Tutorial
    {
        public static readonly string[] Lines =
        {
            "Kazı Kazan İmparatorluğu'na hoş geldin. Bu oyunu ekrana bakmadan oynarsın.",
            "Önemli: oyun sırasında ekran okuyucu TalkBack kapalı olmalı. Oyun kendi sesiyle konuşur.",
            "Önce yukarı kaydır: dükkân açılır. Sola ve sağa kaydırarak kartları gezersin. Çift dokunarak satın alırsın.",
            "Kartın varsa ekrana tek dokun: bir kutu açılır. Hepsi açılınca sonucu söylerim.",
            "Parmağını aşağı kaydır: kart bitince sıradaki kart gelir. Tuzaklı kartı yarıda bırakmak için de aşağı kaydır.",
            "Dükkânda iki parmakla sola veya sağa kaydırırsan kategori değişir: kartlar, kazıma, otomasyon, gadget, prestij. Tekrar yukarı kaydırırsan dükkân kapanır.",
            "İki parmakla dokunursan durumunu söylerim.",
            "Üç parmakla dokunursan ayarlar açılır. Müzik, efekt sesi, konuşma hızı, ayrıntı, kazıma modu, konuşma motoru ve titreşim oradan değişir. Sürükleme modunda ekranı parmağınla kazırsın, kaydırmak için ekranın üst kısmından başla.",
            "Sen yokken de otomatik kazıyıcı çalışır. Geri döndüğünde ne kazandığını söylerim.",
            "İyi şanslar."
        };

        public const string Skipped = "Öğretici atlandı. Ayarlardan tekrar dinleyebilirsin.";
        public const string NoVoiceLine = "Türkçe konuşma sesi bulunamadı. Telefonun ayarlarından Türkçe metin okuma verisini yükle. Şimdilik ses ve titreşimle haber veririm.";
    }
}
