# PROGRESS — Kazı Kazan İmparatorluğu (sesli idle oyun)

## Aşama 0 — Ortam kontrolü (TAMAM, 2026-11-17)
- Motor: Unity DEĞİL, **Godot 4.7.2 (.NET/C#)**. Neden: Unity Personal lisansı başsız etkinleştirilemiyor (manuel etkinleştirme kapalı), Godot ile APK bu kutuda derlenip doğrulanıyor. (Önceki oyun "Maden" aynı yığınla çalışıyor.)
- Kurulu ve doğrulandı: Godot 4.7.2 mono (/opt/godot), export şablonları (~/.local/share/godot/export_templates/4.7.2.stable.mono), .NET SDK 8.0.428 (/opt/dotnet), Android SDK platform 35/36 + build-tools 35.0.0/36.1.0 (/opt/android-sdk), JDK 17 (/opt/jdk17; varsayılan java 21, Godot betiği JAVA_HOME=/opt/jdk17 kullanır), debug keystore.
- Ağ: ortam 'unrestricted'. Ses kaynakları: archive.org (Sonniss GDC aynaları) ÇALIŞIYOR; freesound/pixabay/kenney bu kutudan 403.
- Proje yeri: bu depoda `kazi/` klasörü (Maden'e dokunulmaz). Maden'den yeniden kullanılacak: GestureRecognizer, Speech(TTS), Haptics, SoundPlayer mimarisi, build_android.sh, SfxMeta üretimi.
- Önceki oyunda öğrenilen: TTS varsayılan motorla (voice id boş), Microsoft sesi sorunu; hantallık (kare hızı, ses önyükleme); dükkân yazısı büyük olmalı; sesler gerçek foley olmalı (sentez YOK).
- Açık risk: .NET 8/9 uyumu (Godot şablonu net9.0 bekliyor, gradle yoluyla net8.0 paketliyoruz; cihazda doğrulandı: Maden açılıyor).
