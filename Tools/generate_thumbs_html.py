import os

thumbs_dir = r"D:\04_tiktok_game\NICODROW\work\thumbs"
html_path = r"c:\workSpace\PICO\Tools\thumbs_preview.html"

files = sorted([f for f in os.listdir(thumbs_dir) if f.endswith('.jpg')])

html = ["<html><head><meta charset='utf-8'><title>Thumbnail Preview</title>",
        "<style>body{background:#222;color:#fff;font-family:sans-serif;} .grid{display:flex;flex-wrap:wrap;gap:8px;} .item{border:1px solid #444;padding:4px;text-align:center;} img{width:240px;height:135px;display:block;margin-bottom:4px;}</style>",
        "</head><body><h2>Thumbnail Timeline (1 frame / minute)</h2><div class='grid'>"]

for i, f in enumerate(files):
    minute = i + 1
    h = minute // 60
    m = minute % 60
    ts = f"{h:02d}:{m:02d}:00"
    p = os.path.join(thumbs_dir, f).replace('\\', '/')
    html.append(f"<div class='item'><div><b>{ts}</b> (#{minute}m)</div><img src='file:///{p}'><div>{f}</div></div>")

html.append("</div></body></html>")

with open(html_path, "w", encoding="utf-8") as out:
    out.write("\n".join(html))

print(f"Created {html_path} with {len(files)} thumbnails")
