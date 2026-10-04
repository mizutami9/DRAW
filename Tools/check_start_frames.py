import glob
import os
from PIL import Image

for f in sorted(glob.glob(r"c:\workSpace\PICO\sample_thumbs\start_*.jpg")):
    img = Image.open(f).convert("RGB")
    px = list(img.getdata())
    avg_r = sum(p[0] for p in px) / len(px)
    avg_g = sum(p[1] for p in px) / len(px)
    avg_b = sum(p[2] for p in px) / len(px)
    # STARTING SOON: pinkish / reddish (high R, lower G/B)
    # Game screen: notebook paper (high R, high G, slightly lower B, very bright)
    idx = int(os.path.basename(f).replace('start_', '').replace('.jpg', ''))
    sec = 8 * 60 + (idx - 1) * 5
    m = sec // 60
    s = sec % 60
    print(f"{os.path.basename(f)} | {m:02d}:{s:02d} | R:{avg_r:.1f} G:{avg_g:.1f} B:{avg_b:.1f}")
