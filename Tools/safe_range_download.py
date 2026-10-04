import urllib.request
import os
import time

def safe_download(url, dest, chunk_size=2 * 1024 * 1024):
    print(f"Starting safe range download: {url} -> {dest}")
    # まず総サイズを取得
    req = urllib.request.Request(url, headers={'User-Agent': 'Mozilla/5.0'})
    with urllib.request.urlopen(req) as resp:
        total = int(resp.headers.get('content-length', 0))
    
    print(f"Total size: {total / 1024 / 1024:.2f} MB")
    
    current = 0
    if os.path.exists(dest):
        current = os.path.getsize(dest)
        if current == total:
            print("Already fully downloaded!")
            return
        elif current > total:
            os.remove(dest)
            current = 0
        else:
            print(f"Resuming from {current / 1024 / 1024:.2f} MB...")
    
    with open(dest, 'ab' if current > 0 else 'wb') as out:
        while current < total:
            end = min(current + chunk_size - 1, total - 1)
            headers = {
                'User-Agent': 'Mozilla/5.0',
                'Range': f'bytes={current}-{end}'
            }
            req = urllib.request.Request(url, headers=headers)
            success = False
            for retry in range(5):
                try:
                    with urllib.request.urlopen(req, timeout=30) as resp:
                        data = resp.read()
                        out.write(data)
                        out.flush()
                        current += len(data)
                        success = True
                        break
                except Exception as e:
                    print(f"Retry {retry+1} at {current}: {e}")
                    time.sleep(1)
            if not success:
                raise RuntimeError(f"Failed to download at byte {current}")
            
            if current % (10 * 1024 * 1024) < chunk_size:
                print(f"Progress: {current / 1024 / 1024:.1f} / {total / 1024 / 1024:.1f} MB ({(current/total)*100:.1f}%)")

    print(f"Successfully downloaded to {dest} ({os.path.getsize(dest)} bytes)")

if __name__ == "__main__":
    test_url = "https://huggingface.co/Systran/faster-whisper-tiny.en/resolve/main/model.bin"
    test_dest = r"D:\04_tiktok_game\NICODROW\work\test_tiny_safe.bin"
    safe_download(test_url, test_dest)
