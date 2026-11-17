#!/usr/bin/env bash
# Kazı Kazan - tek komutla Android APK (Godot 4.7.2 .NET): import + dışa aktarım -> Builds/KaziKazan.apk
# Kullanım:  tools/build_android.sh            (debug anahtarıyla imzalı APK)
# Ortam değişkenleri (varsayılanlar bu bulut kutusuna göre):
#   GODOT         Godot mono çalıştırılabilir dosyası
#   DOTNET_ROOT   .NET SDK klasörü (8.x)
#   JAVA_HOME     JDK 17
#   ANDROID_HOME  Android SDK (platform-35, build-tools 35.0.0)
set -euo pipefail
cd "$(dirname "$0")/.."
ROOT="$(pwd)"

GODOT="${GODOT:-/opt/godot/Godot_v4.7.2-stable_mono_linux_x86_64/Godot_v4.7.2-stable_mono_linux.x86_64}"
export DOTNET_ROOT="${DOTNET_ROOT:-/opt/dotnet}"
export JAVA_HOME="${JAVA_HOME:-/opt/jdk17}"
export ANDROID_HOME="${ANDROID_HOME:-/opt/android-sdk}"
export PATH="$DOTNET_ROOT:$JAVA_HOME/bin:$PATH"
export DOTNET_CLI_TELEMETRY_OPTOUT=1
VERSION="4.7.2.stable.mono"
TEMPLATES="$HOME/.local/share/godot/export_templates/$VERSION"
SETTINGS="$HOME/.config/godot/editor_settings-4.7.tres"
KEYSTORE="$HOME/.local/share/godot/keystores/debug.keystore"
OUT="Builds/KaziKazan.apk"

echo "== 1/6 Gerekenler kontrol ediliyor"
[ -x "$GODOT" ] || { echo "HATA: Godot bulunamadi: $GODOT"; exit 1; }
[ -f "$TEMPLATES/android_source.zip" ] || { echo "HATA: Android dışa aktarım şablonları yok: $TEMPLATES"; exit 1; }
[ -x "$JAVA_HOME/bin/java" ] || { echo "HATA: JDK yok: $JAVA_HOME"; exit 1; }
[ -d "$ANDROID_HOME/platforms" ] || { echo "HATA: Android SDK yok: $ANDROID_HOME"; exit 1; }
# Bazı arşivlerde JDK'nın süreç başlatıcısı çalıştırma izni olmadan gelir (Gradle daemon başlamaz).
[ -f "$JAVA_HOME/lib/jspawnhelper" ] && [ ! -x "$JAVA_HOME/lib/jspawnhelper" ] && chmod +x "$JAVA_HOME/lib/jspawnhelper" || true

echo "== 2/6 Debug anahtarı ve editör ayarları"
if [ ! -f "$KEYSTORE" ]; then
  mkdir -p "$(dirname "$KEYSTORE")"
  keytool -genkeypair -keystore "$KEYSTORE" -storepass android -alias androiddebugkey -keypass android \
    -keyalg RSA -keysize 2048 -validity 10000 -dname "CN=Android Debug,O=Android,C=US"
fi
if [ ! -f "$SETTINGS" ]; then
  # Godot'yu bir kez açıp ayar dosyasını ürettirir.
  "$GODOT" --headless --editor --path "$ROOT" --quit >/dev/null 2>&1 || true
fi
set_setting() { # anahtar değer
  if grep -q "^$1 = " "$SETTINGS"; then
    sed -i "s#^$1 = .*#$1 = \"$2\"#" "$SETTINGS"
  else
    sed -i "/^\[resource\]/a $1 = \"$2\"" "$SETTINGS"
  fi
}
set_setting export/android/android_sdk_path "$ANDROID_HOME"
set_setting export/android/java_sdk_path "$JAVA_HOME"
set_setting export/android/debug_keystore "$KEYSTORE"
set_setting export/android/debug_keystore_user "androiddebugkey"
set_setting export/android/debug_keystore_pass "android"

