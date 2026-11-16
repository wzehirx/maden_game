# PROGRESS — Maden (sesli idle oyun)

## Aşama 0 — Ortam kontrolü (TAMAM)
Bu bulut kutusunda (Linux, 4 çekirdek, 15 GB RAM, ffmpeg + Java + Python 3.13 + Node 22 var):
- Unity Editor: YOK. Unity lisans etkinleştirmesi (hesap girişi) gerektirdiği için burada kurulup APK derlenemez.
- Android SDK / dotnet: YOK ama indirilebilir (dl.google.com, builds.dotnet.microsoft.com açık). Saf C# mantığı + denge simülasyonu dotnet ile BURADA derlenip test edilebilir.
- Ağ: unrestricted. Ses siteleri: opengameart.org, archive.org, incompetech.com AÇIK; freesound.org, kenney.nl, pixabay.com 403 (bot koruması) -> kullanılamaz/arka plan çözümü gerekir.

## Kararlar
- Oyun mantığı Unity'den bağımsız saf C# (Core/), dotnet test ile doğrulanır.
- APK derlemesi için iki yol hazırlanacak: (a) Windows PC'de tek komutluk batchmode betiği, (b) GitHub Actions (game-ci) iş akışı.
- Bu ortamda APK derleyemediğim için "APK derlendi" iddiasında bulunmayacağım; derleme kullanıcı tarafında doğrulanacak.

## Sıradaki adım
Aşama 1: Haiku araştırmaları (erişilebilirlik/TTS/titreşim, Unity LTS, ses kaynakları).

## KARAR (Aşama 3 sırasında): Unity BIRAKILDI -> yerel Android (Kotlin + Gradle)
Neden: bu kutuda Android SDK + Gradle çalışıyor, APK GERÇEKTEN derlenip doğrulanabiliyor; TalkBack dokunma kontrolü doğrudan; pil düşük.
- `android/` Gradle projesi: `core` (Kotlin JVM, C# Core'un portu), `app` (Activity + GameView).
- Ses dosyaları tek kaynak: Assets/Resources/Audio (Gradle assets srcDir). Sentez ses YOK.
- Eski Unity dosyaları (Assets/Scripts, Assets/Editor, Assets/Plugins, tools/stubs, workflow) taşıma bitince kaldırılacak. Assets/Scripts/Core (C#) denge simülasyonu için referans.
- Freesound bu ortamdan erişilemiyor (ağ katmanı 403); kullanıcı PC'sinde tools/fetch_sounds.py ile eklenebilir.
- Ağ: ortam 'limited' moda alındı, gerekli hostlar izinli.
