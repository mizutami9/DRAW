import json
import os
import sys
import time
from deep_translator import GoogleTranslator

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
    # 固有名詞の保護
    text = text.replace("Nico Draw", "NICO DRAW")
    text = text.replace("NicoDraw", "NICO DRAW")
    text = text.replace("nicodraw", "NICO DRAW")
    return text

def post_process_ja(text):
    # 翻訳後の微調整
    text = text.replace("ニコ・ドロー", "NICO DRAW")
    text = text.replace("ニコドロー", "NICO DRAW")
    text = text.replace("ニコードロー", "NICO DRAW")
    text = text.replace("ニコ ドロー", "NICO DRAW")
    return text

def main():
    print(f"Loading segments from {INPUT_JSONL}...")
    segments = []
    with open(INPUT_JSONL, "r", encoding="utf-8") as f:
        for line in f:
            if line.strip():
                segments.append(json.loads(line))
                
    total = len(segments)
    print(f"Total segments to translate: {total}")
    
    # 既存の翻訳があればレジューム可能にする
    translated = {}
    if os.path.exists(OUTPUT_JSONL):
        with open(OUTPUT_JSONL, "r", encoding="utf-8") as f:
            for line in f:
                if line.strip():
                    item = json.loads(line)
                    translated[item["id"]] = item
        print(f"Resuming with {len(translated)} already translated segments...")

    translator = GoogleTranslator(source='en', target='ja')
    
    # 25件ずつバッチ翻訳
    batch_size = 25
    start_time = time.time()
    
    with open(OUTPUT_JSONL, "a" if translated else "w", encoding="utf-8") as f_out:
        for i in range(0, total, batch_size):
            batch = segments[i:i + batch_size]
            to_translate = [s for s in batch if s["id"] not in translated]
            
            if to_translate:
                # 連結して1リクエストで翻訳（区切り記号: \n ||| \n）
                separator = "\n===SEG===\n"
                combined_text = separator.join(clean_and_protect(s["text"]) for s in to_translate)
                
                success = False
                for attempt in range(5):
                    try:
                        translated_block = translator.translate(combined_text)
                        parts = translated_block.split("===SEG===")
                        if len(parts) == len(to_translate):
                            for s, ja_text in zip(to_translate, parts):
                                ja = post_process_ja(ja_text.strip())
                                s["ja_text"] = ja
                                f_out.write(json.dumps(s, ensure_ascii=False) + "\n")
                                f_out.flush()
                                translated[s["id"]] = s
                            success = True
                            break
                        else:
                            # 分割数が合わない場合は個別に翻訳
                            print(f"Batch {i//batch_size} split mismatch ({len(parts)} vs {len(to_translate)}), translating individually...")
                            for s in to_translate:
                                ja = translator.translate(clean_and_protect(s["text"]))
                                s["ja_text"] = post_process_ja(ja.strip())
                                f_out.write(json.dumps(s, ensure_ascii=False) + "\n")
                                f_out.flush()
                                translated[s["id"]] = s
                            success = True
                            break
                    except Exception as e:
                        print(f"Translation error at batch {i}: {e}. Retrying ({attempt+1}/5)...")
                        time.sleep(1 + attempt * 2)
                        
                if not success:
                    print(f"Failed to translate batch {i}, falling back to placeholder...")
                    for s in to_translate:
                        s["ja_text"] = "[聞き取り不明/翻訳失敗]"
                        f_out.write(json.dumps(s, ensure_ascii=False) + "\n")
                        f_out.flush()
                        translated[s["id"]] = s

            if (i + batch_size) % 100 < batch_size or i + batch_size >= total:
                curr = len(translated)
                elapsed = time.time() - start_time
                pct = (curr / total) * 100
                print(f"Translated {curr}/{total} ({pct:.1f}%) in {elapsed:.1f}s")
                
    # SRT と TXT を出力
    print("\nGenerating Japanese SRT and TXT...")
    all_translated = [translated[s["id"]] for s in segments if s["id"] in translated]
    all_translated.sort(key=lambda x: x["id"])
    
    with open(OUTPUT_SRT, "w", encoding="utf-8") as f_srt, \
         open(OUTPUT_TXT, "w", encoding="utf-8") as f_txt:
        for s in all_translated:
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
            
    print(f"Done! Japanese outputs:\n - {OUTPUT_JSONL}\n - {OUTPUT_SRT}\n - {OUTPUT_TXT}")

if __name__ == "__main__":
    main()
