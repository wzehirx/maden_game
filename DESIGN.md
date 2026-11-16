# Maden - Teknik Tasarım

## Motor
Godot 4.7.2 (.NET / C#). Unity bırakıldı (lisans alınamadı). Proje kökü = depo kökü (`project.godot`, `Maden.csproj`, `Maden.sln`).

## Mimari
- `Assets/Scripts/Core/` (saf C#, namespace `Maden.Core`, Godot'siz): ekonomi, eşya düşürme, kayıt, çevrimdışı kazanç, oyun durumu, olay sistemi ve `GestureRecognizer` (ham dokunma olayından hareket tanıma; eşikler dpi ve ekran boyuna oranlı).
- `Assets/Scripts/Game/` (Godot katmanı, namespace `Maden.Game`): `Main` (kodla sahne kurar), `InputRouter` (InputEventScreenTouch/Drag + fare/klavye yedeği -> RawTouch), `Speech` (DisplayServer TTS, Türkçe ses seçimi, öncelik/spam kuralları), `SoundPlayer` (AudioBus: Music, Sfx, Ambient), `Haptics` (Input.VibrateHandheld desenleri), `Settings` (user://settings.cfg), `SettingsMenu`, `Tutorial`, `GameController`.
- `Core.Tests/` (xUnit, GestureTests dahil) ve `Sim/` (denge simülasyonu) ayrı dotnet projeleri; `Maden.csproj` bunları derlemeden çıkarır. İkisi de Core dosyalarını doğrudan derler.
- Veri: `Assets/StreamingAssets/economy.json` (res:// olarak okunur; export filtresi `*.json`). Kayıt: `user://save.json` (+ .bak).
- Sesler: `Assets/Resources/Audio/{sfx,music,ambient}/*.ogg` (res://). Sentez yok; dosya eksikse o olayda sessiz + uyarı.

## Erişilebilirlik
Oyun kendi kendini seslendirir (Godot TTS = Android TextToSpeech). TalkBack oyun sırasında KAPALI olmalıdır; TalkBack açıkken dokunuşlar oyuna ulaşmaz. Türkçe ses yoksa titreşim ve ses efektiyle haber verilir.

## Kontroller
Tek dokunuş kaz; aşağı kaydır sat; yukarı kaydır dükkân; dükkânda sağ/sol gezin, çift dokunuş satın al; 2 parmak durum; 3 parmak ayarlar. Masaüstü: Space/S/B/oklar/Enter/2/3, fare sol/sağ/orta.

## Pil
Engine.MaxFps=20, OS.LowProcessorUsageMode, GL Compatibility, ekran açık kalır. Otomatik kayıt 30 sn + arka plana geçerken + kapanırken.

## Ekonomi
10 katman (Toprak, Kil, Taş, Kömür, Demir, Gümüş, Altın, Kristal, Elmas, Kadim Hazine); yükseltmeler maliyet = taban x çarpan^seviye; hedef bitiş 15-20 saat, en uzun bekleme < 5 dk (Sim doğrular).

## Android derleme
`tools/build_android.sh` -> `Builds/Maden.apk` (gradle yolu: .NET için gerekli; android/build şablonu android_source.zip'ten açılır). Debug anahtarıyla imzalı. Ayrıntı: `tools/KURULUM_TR.md`, CI: `.github/workflows/build.yml`.
