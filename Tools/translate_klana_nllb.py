import os
import sys
import json
import time
import sentencepiece as spm
import ctranslate2

sys.stdout.reconfigure(encoding='utf-8', line_buffering=True)

MODEL_DIR = r"D:\04_tiktok_game\NICODROW\models\nllb-200-distilled-600M-ct2-int8"
INPUT_JSONL = r"D:\04_tiktok_game\NICODROW\output_klana\segments.jsonl"
OUTPUT_DIR = r"D:\04_tiktok_game\NICODROW\output_klana"

OUTPUT_JSONL = os.path.join(OUTPUT_DIR, "segments_ja.jsonl")
OUTPUT_SRT_ORIG = os.path.join(OUTPUT_DIR, "japanese_subtitles.srt")
OUTPUT_SRT_OSHEN = os.path.join(OUTPUT_DIR, "japanese_subtitles_synced_to_oshen.srt")
OUTPUT_SRT_GAME = os.path.join(OUTPUT_DIR, "japanese_subtitles_game_start.srt")
OUTPUT_TXT = os.path.join(OUTPUT_DIR, "japanese_transcript.txt")

# オフセット定数
OSHEN_OFFSET = 23.228  # Klana時間に加算するとOshen時間になる
KLANA_GAME_START = 429.77  # 00:07:09.77

def format_timestamp(seconds):
    if seconds < 0:
        seconds = 0
    millis = int((seconds - int(seconds)) * 1000)
    secs = int(seconds) % 60
    mins = (int(seconds) // 60) % 60
    hours = int(seconds) // 3600
    return f"{hours:02d}:{mins:02d}:{secs:02d},{millis:03d}"

def clean_input(text):
    text = text.replace("Nico Draw", "NICO DRAW")
    text = text.replace("NicoDraw", "NICO DRAW")
    text = text.replace("nicodraw", "NICO DRAW")
    return text.strip()

def post_process_ja(text):
    text = text.replace("ニコ・ドロー", "NICO DRAW")
    text = text.replace("ニコドロー", "NICO DRAW")
    text = text.replace("ニコードロー", "NICO DRAW")
    text = text.replace("ニコ ドロー", "NICO DRAW")
    return text.strip()

def main():
    if not os.path.exists(INPUT_JSONL):
        print(f"Error: {INPUT_JSONL} not found.")
        return

    print("Loading SentencePiece tokenizer and CTranslate2 model...")
    sp_model = os.path.join(MODEL_DIR, "sentencepiece.bpe.model")
    sp = spm.SentencePieceProcessor()
    sp.load(sp_model)

    translator = ctranslate2.Translator(MODEL_DIR, device="cpu", compute_type="int8")

    print(f"Reading input segments from {INPUT_JSONL}...")
    segments = []
    with open(INPUT_JSONL, "r", encoding="utf-8") as f:
        for line in f:
            if line.strip():
                segments.append(json.loads(line))

    total = len(segments)
    print(f"Total segments: {total}")

    batch_size = 32
    results = []
    t0 = time.time()

    for i in range(0, total, batch_size):
        batch = segments[i : i + batch_size]
        texts = [clean_input(s["text"]) for s in batch]

        tokenized_batch = []
        for t in texts:
            pieces = sp.encode_as_pieces(t)
            tokenized_batch.append(["eng_Latn"] + pieces + ["</s>"])

        translations = translator.translate_batch(
            tokenized_batch,
            target_prefix=[["jpn_Jpan"]] * len(batch),
            beam_size=2,
            max_batch_size=32
        )

        for s, trans in zip(batch, translations):
            hyp = trans.hypotheses[0]
            if hyp and hyp[0] == "jpn_Jpan":
                hyp = hyp[1:]
            if hyp and hyp[-1] == "</s>":
                hyp = hyp[:-1]
            ja_text = post_process_ja(sp.decode_pieces(hyp))
            s_copy = dict(s)
            s_copy["ja_text"] = ja_text
            results.append(s_copy)

        done = len(results)
        pct = (done / total) * 100
        speed = done / (time.time() - t0 + 0.001)
        if i % (batch_size * 5) == 0 or done == total:
            print(f"Translated {done}/{total} ({pct:.1f}%) | {speed:.1f} seg/s")

    print(f"Saving translated JSONL to {OUTPUT_JSONL}...")
    with open(OUTPUT_JSONL, "w", encoding="utf-8") as f:
        for r in results:
            f.write(json.dumps(r, ensure_ascii=False) + "\n")

    print(f"Writing full Japanese transcript to {OUTPUT_TXT}...")
    with open(OUTPUT_TXT, "w", encoding="utf-8") as f:
        for r in results:
            t_str = format_timestamp(r["start"])
            f.write(f"[{t_str}] {r['ja_text']}\n")

    # 1. Klana元動画用SRT
    print(f"Writing original Klana SRT to {OUTPUT_SRT_ORIG}...")
    with open(OUTPUT_SRT_ORIG, "w", encoding="utf-8") as f:
        for idx, r in enumerate(results, 1):
            s_ts = format_timestamp(r["start"])
            e_ts = format_timestamp(r["end"])
            f.write(f"{idx}\n{s_ts} --> {e_ts}\n{r['ja_text']}\n\n")

    # 2. Oshen動画同期用SRT (+23.228s)
    print(f"Writing Oshen-synced SRT to {OUTPUT_SRT_OSHEN}...")
    with open(OUTPUT_SRT_OSHEN, "w", encoding="utf-8") as f:
        for idx, r in enumerate(results, 1):
            s_ts = format_timestamp(r["start"] + OSHEN_OFFSET)
            e_ts = format_timestamp(r["end"] + OSHEN_OFFSET)
            f.write(f"{idx}\n{s_ts} --> {e_ts}\n{r['ja_text']}\n\n")

    # 3. ゲーム開始頭出し用SRT (ゲーム開始07:09.77を0秒とする)
    print(f"Writing game-start cut SRT to {OUTPUT_SRT_GAME}...")
    with open(OUTPUT_SRT_GAME, "w", encoding="utf-8") as f:
        idx_count = 1
        for r in results:
            if r["end"] <= KLANA_GAME_START:
                continue
            s_time = max(0.0, r["start"] - KLANA_GAME_START)
            e_time = max(0.0, r["end"] - KLANA_GAME_START)
            s_ts = format_timestamp(s_time)
            e_ts = format_timestamp(e_time)
            f.write(f"{idx_count}\n{s_ts} --> {e_ts}\n{r['ja_text']}\n\n")
            idx_count += 1

    print("ALL SUBTITLES AND TRANSLATIONS SUCCESSFULLY CREATED!")

if __name__ == "__main__":
    main()
