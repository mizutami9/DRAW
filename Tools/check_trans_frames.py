import glob
import os
from PIL import Image

for f in sorted(glob.glob(r"c:\workSpace\PICO\sample_thumbs\trans_*.jpg")):
    img = Image.open(f).convert("RGB")
    px = list(img.getdata())
    avg_r = sum(p[0] for p in px) / len(px)
    avg_g = sum(p[1] for p in px) / len(px)
    avg_b = sum(p[2] for p in px) / len(px)
    idx = int(os.path.basename(f).replace('trans_', '').replace('.jpg', ''))
    sec = 6 * 60 + 50 + (idx - 1) * 5
    m = sec // 60
    s = sec % 60
    is_game = (avg_r > 200 and avg_g > 200 and avg_b > 190)
    tag = "★ GAME SCREEN" if is_game else "STARTING SOON"
    print(f"{os.path.basename(f)} | {m:02d}:{s:02d} | R:{avg_r:.1f} G:{avg_g:.1f} B:{avg_b:.1f} | {tag}")