echo "== 3/6 Android gradle şablonu (.NET için gerekli)"
if [ ! -f android/build/build.gradle ] || [ "$(cat android/.build_version 2>/dev/null)" != "$VERSION" ]; then
  rm -rf android
  mkdir -p android/build
  unzip -q -o "$TEMPLATES/android_source.zip" -d android/build
  printf '%s' "$VERSION" > android/.build_version
  touch android/build/.gdignore
fi
chmod +x android/build/gradlew

echo "== 3b/6 Android TTS köprüsü (../tools/android_tts -> gradle şablonu)"
# Godot Android plugin v2: GodotPlugin alt sınıfı + manifest meta-data kaydı + <queries> (TTS motoru görünürlüğü).
# Ortak köprü Maden deposunda kalır (kopyalanmaz): ../tools/android_tts. Gerekirse TTS_DIR ile değiştirilebilir.
TTS_DIR="${TTS_DIR:-$ROOT/../tools/android_tts}"
TTS_SRC="$TTS_DIR/src"
TTS_DST="android/build/src/main/java"
[ -d "$TTS_SRC" ] || { echo "HATA: $TTS_SRC yok"; exit 1; }
cp -R "$TTS_SRC/." "$TTS_DST/"
python3 "$TTS_DIR/patch_manifest.py" android/build/src/main/AndroidManifest.xml

echo "== 4/6 İçe aktarma (ses dosyaları vb.) ve C# derlemesi"
"$GODOT" --headless --path "$ROOT" --import >/dev/null 2>&1 || true
"$GODOT" --headless --path "$ROOT" --build-solutions --quit

echo "== 5/6 APK dışa aktarılıyor"
mkdir -p Builds
rm -f "$OUT"
"$GODOT" --headless --path "$ROOT" --export-debug Android "$OUT"

echo "== 6/6 Doğrulama"
[ -s "$OUT" ] || { echo "HATA: APK oluşmadı"; exit 1; }
BT="$ANDROID_HOME/build-tools/35.0.0"
ls -la "$OUT"
"$BT/aapt2" dump badging "$OUT" | sed -n "1,5p" || true
"$BT/apksigner" verify --verbose "$OUT" 2>&1 | grep -E "^Verifies|Verified using v2" || true
LIST="$(unzip -l "$OUT")"
grep -q "mono/publish/arm64/KaziKazan.dll" <<<"$LIST" || { echo "HATA: APK içinde KaziKazan.dll (C# kodu) yok"; exit 1; }
for j in cards upgrades prestige settings; do
  grep -q "assets/data/$j.json" <<<"$LIST" || { echo "HATA: APK içinde data/$j.json yok"; exit 1; }
done
grep -q "assets/Audio/sfx/" <<<"$LIST" || grep -q "Audio/sfx/" <<<"$LIST" || { echo "HATA: APK içinde ses dosyaları yok"; exit 1; }
# TTS köprüsü: Java sınıfı classes.dex'te mi, manifestte queries + plugin meta-data var mı?
# (grep -q + pipefail SIGPIPE verir; bu yüzden grep -c)
unzip -p "$OUT" 'classes*.dex' | strings | grep -c "com/maden/oyun/tts/MadenTtsPlugin" >/dev/null || { echo "HATA: MadenTtsPlugin APK'nın classes.dex dosyasında yok"; exit 1; }
MANI="$("$BT/aapt2" dump xmltree --file AndroidManifest.xml "$OUT")"
grep -q "android.intent.action.TTS_SERVICE" <<<"$MANI" || { echo "HATA: manifestte TTS_SERVICE <queries> yok"; exit 1; }
grep -q "org.godotengine.plugin.v2.MadenTts" <<<"$MANI" || { echo "HATA: manifestte MadenTts plugin meta-data yok"; exit 1; }
echo "TTS köprüsü: sınıf dex'te, <queries> ve plugin v2 meta-data manifestte."
echo "TAMAM: $OUT"
