import glob
import os
from PIL import Image

for i, f in enumerate(sorted(glob.glob(r"c:\workSpace\PICO\sample_thumbs\sec_*.jpg"))):
    img = Image.open(f).convert("RGB")
    px = list(img.getdata())
    avg_r = sum(p[0] for p in px) / len(px)
    avg_g = sum(p[1] for p in px) / len(px)
    avg_b = sum(p[2] for p in px) / len(px)
    sec = 30 + i
    is_paper = (avg_g > 210)
    tag = "★ NICO DRAW START" if is_paper else "Starting Soon"
    print(f"07:{sec:02d} | R:{avg_r:.1f} G:{avg_g:.1f} B:{avg_b:.1f} | {tag}")
