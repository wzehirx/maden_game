namespace Maden.Game
{
    /// <summary>Kısa sesli öğretici metinleri (Türkçe).</summary>
    public static class Tutorial
    {
        public static readonly string[] Lines =
        {
            "Maden'e hoş geldin. Bu oyunu ekrana bakmadan oynarsın.",
            "Önemli: oyun sırasında ekran okuyucu TalkBack kapalı olmalı. Oyun kendi sesiyle konuşur.",
            "Ekrana tek dokun: kazarsın. Hızlı iki kez dokunursan iki kez kazarsın.",
            "Parmağını aşağı kaydır: çantadaki her şeyi satarsın.",
            "Parmağını yukarı kaydır: dükkân açılır. Dükkânda sola ve sağa kaydırarak ürünleri gezersin. İki parmakla sola veya sağa kaydırırsan kategori değişir: kazma, vuruş, çanta, makineler. Çift dokunarak satın alırsın. Tekrar yukarı kaydırırsan dükkân kapanır.",
            "İki parmakla dokunursan durumunu söylerim.",
            "Üç parmakla dokunursan ayarlar açılır. Müziği, sesi, konuşma hızını, konuşma sesini ve titreşimi oradan değiştirirsin.",
            "Sen yokken de oyun ilerler. Geri döndüğünde ne kazandığını söylerim.",
            "İyi kazılar."
        };

        public const string Skipped = "Öğretici atlandı. Ayarlardan tekrar dinleyebilirsin.";
        public const string NoVoiceLine = "Türkçe konuşma sesi bulunamadı. Telefonun ayarlarından Türkçe metin okuma verisini yükle. Şimdilik ses ve titreşimle haber veririm.";
    }
}
