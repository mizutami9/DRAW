import urllib.request
import time
import os

url = "https://huggingface.co/Systran/faster-whisper-tiny.en/resolve/main/model.bin"
dest = r"D:\04_tiktok_game\NICODROW\work\test_tiny.bin"

print(f"Testing small-chunk download from {url}...")
start = time.time()
req = urllib.request.Request(url, headers={'User-Agent': 'Mozilla/5.0'})
with urllib.request.urlopen(req) as resp, open(dest, 'wb') as out:
    total = int(resp.headers.get('content-length', 0))
    downloaded = 0
    while True:
        # 小さめのチャンク(32KB)で読み込むことでSchannelバッファオーバーランを防止
        chunk = resp.read(32768)
        if not chunk:
            break
        out.write(chunk)
        downloaded += len(chunk)
        if downloaded % (5 * 1024 * 1024) < 32768:
            print(f"Downloaded {downloaded / 1024 / 1024:.1f} MB / {total / 1024 / 1024:.1f} MB...")

print(f"SUCCESS in {time.time() - start:.2f}s! File size: {os.path.getsize(dest)} bytes")
