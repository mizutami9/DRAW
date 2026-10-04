import subprocess
import os

ff = r"C:\Program Files (x86)\Digiarty\VideoProc Vlogger\ffmpeg.exe"
in_mp4 = r"D:\04_tiktok_game\NICODROW\twitch_fixed.mp4"
sample_srt = r"D:\04_tiktok_game\NICODROW\work\test_sample.srt"
sample_mp4 = r"D:\04_tiktok_game\NICODROW\work\test_sub_mux.mp4"

with open(sample_srt, "w", encoding="utf-8") as f:
    f.write("1\n00:00:01,000 --> 00:00:05,000\n<font color=\"#FF69B4\">[Oshen] テスト字幕です！</font>\n\n2\n00:00:06,000 --> 00:00:10,000\n<font color=\"#00D2FF\">[Klana] 4色色分けテスト！</font>\n")

# Try muxing into mp4 with mov_text
cmd = [
    ff, "-y",
    "-ss", "00:07:33", "-t", "15",
    "-i", in_mp4,
    "-i", sample_srt,
    "-c:v", "copy",
    "-c:a", "copy",
    "-c:s", "mov_text",
    "-metadata:s:s:0", "language=jpn",
    sample_mp4
]
print("Running command:", " ".join(cmd))
res = subprocess.run(cmd, capture_output=True, text=True, errors="ignore")
print("Returncode:", res.returncode)
if res.returncode != 0:
    print("Stderr:", res.stderr[-500:])
else:
    print("Success! File size:", os.path.getsize(sample_mp4))
