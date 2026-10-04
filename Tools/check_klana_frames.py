import subprocess
import os

ff = r"C:\Program Files (x86)\Digiarty\VideoProc Vlogger\ffmpeg.exe"
target_name = "[EN⧸GER⧸JP] My pen shall be my weapon!! ｜ NICO DRAW with @novaluxvt @oshenallure @bupu_u [v2885664759].mp4"
target_path = os.path.join(r"D:\04_tiktok_game\NICODROW", target_name)
out_dir = r"D:\04_tiktok_game\NICODROW\work_klana\check_frames"
os.makedirs(out_dir, exist_ok=True)

cmd = [
    ff, "-y",
    "-ss", "00:07:05",
    "-to", "00:07:15",
    "-i", target_path,
    "-vf", "fps=1,scale=480:270",
    os.path.join(out_dir, "frame_%02d.jpg")
]
subprocess.run(cmd, capture_output=True)
print("Frames extracted:", os.listdir(out_dir))
