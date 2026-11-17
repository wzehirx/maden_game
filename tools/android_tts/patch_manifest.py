#!/usr/bin/env python3
"""Maden Android TTS koprusu icin gradle sablonunun AndroidManifest.xml dosyasini yamalar (tekrar calistirmaya duyarli).
Kullanim: patch_manifest.py <AndroidManifest.xml>
Eklenenler:
  1) <queries><intent><action TTS_SERVICE/></intent></queries>  -> Android 11+ paket gorunurlugu; yalnizca TTS motorlari
     gorunur (QUERY_ALL_PACKAGES gerekmez).
  2) <meta-data org.godotengine.plugin.v2.MadenTts = com.maden.oyun.tts.MadenTtsPlugin>  -> Godot plugin v2 kaydi.
"""
import sys

path = sys.argv[1]
src = open(path, encoding="utf-8").read()
changed = False

QUERIES = (
    '    <!-- MADEN_TTS: TTS motorlarini gormek icin en dar paket gorunurlugu kurali -->\n'
    '    <queries>\n'
    '        <intent>\n'
    '            <action android:name="android.intent.action.TTS_SERVICE" />\n'
    '        </intent>\n'
    '    </queries>\n\n'
)
META = (
    '        <!-- MADEN_TTS: Godot Android plugin v2 kaydi (GodotPluginRegistry bu anahtari tarar) -->\n'
    '        <meta-data\n'
    '            android:name="org.godotengine.plugin.v2.MadenTts"\n'
    '            android:value="com.maden.oyun.tts.MadenTtsPlugin" />\n\n'
)

if "android.intent.action.TTS_SERVICE" not in src:
    i = src.index("<application")
    src = src[:i].rstrip(" ") + QUERIES + "    " + src[i:]
    changed = True
if "org.godotengine.plugin.v2.MadenTts" not in src:
    i = src.index("<profileable") if "<profileable" in src else src.index("<activity")
    line_start = src.rfind("\n", 0, i) + 1
    src = src[:line_start] + META + src[line_start:]
    changed = True

if changed:
    open(path, "w", encoding="utf-8").write(src)
    print("AndroidManifest yamalandi:", path)
else:
    print("AndroidManifest zaten yamali:", path)
