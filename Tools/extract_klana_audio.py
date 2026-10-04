import subprocess
import os
import sys

sys.stdout.reconfigure(encoding='utf-8')

ff = r"C:\Program Files (x86)\Digiarty\VideoProc Vlogger\ffmpeg.exe"
target_name = "[EN⧸GER⧸JP] My pen shall be my weapon!! ｜ NICO DRAW with @novaluxvt @oshenallure @bupu_u [v2885664759].mp4"
target_path = os.path.join(r"D:\04_tiktok_game\NICODROW", target_name)
work_dir = r"D:\04_tiktok_game\NICODROW\work_klana"
os.makedirs(work_dir, exist_ok=True)
out_wav = os.path.join(work_dir, "audio_16k.wav")

print(f"Extracting 16kHz mono audio from:\n{target_path}\nto:\n{out_wav}")

cmd = [
    ff, "-y",
    "-i", target_path,
    "-vn",
    "-acodec", "pcm_s16le",
    "-ar", "16000",
    "-ac", "1",
    out_wav
]

res = subprocess.run(cmd, capture_output=True, text=True, errors="ignore")
if res.returncode != 0:
    print("Error:")
    print(res.stderr[-1000:])
else:
    sz = os.path.getsize(out_wav)
    print(f"Success! Extracted audio: {sz:,} bytes")
