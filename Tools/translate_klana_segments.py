import json
import os
import sys
import time
from deep_translator import GoogleTranslator

sys.stdout.reconfigure(line_buffering=True, encoding='utf-8')

INPUT_JSONL = r"D:\04_tiktok_game\NICODROW\output_klana\segments.jsonl"
OUTPUT_JSONL = r"D:\04_tiktok_game\NICODROW\output_klana\segments_ja.jsonl"
OUTPUT_SRT = r"D:\04_tiktok_game\NICODROW\output_klana\japanese_subtitles.srt"
OUTPUT_TXT = r"D:\04_tiktok_game\NICODROW\output_klana\japanese_transcript.txt"

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

def main():
    if not os.path.exists(INPUT_JSONL):
        print(f"Error: {INPUT_JSONL} not found yet.")
        return

    print(f"Loading segments from {INPUT_JSONL}...")
    segments = []
    with open(INPUT_JSONL, "r", encoding="utf-8") as f:
        for line in f:
            if line.strip():
                segments.append(json.loads(line))
                
    total = len(segments)
    print(f"Total segments to translate: {total}")
    
    translated = {}
    if os.path.exists(OUTPUT_JSONL):
        with open(OUTPUT_JSONL, "r", encoding="utf-8") as f:
            for line in f:
                if line.strip():
                    item = json.loads(line)
                    translated[item["id"]] = item
        print(f"Resuming with {len(translated)} already translated segments...")

    translator = GoogleTranslator(source='en', target='ja')
    batch_size = 25
    t0 = time.time()
    
    with open(OUTPUT_JSONL, "a", encoding="utf-8") as f_out:
        for i in range(0, total, batch_size):
            batch = segments[i : i + batch_size]
            pending = [s for s in batch if s["id"] not in translated]
            
            if not pending:
                continue
                
            raw_texts = [clean_and_protect(s["text"]) for s in pending]
            delim = " ||| "
            combined = delim.join(raw_texts)
            
            try:
                ja_combined = translator.translate(combined)
                ja_texts = ja_combined.split(delim)
                if len(ja_texts) != len(pending):
                    ja_texts = [translator.translate(t) for t in raw_texts]
            except Exception as e:
                print(f"Batch error at {i}: {e}. Retrying individually...")
                time.sleep(1)
                ja_texts = []
                for t in raw_texts:
                    try:
                        ja_texts.append(translator.translate(t))
                    except Exception:
                        ja_texts.append("[翻訳エラー]")
            
            for s, ja in zip(pending, ja_texts):
                ja_clean = post_process_ja(ja.strip())
                s_copy = dict(s)
                s_copy["ja_text"] = ja_clean
                f_out.write(json.dumps(s_copy, ensure_ascii=False) + "\n")
                f_out.flush()
                translated[s["id"]] = s_copy
                
            done = len(translated)
            pct = (done / total) * 100
            rate = done / (time.time() - t0 + 0.001)
            print(f"Translated {done}/{total} ({pct:.1f}%) | {rate:.1f} seg/s")
            time.sleep(0.2)
            
    print("\nWriting final SRT and TXT files...")
    all_translated = [translated[s["id"]] for s in segments if s["id"] in translated]
    all_translated.sort(key=lambda x: x["start"])
    
    with open(OUTPUT_SRT, "w", encoding="utf-8") as f_srt, \
         open(OUTPUT_TXT, "w", encoding="utf-8") as f_txt:
        for idx, item in enumerate(all_translated, 1):
            f_srt.write(f"{idx}\n")
            f_srt.write(f"{format_timestamp(item['start'])} --> {format_timestamp(item['end'])}\n")
            f_srt.write(f"{item['ja_text']}\n\n")
            
            h = int(item['start']) // 3600
            m = (int(item['start']) // 60) % 60
            s = int(item['start']) % 60
            f_txt.write(f"[{h:02d}:{m:02d}:{s:02d}] {item['ja_text']}\n")
            
    print(f"Translation complete! Outputs saved to {OUTPUT_DIR}")

if __name__ == "__main__":
    main()
