import os
import sys
import json
import time

sys.stdout.reconfigure(line_buffering=True)

AUDIO_PATH = r"D:\04_tiktok_game\NICODROW\work\audio_16k.wav"
MODEL_PATH = r"D:\04_tiktok_game\NICODROW\work\models\small.en"
OUTPUT_DIR = r"D:\04_tiktok_game\NICODROW\output"
os.makedirs(OUTPUT_DIR, exist_ok=True)

JSONL_OUT = os.path.join(OUTPUT_DIR, "segments.jsonl")
SRT_EN_OUT = os.path.join(OUTPUT_DIR, "english_subtitles.srt")
TXT_EN_OUT = os.path.join(OUTPUT_DIR, "english_transcript.txt")

def format_timestamp(seconds):
    millis = int((seconds - int(seconds)) * 1000)
    secs = int(seconds) % 60
    mins = (int(seconds) // 60) % 60
    hours = int(seconds) // 3600
    return f"{hours:02d}:{mins:02d}:{secs:02d},{millis:03d}"

def main():
    import faster_whisper
    
    print("Loading faster-whisper model on CPU (int8)...")
    model = faster_whisper.WhisperModel(MODEL_PATH, device="cpu", compute_type="int8", cpu_threads=8)
    print("Model loaded on CPU successfully!")

    print(f"Starting transcription of {AUDIO_PATH}...")
    start_time = time.time()
    
    # vad_filter=True で発話区間のみ高精度・高速に文字起こし
    segments_gen, info = model.transcribe(
        AUDIO_PATH,
        beam_size=5,
        word_timestamps=True,
        vad_filter=True,
        vad_parameters=dict(min_silence_duration_ms=500),
        language="en"
    )
    
    total_duration = info.duration
    print(f"Audio duration: {total_duration:.1f}s ({total_duration/60:.1f} minutes)")
    
    seg_idx = 0
    with open(JSONL_OUT, "w", encoding="utf-8") as f_jsonl, \
         open(SRT_EN_OUT, "w", encoding="utf-8") as f_srt, \
         open(TXT_EN_OUT, "w", encoding="utf-8") as f_txt:
         
        for segment in segments_gen:
            seg_idx += 1
            text = segment.text.strip()
            
            # JSONL 保存
            words_data = []
            if segment.words:
                for w in segment.words:
                    words_data.append({"word": w.word, "start": w.start, "end": w.end, "probability": w.probability})
            
            data = {
                "id": seg_idx,
                "start": segment.start,
                "end": segment.end,
                "text": text,
                "words": words_data
            }
            f_jsonl.write(json.dumps(data, ensure_ascii=False) + "\n")
            f_jsonl.flush()
            
            # SRT 保存
            f_srt.write(f"{seg_idx}\n")
            f_srt.write(f"{format_timestamp(segment.start)} --> {format_timestamp(segment.end)}\n")
            f_srt.write(f"{text}\n\n")
            f_srt.flush()
            
            # TXT 保存
            h = int(segment.start) // 3600
            m = (int(segment.start) // 60) % 60
            s = int(segment.start) % 60
            f_txt.write(f"[{h:02d}:{m:02d}:{s:02d}] {text}\n")
            f_txt.flush()
            
            if seg_idx % 20 == 0:
                elapsed = time.time() - start_time
                pct = (segment.end / total_duration) * 100
                speed = segment.end / (elapsed + 0.001)
                print(f"Seg {seg_idx:4d} | At {format_timestamp(segment.end)} ({pct:5.1f}%) | Speed: {speed:4.1f}x real-time")
                
    total_elapsed = time.time() - start_time
    print(f"\nTranscription completed! Total segments: {seg_idx} in {total_elapsed:.1f}s ({total_elapsed/60:.1f}m)")
    print(f"Outputs:\n - {JSONL_OUT}\n - {SRT_EN_OUT}\n - {TXT_EN_OUT}")

if __name__ == "__main__":
    main()
