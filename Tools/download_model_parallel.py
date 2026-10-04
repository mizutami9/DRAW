import urllib.request
import os
import sys
import time
from concurrent.futures import ThreadPoolExecutor

sys.stdout.reconfigure(line_buffering=True)

MODEL_DIR = r"D:\04_tiktok_game\NICODROW\work\models\small.en"
os.makedirs(MODEL_DIR, exist_ok=True)

def download_range(url, start_byte, end_byte, max_retries=10):
    headers = {
        'User-Agent': 'Mozilla/5.0 (Windows NT 10.0; Win64; x64)',
        'Range': f'bytes={start_byte}-{end_byte}'
    }
    req = urllib.request.Request(url, headers=headers)
    for attempt in range(max_retries):
        try:
            with urllib.request.urlopen(req, timeout=20) as resp:
                data = resp.read()
                if len(data) == (end_byte - start_byte + 1):
                    return data
        except Exception as e:
            time.sleep(0.5 + attempt * 0.5)
    raise RuntimeError(f"Failed to download chunk {start_byte}-{end_byte}")

def download_file_segmented(url, dest_path, chunk_size=2 * 1024 * 1024, max_workers=4):
    print(f"Resolving {dest_path}...")
    req = urllib.request.Request(url, headers={'User-Agent': 'Mozilla/5.0'})
    with urllib.request.urlopen(req) as resp:
        final_url = resp.geturl()
        total_size = int(resp.headers.get('content-length', 0))
    
    print(f"File: {os.path.basename(dest_path)} | Total Size: {total_size / 1024 / 1024:.2f} MB")
    
    if os.path.exists(dest_path) and os.path.getsize(dest_path) == total_size:
        print(f"Already complete: {dest_path}")
        return

    # チャンク一覧を作成
    chunks = []
    for s in range(0, total_size, chunk_size):
        e = min(s + chunk_size - 1, total_size - 1)
        chunks.append((s, e))

    print(f"Total chunks: {len(chunks)} ({chunk_size / 1024 / 1024:.1f} MB each)")
    
    # 既存のダウンロード済みサイズを調べる（シーケンシャル追記用）
    current_size = 0
    if os.path.exists(dest_path):
        current_size = os.path.getsize(dest_path)
        # chunk_size の倍数まで切り捨てて再開
        valid_chunks = current_size // chunk_size
        current_size = valid_chunks * chunk_size
        if current_size > 0:
            print(f"Resuming from chunk {valid_chunks} ({current_size / 1024 / 1024:.2f} MB)...")
            with open(dest_path, "r+b") as f:
                f.truncate(current_size)
    
    remaining_chunks = [c for c in chunks if c[0] >= current_size]
    
    start_time = time.time()
    with open(dest_path, 'ab' if current_size > 0 else 'wb') as out_f:
        downloaded = current_size
        # 安定性を保つため順次取得（または小さな並行バッファ）
        with ThreadPoolExecutor(max_workers=max_workers) as executor:
            batch_size = max_workers * 2
            for i in range(0, len(remaining_chunks), batch_size):
                batch = remaining_chunks[i:i + batch_size]
                futures = [executor.submit(download_range, final_url, s, e) for s, e in batch]
                for fut in futures:
                    chunk_data = fut.result()
                    out_f.write(chunk_data)
                    out_f.flush()
                    downloaded += len(chunk_data)
                
                elapsed = time.time() - start_time
                pct = (downloaded / total_size) * 100
                speed = (downloaded - current_size) / (elapsed + 0.001) / 1024 / 1024
                print(f"Progress: {downloaded / 1024 / 1024:.1f} / {total_size / 1024 / 1024:.1f} MB ({pct:.1f}%) @ {speed:.2f} MB/s")

    print(f"Download complete: {dest_path}")

if __name__ == "__main__":
    files = [
        ("config.json", "https://huggingface.co/Systran/faster-whisper-small.en/raw/main/config.json"),
        ("tokenizer.json", "https://huggingface.co/Systran/faster-whisper-small.en/raw/main/tokenizer.json"),
        ("vocabulary.txt", "https://huggingface.co/Systran/faster-whisper-small.en/raw/main/vocabulary.txt"),
        ("model.bin", "https://huggingface.co/Systran/faster-whisper-small.en/resolve/main/model.bin"),
    ]
    for name, u in files:
        p = os.path.join(MODEL_DIR, name)
        download_file_segmented(u, p, chunk_size=2 * 1024 * 1024, max_workers=3)
    print("ALL MODEL FILES READY!")
