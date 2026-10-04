import subprocess
import os
import sys
import time

sys.stdout.reconfigure(line_buffering=True, encoding='utf-8')

print("=== STARTING KLANA FULL PIPELINE ===")
print("Step 1: Transcription with faster-whisper...")
t0 = time.time()
res1 = subprocess.run(["py", "Tools/run_klana_transcription.py"])
if res1.returncode != 0:
    print("Transcription failed with returncode:", res1.returncode)
    sys.exit(1)

print(f"\nStep 1 finished in {time.time()-t0:.1f}s")
print("Step 2: Translation with GoogleTranslator...")
t1 = time.time()
res2 = subprocess.run(["py", "Tools/translate_klana_segments.py"])
if res2.returncode != 0:
    print("Translation failed with returncode:", res2.returncode)
    sys.exit(1)

print(f"\nStep 2 finished in {time.time()-t1:.1f}s")

# Step 3: Oshenタイムライン同期版SRTの作成 (Offset = +23.228s)
print("Step 3: Creating Oshen-Timeline-Synced SRT...")
IN_SRT = r"D:\04_tiktok_game\NICODROW\output_klana\japanese_subtitles.srt"
SYNC_SRT = r"D:\04_tiktok_game\NICODROW\output_klana\japanese_subtitles_synced_to_oshen.srt"
OFFSET = 23.228

def parse_time(ts):
    h, m, s_ms = ts.split(':')
    s, ms = s_ms.split(',')
    return int(h)*3600 + int(m)*60 + int(s) + int(ms)/1000.0

def fmt_time(sec):
    if sec < 0: sec = 0
    h = int(sec // 3600)
    m = int((sec % 3600) // 60)
    s = int(sec % 60)
    ms = int(round((sec - int(sec)) * 1000))
    if ms >= 1000: ms = 999
    return f"{h:02d}:{m:02d}:{s:02d},{ms:03d}"

with open(IN_SRT, 'r', encoding='utf-8') as f_in, open(SYNC_SRT, 'w', encoding='utf-8') as f_out:
    lines = f_in.readlines()
    i = 0
    while i < len(lines):
        line = lines[i].strip()
        if '-->' in line:
            parts = line.split(' --> ')
            t_start = parse_time(parts[0]) + OFFSET
            t_end = parse_time(parts[1]) + OFFSET
            f_out.write(f"{fmt_time(t_start)} --> {fmt_time(t_end)}\n")
        else:
            f_out.write(lines[i])
        i += 1

print(f"Saved Oshen-synced SRT to: {SYNC_SRT}")
print(f"=== ALL DONE in {time.time()-t0:.1f}s ===")
