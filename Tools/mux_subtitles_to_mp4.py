import subprocess
import os
import sys
import time

sys.stdout.reconfigure(encoding='utf-8')

ff = r"C:\Program Files (x86)\Digiarty\VideoProc Vlogger\ffmpeg.exe"
in_mp4 = r"D:\04_tiktok_game\NICODROW\twitch_fixed.mp4"
in_srt = r"D:\04_tiktok_game\NICODROW\output\twitch_fixed_subtitled.srt"
out_mp4 = r"D:\04_tiktok_game\NICODROW\output\twitch_fixed_subtitled.mp4"

print(f"Multiplexing subtitles into MP4...")
print(f"Input Video: {in_mp4}")
print(f"Input Subtitle: {in_srt}")
print(f"Output Video: {out_mp4}")

t0 = time.time()
cmd = [
    ff, "-y",
    "-i", in_mp4,
    "-i", in_srt,
    "-map", "0:v:0",
    "-map", "0:a:0",
    "-map", "1:s:0",
    "-c:v", "copy",
    "-c:a", "copy",
    "-c:s", "mov_text",
    "-metadata:s:s:0", "language=jpn",
    "-metadata:s:s:0", "title=Japanese (4-Color)",
    out_mp4
]

res = subprocess.run(cmd, capture_output=True, text=True, errors="ignore")
if res.returncode != 0:
    print("Error during muxing:")
    print(res.stderr[-1000:])
else:
    sz = os.path.getsize(out_mp4)
    print(f"Success! Created {out_mp4} ({sz:,} bytes) in {time.time() - t0:.1f}s")
