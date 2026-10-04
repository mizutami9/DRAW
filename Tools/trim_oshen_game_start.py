import os
import subprocess
import sys

sys.stdout.reconfigure(encoding='utf-8')

ff = r"C:\Program Files (x86)\Digiarty\VideoProc Vlogger\ffmpeg.exe"
input_mp4 = r"D:\04_tiktok_game\NICODROW\twitch_fixed.mp4"
output_dir = r"D:\04_tiktok_game\NICODROW\output"
output_mp4 = os.path.join(output_dir, "oshen_from_game_start.mp4")

# Oshenゲーム開始時刻: 00:07:33.00
START_TIME = "00:07:33.00"

cmd = [
    ff, "-y",
    "-ss", START_TIME,
    "-i", input_mp4,
    "-c:v", "h264_nvenc",
    "-preset", "p4",
    "-cq", "20",
    "-c:a", "copy",
    output_mp4
]

print(f"Trimming Oshen video from game start ({START_TIME})...")
res = subprocess.run(cmd)
print("Finished with returncode:", res.returncode)
