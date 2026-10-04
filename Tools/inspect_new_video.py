import subprocess
import os
import sys
import json

sys.stdout.reconfigure(encoding='utf-8')

ff = r"C:\Program Files (x86)\Digiarty\VideoProc Vlogger\ffmpeg.exe"
target_name = "[EN⧸GER⧸JP] My pen shall be my weapon!! ｜ NICO DRAW with @novaluxvt @oshenallure @bupu_u [v2885664759].mp4"
target_path = os.path.join(r"D:\04_tiktok_game\NICODROW", target_name)

print("Target exists:", os.path.exists(target_path))
print("Target size:", os.path.getsize(target_path) if os.path.exists(target_path) else 0)

# Run ffprobe / ffmpeg to get metadata
cmd = [ff, "-i", target_path]
res = subprocess.run(cmd, capture_output=True, text=True, errors="ignore")
for line in res.stderr.splitlines():
    if any(k in line for k in ["Duration", "Stream #0", "Video:", "Audio:"]):
        print(line.strip())
