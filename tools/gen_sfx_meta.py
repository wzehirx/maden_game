#!/usr/bin/env python3
"""Ses dosyalarini ffmpeg ile olcer; Assets/Scripts/Game/SfxMeta.cs uretir (dosya secimini DEGISTIRMEZ).
Her dosya icin: gain_db (seviye dengeleme), baslangic (bas sessizlik kirpma), bitis (kuyruk kirpma)."""
import re, subprocess, sys, pathlib
root = pathlib.Path(__file__).resolve().parent.parent
audio = root / "Assets/Resources/Audio"
out = root / "Assets/Scripts/Game/SfxMeta.cs"

def run(args):
    return subprocess.run(args, capture_output=True, text=True).stderr

def dur(f):
    r = subprocess.run(["ffprobe","-v","error","-show_entries","format=duration","-of","csv=p=0",str(f)],capture_output=True,text=True)
    return float(r.stdout.strip())

def silences(f, d):
    txt = run(["ffmpeg","-nostats","-i",str(f),"-af","silencedetect=n=-40dB:d=0.02","-f","null","-"])
    starts = [float(x) for x in re.findall(r"silence_start: (-?[\d.]+)", txt)]
    ends = [float(x) for x in re.findall(r"silence_end: ([\d.]+)", txt)]
    lead = 0.0
    if starts and starts[0] < 0.005 and ends: lead = ends[0]
    tail = d
    if len(starts) > len(ends) and starts: tail = max(starts[-1], 0.0)
    return lead, tail

def active_mean(f):
    txt = run(["ffmpeg","-nostats","-i",str(f),"-af","silenceremove=start_periods=1:start_threshold=-40dB:stop_periods=-1:stop_threshold=-40dB:stop_duration=0.05,volumedetect","-f","null","-"])
    m = re.search(r"mean_volume: (-?[\d.]+)", txt); p = re.search(r"max_volume: (-?[\d.]+)", txt)
    return float(m.group(1)), float(p.group(1))

def lufs(f):
    txt = run(["ffmpeg","-nostats","-i",str(f),"-af","ebur128","-f","null","-"])
    return float(re.findall(r"I:\s+(-?[\d.]+) LUFS", txt)[-1])

# sure ust sinirlari (sn): kisa vuruslar 0.5, kisa arayuz sesleri 0.8, bulus/yukseltme 1.5, patlama 1.5, fanfar 2.3
def cap(name):
    if name.endswith("_hit") or "_hit_" in name: return 0.5
    if name.startswith(("sell","buy","error","menu_","fuse")): return 0.8
    if name.startswith(("rare_find","legend_find","upgrade","dynamite")): return 1.5
    if name.startswith("fanfare"): return 2.3
    return 1.0

rows = []
for f in sorted((audio/"sfx").glob("*.ogg")):
    name = f.stem; d = dur(f)
    if name.startswith("drill_loop"):
        mean, peak = active_mean(f); g = round(max(-10, min(6, -20 - mean)),1); rows.append((f"sfx/{name}", g, 0.0, 0.0)); continue
    lead, tail = silences(f, d)
    if lead < 0.02: lead = 0.0
    end = min(tail, cap(name), d)
    if end - lead < 0.08: lead = 0.0
    mean, peak = active_mean(f)
    g = -20 - mean
    g = max(-10.0, min(6.0, g)); g = min(g, 1.0 - peak)
    rows.append((f"sfx/{name}", round(g,1), round(lead,3), round(end,3)))
for f in sorted((audio/"ambient").glob("*.ogg")):
    g = max(-12, min(6, -20 - lufs(f))); rows.append((f"ambient/{f.stem}", round(g,1), 0.0, 0.0))
for f in sorted((audio/"music").glob("*.ogg")):
    g = max(-12, min(6, -20 - lufs(f))); rows.append((f"music/{f.stem}", round(g,1), 0.0, 0.0))

with open(out, "w", encoding="utf-8") as o:
    o.write("// OTOMATIK URETILDI: tools/gen_sfx_meta.py (ffmpeg olcumu). Elle degistirme; scripti yeniden calistir.\n")
    o.write("// gain_db: seviye dengeleme; start/end: bas sessizlik ve kuyruk kirpma (saniye, 0 = dokunma).\n")
    o.write("using System.Collections.Generic;\n\nnamespace Maden.Game\n{\n    public static class SfxMeta\n    {\n")
    o.write("        public readonly struct Info { public readonly float GainDb, Start, End; public Info(float g, float s, float e) { GainDb = g; Start = s; End = e; } }\n")
    o.write("        public static readonly Dictionary<string, Info> Table = new Dictionary<string, Info>\n        {\n")
    for n, g, s, e in rows:
        o.write(f'            {{ "{n}", new Info({g}f, {s}f, {e}f) }},\n')
    o.write("        };\n    }\n}\n")
print("yazildi", out, len(rows))
for r in rows: print(r)
