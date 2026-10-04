import sys
import os
sys.path.append(r"c:\workSpace\PICO\Tools")
from safe_range_download import safe_download

sys.stdout.reconfigure(encoding='utf-8')

url = "https://huggingface.co/osa911/nllb-200-distilled-600M-ct2-int8/resolve/main/model.bin"
dest = r"D:\04_tiktok_game\NICODROW\models\nllb-200-distilled-600M-ct2-int8\model.bin"

if os.path.exists(dest) and os.path.getsize(dest) < 619704329:
    print(f"Removing partial file ({os.path.getsize(dest)} bytes)...")
    os.remove(dest)

safe_download(url, dest, chunk_size=4 * 1024 * 1024)
