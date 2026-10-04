import os
import sys
import json
import time
import math
import urllib.request
import urllib.parse
from concurrent.futures import ThreadPoolExecutor, as_completed

CACHE_DIR = r"D:\04_tiktok_game\NICODROW\work\tts_cache"
os.makedirs(CACHE_DIR, exist_ok=True)

MERGED_JSON = r"D:\04_tiktok_game\NICODROW\work\merged_segments.json"

def synthesize_segment(seg):
    idx = seg['idx']
    speaker = seg.get('speaker', 8)
    text = seg['text_ja']
    out_wav = os.path.join(CACHE_DIR, f"seg_{idx:04d}.wav")
    
    # 既に生成済みならスキップ
    if os.path.exists(out_wav) and os.path.getsize(out_wav) > 100:
        return idx, True, "cached"

    # 時間計算と速度調整
    duration = max(seg['end'] - seg['start'], 1.2)
    chars = len(text)
    cps = chars / duration
    speed = 1.15
    if cps > 5.0:
        speed = min(1.45, 1.15 * (cps / 5.0))
    elif cps < 2.5:
        speed = 1.05

    # リトライループ
    for retry in range(4):
        try:
            # 1. audio_query
            q_url = f"http://127.0.0.1:50021/audio_query?speaker={speaker}&text=" + urllib.parse.quote(text)
            req = urllib.request.Request(q_url, method="POST")
            with urllib.request.urlopen(req, timeout=15) as resp:
                query_data = json.loads(resp.read())
            
            query_data["speedScale"] = round(speed, 2)
            
            # 2. synthesis
            s_url = f"http://127.0.0.1:50021/synthesis?speaker={speaker}"
            data_bytes = json.dumps(query_data).encode('utf-8')
            req2 = urllib.request.Request(s_url, data=data_bytes, headers={"Content-Type": "application/json"}, method="POST")
            with urllib.request.urlopen(req2, timeout=20) as resp2:
                wav_bytes = resp2.read()

            with open(out_wav, "wb") as f:
                f.write(wav_bytes)
            
            return idx, True, "done"
        except Exception as e:
            time.sleep(1.0 + retry * 1.5)
            if retry == 3:
                return idx, False, str(e)

def run_tts_batch():
    with open(MERGED_JSON, 'r', encoding='utf-8') as f:
        segments = json.load(f)

    total = len(segments)
    print(f"Starting TTS batch for {total} segments with 4 workers...")
    
    # キャッシュチェック
    existing = 0
    to_process = []
    for s in segments:
        wav_path = os.path.join(CACHE_DIR, f"seg_{s['idx']:04d}.wav")
        if os.path.exists(wav_path) and os.path.getsize(wav_path) > 100:
            existing += 1
        else:
            to_process.append(s)

    print(f"Already cached: {existing}/{total}. Remaining to synthesize: {len(to_process)}")

    if not to_process:
        print("All segments already synthesized!")
        return

    t0 = time.time()
    completed = 0
    errors = 0

    with ThreadPoolExecutor(max_workers=4) as executor:
        futures = {executor.submit(synthesize_segment, s): s for s in to_process}
        for fut in as_completed(futures):
            idx, success, msg = fut.result()
            if success:
                completed += 1
            else:
                errors += 1
                print(f"[ERROR] Seg {idx}: {msg}")

            if (completed + errors) % 50 == 0 or (completed + errors) == len(to_process):
                elapsed = time.time() - t0
                pct = (completed + errors) / len(to_process) * 100
                sec_per_item = elapsed / (completed + errors) if (completed + errors) > 0 else 0
                eta_min = (len(to_process) - (completed + errors)) * sec_per_item / 60
                print(f"Progress: {completed + errors}/{len(to_process)} ({pct:.1f}%) | "
                      f"Elapsed: {elapsed/60:.1f}m | ETA: {eta_min:.1f}m | Speed: {sec_per_item:.2f}s/seg")

    print(f"\nTTS batch finished in {(time.time() - t0)/60:.1f} minutes! (Errors: {errors})")

if __name__ == "__main__":
    run_tts_batch()
