import os
from PIL import Image

thumbs_dir = r"D:\04_tiktok_game\NICODROW\work\thumbs"
files = sorted([f for f in os.listdir(thumbs_dir) if f.endswith('.jpg')])

print(f"{'Min':<5} | {'Timestamp':<8} | {'Avg RGB':<18} | {'Bright% (>200)':<15} | {'Assessment'}")
print("-" * 65)

candidates = []

for i, f in enumerate(files):
    minute = i + 1
    h = minute // 60
    m = minute % 60
    ts = f"{h:02d}:{m:02d}:00"
    p = os.path.join(thumbs_dir, f)
    
    with Image.open(p) as img:
        img_small = img.resize((48, 27))
        pixels = list(img_small.getdata())
        
        r_tot = sum(p[0] for p in pixels) / len(pixels)
        g_tot = sum(p[1] for p in pixels) / len(pixels)
        b_tot = sum(p[2] for p in pixels) / len(pixels)
        
        # 明るいピクセルの割合
        bright = sum(1 for p in pixels if p[0] > 180 and p[1] > 180 and p[2] > 170) / len(pixels) * 100
        
        # NICO DRAWは全体的に明るいノート背景（紙色）が60%以上を占める
        is_paper = (bright > 40 and r_tot > 160 and g_tot > 160)
        
        status = "★ NICO DRAW CANDIDATE" if is_paper else ""
        if is_paper:
            candidates.append((minute, ts, f, bright, (r_tot, g_tot, b_tot)))
            
        if is_paper or minute % 10 == 0:
            print(f"{minute:<5} | {ts:<8} | ({r_tot:5.1f},{g_tot:5.1f},{b_tot:5.1f}) | {bright:13.1f}% | {status}")

print("\nCandidate ranges:")
if candidates:
    start_min = candidates[0][0]
    prev_min = candidates[0][0]
    for c in candidates[1:]:
        if c[0] > prev_min + 2:
            print(f"Segment: {start_min}m ({start_min//60:02d}:{start_min%60:02d}:00) ~ {prev_min}m ({prev_min//60:02d}:{prev_min%60:02d}:00)")
            start_min = c[0]
        prev_min = c[0]
    print(f"Segment: {start_min}m ({start_min//60:02d}:{start_min%60:02d}:00) ~ {prev_min}m ({prev_min//60:02d}:{prev_min%60:02d}:00)")
else:
    print("No strong paper-like segments found by brightness alone.")
