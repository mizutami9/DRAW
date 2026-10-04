import json
import os
import sys

sys.stdout.reconfigure(encoding='utf-8')

ASS_FILE = r"D:\04_tiktok_game\NICODROW\output\twitch_fixed_subtitled.ass"
OUT_DIR = r"D:\04_tiktok_game\NICODROW\output\editor_subtitles"
os.makedirs(OUT_DIR, exist_ok=True)

# format seconds to srt timestamp
def sec_to_srt(sec):
    h = int(sec // 3600)
    m = int((sec % 3600) // 60)
    s = int(sec % 60)
    ms = int(round((sec - int(sec)) * 1000))
    if ms >= 1000: ms = 999
    return f"{h:02d}:{m:02d}:{s:02d},{ms:03d}"

def ass_ts_to_sec(ts):
    parts = ts.split(':')
    h = int(parts[0])
    m = int(parts[1])
    s, cs = parts[2].split('.')
    return h * 3600 + m * 60 + int(s) + int(cs) / 100.0

# Read events from ASS
events = []
with open(ASS_FILE, 'r', encoding='utf-8') as f:
    for line in f:
        if line.startswith('Dialogue:'):
            # Format: Layer, Start, End, Style, Name, MarginL, MarginR, MarginV, Effect, Text
            parts = line.strip().split(',', 9)
            start_sec = ass_ts_to_sec(parts[1])
            end_sec = ass_ts_to_sec(parts[2])
            style = parts[3]
            name = parts[4]
            raw_text = parts[9]
            
            # clean text
            clean_text = raw_text.replace('\\N', ' ').replace('\\n', ' ')
            # remove 【Name】 prefix for clean version if needed, or keep it
            events.append({
                'start': start_sec,
                'end': end_sec,
                'speaker': style,
                'raw_text': clean_text
            })

print(f"Loaded {len(events)} events.")

# 1. クリーン統合SRT（タグなし、どの編集ソフトでも文字化け/タグ露出しない）
clean_srt_path = os.path.join(OUT_DIR, "all_subtitles_clean.srt")
with open(clean_srt_path, 'w', encoding='utf-8') as f:
    for i, ev in enumerate(events, 1):
        f.write(f"{i}\n{sec_to_srt(ev['start'])} --> {sec_to_srt(ev['end'])}\n{ev['raw_text']}\n\n")

# 2. 話者ごとの個別SRT（トラック別に読み込んでソフト内で一括色指定できる！）
speakers = ['Oshen', 'Klana', 'Bup', 'Maya']
for spk in speakers:
    spk_events = [ev for ev in events if ev['speaker'] == spk]
    spk_path = os.path.join(OUT_DIR, f"{spk}_subtitles.srt")
    with open(spk_path, 'w', encoding='utf-8') as f:
        for i, ev in enumerate(spk_events, 1):
            # 話者名タグを除いたセリフ本文のみ
            text_body = ev['raw_text']
            if text_body.startswith(f"【{spk}】"):
                text_body = text_body[len(f"【{spk}】"):].strip()
            f.write(f"{i}\n{sec_to_srt(ev['start'])} --> {sec_to_srt(ev['end'])}\n{text_body}\n\n")
    print(f"Saved {len(spk_events)} events for {spk} to {spk_path}")

print(f"All editor subtitles ready in {OUT_DIR}")
