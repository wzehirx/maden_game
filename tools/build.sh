#!/usr/bin/env bash
# Maden - Linux/macOS APK derleme betiği.
# Kullanım: tools/build.sh   (isteğe bağlı: UNITY_PATH=/yol/Unity)
set -euo pipefail
cd "$(dirname "$0")/.."
PROJECT="$(pwd)"

find_unity() {
  if [ -n "${UNITY_PATH:-}" ]; then echo "$UNITY_PATH"; return; fi
  local c
  for c in \
    "$HOME"/Unity/Hub/Editor/6000.3.*/Editor/Unity \
    /Applications/Unity/Hub/Editor/6000.3.*/Unity.app/Contents/MacOS/Unity \
    "$HOME"/Unity/Hub/Editor/6000.*/Editor/Unity \
    /Applications/Unity/Hub/Editor/6000.*/Unity.app/Contents/MacOS/Unity; do
    if [ -x "$c" ]; then echo "$c"; return; fi
  done
}

UNITY="$(find_unity || true)"
if [ -z "${UNITY:-}" ]; then
  echo "HATA: Unity 6000.3 bulunamadı. UNITY_PATH ayarlayın." >&2
  exit 1
fi
echo "Unity: $UNITY"

echo "[1/2] Proje ayarları uygulanıyor..."
"$UNITY" -batchmode -nographics -quit -projectPath "$PROJECT" -buildTarget Android \
  -executeMethod MadenBuild.Configure -logFile "$PROJECT/configure.log"

echo "[2/2] APK derleniyor..."
if ! "$UNITY" -batchmode -nographics -quit -projectPath "$PROJECT" -buildTarget Android \
  -executeMethod MadenBuild.BuildAndroid -logFile "$PROJECT/build.log"; then
  echo "HATA: Derleme başarısız. build.log sonu:" >&2
  tail -n 40 "$PROJECT/build.log" >&2 || true
  exit 1
fi
test -f "$PROJECT/Builds/Maden.apk" && echo "BAŞARILI: $PROJECT/Builds/Maden.apk"
