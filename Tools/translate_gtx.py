import json
import os
import sys
import time
import urllib.parse
import urllib.request

sys.stdout.reconfigure(line_buffering=True)

INPUT_JSONL = r"D:\04_tiktok_game\NICODROW\output\segments.jsonl"
OUTPUT_JSONL = r"D:\04_tiktok_game\NICODROW\output\segments_ja.jsonl"
OUTPUT_SRT = r"D:\04_tiktok_game\NICODROW\output\japanese_subtitles.srt"
OUTPUT_TXT = r"D:\04_tiktok_game\NICODROW\output\japanese_transcript.txt"

def format_timestamp(seconds):
    millis = int((seconds - int(seconds)) * 1000)
    secs = int(seconds) % 60
    mins = (int(seconds) // 60) % 60
    hours = int(seconds) // 3600
    return f"{hours:02d}:{mins:02d}:{secs:02d},{millis:03d}"

def clean_and_protect(text):
    text = text.replace("Nico Draw", "NICO DRAW")
    text = text.replace("NicoDraw", "NICO DRAW")
    text = text.replace("nicodraw", "NICO DRAW")
    return text

def post_process_ja(text):
    text = text.replace("ニコ・ドロー", "NICO DRAW")
    text = text.replace("ニコドロー", "NICO DRAW")
    text = text.replace("ニコードロー", "NICO DRAW")
    text = text.replace("ニコ ドロー", "NICO DRAW")
    return text

def translate_texts(texts):
    if not texts:
        return []
    
    # 複数行を連結
    combined = "\n".join(clean_and_protect(t) for t in texts)
    url = "https://translate.googleapis.com/translate_a/single?client=gtx&sl=en&tl=ja&dt=t&q=" + urllib.parse.quote(combined)
    
    req = urllib.request.Request(url, headers={'User-Agent': 'Mozilla/5.0 (Windows NT 10.0; Win64; x64)'})
    for attempt in range(5):
        try:
            with urllib.request.urlopen(req, timeout=15) as resp:
                data = json.loads(resp.read().decode('utf-8'))
                # data[0] contains translated segments: [[trans, orig], [trans, orig], ...]
                translated_full = "".join(item[0] for item in data[0] if item and item[0])
                lines = translated_full.split("\n")
                if len(lines) == len(texts):
                    return [post_process_ja(l.strip()) for l in lines]
                else:
                    # 行数が合わない場合は個別翻訳
                    break
        except Exception as e:
            time.sleep(1 + attempt)
            
    # フォールバック：1つずつ翻訳
    results = []
    for t in texts:
        q_url = "https://translate.googleapis.com/translate_a/single?client=gtx&sl=en&tl=ja&dt=t&q=" + urllib.parse.quote(clean_and_protect(t))
        q_req = urllib.request.Request(q_url, headers={'User-Agent': 'Mozilla/5.0'})
        try:
            with urllib.request.urlopen(q_req, timeout=10) as resp:
                q_data = json.loads(resp.read().decode('utf-8'))
                q_text = "".join(item[0] for item in q_data[0] if item and item[0])
                results.append(post_process_ja(q_text.strip()))
        except Exception:
            results.append("[聞き取り不明]")
    return results

def main():
    print(f"Loading {INPUT_JSONL}...")
    segments = []
    with open(INPUT_JSONL, "r", encoding="utf-8") as f:
        for line in f:
            if line.strip():
                segments.append(json.loads(line))
                
    total = len(segments)
    print(f"Total segments: {total}")
    
    translated = {}
    if os.path.exists(OUTPUT_JSONL):
        with open(OUTPUT_JSONL, "r", encoding="utf-8") as f:
            for line in f:
                if line.strip():
                    item = json.loads(line)
                    translated[item["id"]] = item
        print(f"Resuming from {len(translated)} already translated segments...")

    batch_size = 40
    start_time = time.time()
    
    with open(OUTPUT_JSONL, "a" if translated else "w", encoding="utf-8") as f_out:
        for i in range(0, total, batch_size):
            batch = segments[i:i + batch_size]
            to_trans = [s for s in batch if s["id"] not in translated]
            
            if to_trans:
                texts = [s["text"] for s in to_trans]
                ja_results = translate_texts(texts)
                for s, ja in zip(to_trans, ja_results):
                    s["ja_text"] = ja
                    f_out.write(json.dumps(s, ensure_ascii=False) + "\n")
                    f_out.flush()
                    translated[s["id"]] = s
                    
                time.sleep(0.3) # サーバー負荷への配慮
                
            curr = len(translated)
            pct = (curr / total) * 100
            elapsed = time.time() - start_time
            print(f"Progress: {curr:4d} / {total} ({pct:5.1f}%) in {elapsed:.1f}s")
            
    print("\nWriting Japanese SRT and TXT...")
    all_items = [translated[s["id"]] for s in segments if s["id"] in translated]
    all_items.sort(key=lambda x: x["id"])
    
    with open(OUTPUT_SRT, "w", encoding="utf-8") as f_srt, \
         open(OUTPUT_TXT, "w", encoding="utf-8") as f_txt:
        for s in all_items:
            sid = s["id"]
            st = s["start"]
            et = s["end"]
            ja = s.get("ja_text", "")
            
            f_srt.write(f"{sid}\n")
            f_srt.write(f"{format_timestamp(st)} --> {format_timestamp(et)}\n")
            f_srt.write(f"{ja}\n\n")
            
            h = int(st) // 3600
            m = (int(st) // 60) % 60
            sec = int(st) % 60
            f_txt.write(f"[{h:02d}:{m:02d}:{sec:02d}] {ja}\n")
            
    print(f"Translation Complete!\nOutputs:\n - {OUTPUT_JSONL}\n - {OUTPUT_SRT}\n - {OUTPUT_TXT}")

if __name__ == "__main__":
    main()
