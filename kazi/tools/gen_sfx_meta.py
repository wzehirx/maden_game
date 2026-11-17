#!/usr/bin/env python3
"""Ses dosyalarini ffmpeg ile olcer; kazi/Scripts/Game/SfxMeta.cs uretir (dosya secimini DEGISTIRMEZ).
Her dosya icin: gain_db (seviye dengeleme), baslangic (bas sessizlik kirpma), bitis (kuyruk kirpma).
Kullanim: python3 kazi/tools/gen_sfx_meta.py   (ffmpeg/ffprobe gerekir)"""
import re, subprocess, pathlib
root = pathlib.Path(__file__).resolve().parent.parent
audio = root / "Audio"
out = root / "Scripts/Game/SfxMeta.cs"

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

# (onek, sure ust siniri sn, ek dB). Ilk eslesen kural gecerli. Kisa vuruslar kirpilir ve oyunda sonumlenir.
RULES = [
    ("scratch_short", 0.40, -3.0), ("boundary_tick", 0.20, -5.0), ("cell_reveal", 0.50, 0.0),
    ("match_tick", 0.40, -1.0), ("scratch_", 0.90, -3.0),
    ("win_chime", 1.30, 0.0), ("win_small", 1.50, -1.0), ("win_mid", 1.50, 0.0), ("win_big", 2.50, 0.0),
    ("jackpot", 5.00, 0.0), ("prestige_swell", 5.00, 0.0), ("catalog_unlock", 2.70, 0.0),
    ("lose_thud", 0.60, -1.0), ("trap_", 1.20, 0.0), ("ticket_tear", 0.70, -1.0),
    ("coin_bag", 0.30, -2.0), ("coin_single", 0.60, -2.0), ("coin_slide", 0.40, -2.0), ("coins_drop", 1.50, -2.0), ("coin_count", 1.20, -2.0),
    ("banknote", 0.60, -2.0), ("cash_drawer", 0.45, -1.0), ("help_money", 1.00, 0.0),
    ("gadget_", 1.20, -6.0), ("counter_bell", 1.50, -5.0),
    ("buy_", 1.00, -1.0), ("menu_", 1.00, -3.0), ("page_", 0.70, -3.0), ("card_", 1.00, -3.0),
    ("error_wood", 0.60, -2.0), ("paper_", 1.00, -2.0),
]
def rule(name):
    for pre, cap, off in RULES:
        if name.startswith(pre): return cap, off
    return 1.0, 0.0

rows = []
for f in sorted((audio/"sfx").glob("*.ogg")):
    name = f.stem; d = dur(f)
    if name.endswith("_loop"):
        # Gadget/otomasyon dongusu: kirpma yok, cok kisik (-26 dB ortalama hedefi)
        mean, peak = active_mean(f); g = round(max(-12, min(6, -26 - mean)), 1)
        rows.append((f"sfx/{name}", g, 0.0, 0.0)); continue
    cap, off = rule(name)
    lead, tail = silences(f, d)
    if lead < 0.02: lead = 0.0
    end = min(tail, cap, d)
    if end - lead < 0.05: lead = 0.0
    mean, peak = active_mean(f)
    g = -20 - mean
    g = max(-10.0, min(6.0, g)); g = min(g, 1.0 - peak)
    g = max(-14.0, g + off)
    end = 0.0 if abs(end - d) < 0.01 else end   # 0 = dokunma (dosyanin tamami)
    rows.append((f"sfx/{name}", round(g, 1), round(lead, 3), round(end, 3)))
for f in sorted((audio/"ambient").glob("*.ogg")):
    g = max(-12, min(6, -20 - lufs(f))); rows.append((f"ambient/{f.stem}", round(g, 1), 0.0, 0.0))
for f in sorted((audio/"music").glob("*.ogg")):
    g = max(-12, min(6, -20 - lufs(f))); rows.append((f"music/{f.stem}", round(g, 1), 0.0, 0.0))

with open(out, "w", encoding="utf-8") as o:
    o.write("// OTOMATIK URETILDI: tools/gen_sfx_meta.py (ffmpeg olcumu). Elle degistirme; scripti yeniden calistir.\n")
    o.write("// gain_db: seviye dengeleme; start/end: bas sessizlik ve kuyruk kirpma (saniye, 0 = dokunma).\n")
    o.write("using System.Collections.Generic;\n\nnamespace Kazi.Game\n{\n    public static class SfxMeta\n    {\n")
    o.write("        public readonly struct Info { public readonly float GainDb, Start, End; public Info(float g, float s, float e) { GainDb = g; Start = s; End = e; } }\n")
    o.write("        public static readonly Dictionary<string, Info> Table = new Dictionary<string, Info>\n        {\n")
    for n, g, s, e in rows:
        o.write(f'            {{ "{n}", new Info({g}f, {s}f, {e}f) }},\n')
    o.write("        };\n    }\n}\n")
print("yazildi", out, len(rows))
for r in rows: print(r)
