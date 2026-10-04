import json
import re
import os
import sys

sys.stdout.reconfigure(encoding='utf-8')

INPUT_JSONL = r"D:\04_tiktok_game\NICODROW\output\segments_ja.jsonl"
OUT_ASS = r"D:\04_tiktok_game\NICODROW\output\twitch_fixed_subtitled.ass"
OUT_SRT = r"D:\04_tiktok_game\NICODROW\output\twitch_fixed_subtitled.srt"

def format_timestamp_ass(seconds):
    h = int(seconds // 3600)
    m = int((seconds % 3600) // 60)
    s = int(seconds % 60)
    cs = int(round((seconds - int(seconds)) * 100))
    if cs >= 100:
        cs = 99
    return f"{h:01d}:{m:02d}:{s:02d}.{cs:02d}"

def format_timestamp_srt(seconds):
    h = int(seconds // 3600)
    m = int((seconds % 3600) // 60)
    s = int(seconds % 60)
    ms = int(round((seconds - int(seconds)) * 1000))
    if ms >= 1000:
        ms = 999
    return f"{h:02d}:{m:02d}:{s:02d},{ms:03d}"

def build_subtitles():
    with open(INPUT_JSONL, 'r', encoding='utf-8') as f:
        raw_segs = [json.loads(line) for line in f]

    print(f"Loaded {len(raw_segs)} raw segments.")

    # 1. 結合処理（自然な字幕表示長にする）
    merged = []
    curr = None

    for s in raw_segs:
        ja = s.get('ja_text', '').strip()
        en = s.get('text', '').strip()
        if not ja or ja == '[聞き取り不明]':
            continue

        if curr is None:
            curr = {
                'start': s['start'],
                'end': s['end'],
                'en': en,
                'ja': ja
            }
        else:
            gap = s['start'] - curr['end']
            combined_len = len(curr['ja']) + len(ja)
            duration = s['end'] - curr['start']

            # 直前の文が句点・感嘆符・疑問符で終わっていなければマージ
            ends_sentence = curr['ja'].endswith(('。', '！', '？', '…', '!', '?'))
            if gap <= 0.6 and combined_len <= 30 and duration <= 6.0 and not ends_sentence:
                curr['end'] = s['end']
                curr['en'] += " " + en
                curr['ja'] += " " + ja
            else:
                merged.append(curr)
                curr = {
                    'start': s['start'],
                    'end': s['end'],
                    'en': en,
                    'ja': ja
                }

    if curr:
        merged.append(curr)

    print(f"Merged into {len(merged)} subtitle cues.")

    # 2. 4人の話者判定
    # 0: Oshen (ピンク)
    # 1: Klana (シアン)
    # 2: Bup (グリーン)
    # 3: Maya (イエロー)

    last_speaker = 0
    speaker_counts = {0: 0, 1: 0, 2: 0, 3: 0}

    for idx, cue in enumerate(merged):
        en_lower = cue['en'].lower()
        ja_text = cue['ja']

        # ルールベース判定
        speaker = None

        # Bup判定 (男性スラング、男性指示詞)
        if any(w in en_lower for w in ['bro', 'dude', 'man', 'he said', 'his', 'guy', 'sir', 'son']) or any(w in ja_text for w in ['野郎', '彼', '男', '兄貴']):
            speaker = 2 # Bup
        # Klana判定
        elif any(w in en_lower for w in ['klana', 'kiana']):
            # 呼びかけの場合、次がKlanaになりやすい
            speaker = 1 # Klana
        # Maya判定
        elif any(w in en_lower for w in ['maya', 'nova']):
            speaker = 3 # Maya
        # Oshen判定 (配信主特有の語彙)
        elif any(w in en_lower for w in ['chat', 'stream', 'twitch', 'host', 'i\'m host', 'my game', 'my stream', 'subscribe', 'preset']):
            speaker = 0 # Oshen
        else:
            # 会話の掛け合いシミュレーション
            gap_from_last = cue['start'] - merged[idx-1]['end'] if idx > 0 else 999
            if gap_from_last < 0.8:
                # 短い間隔での相槌や掛け合い
                if last_speaker == 0:
                    # Oshenの後は他メンバーが反応しやすい
                    speaker = (idx % 3) + 1  # 1, 2, or 3
                else:
                    # 他メンバーの後はOshen(ホスト)が拾いやすい
                    speaker = 0
            else:
                # 間隔が空いた場合はメイン配信者のOshenが発言しやすい
                speaker = 0 if (idx % 5 != 3) else 1

        cue['speaker'] = speaker
        last_speaker = speaker
        speaker_counts[speaker] += 1

    print("Speaker distribution:", speaker_counts)

    # 3. ASSファイル生成
    # 色設定: BGR形式 (&H00BBGGRR)
    # Oshen: #FF4F81 -> &H814FFF
    # Klana: #00D2FF -> &HFFD200
    # Bup:   #44D62C -> &H2CD644
    # Maya:  #FFB300 -> &H00B3FF
    ass_header = """[Script Info]
Title: NICO DRAW Twitch Stream (4 Players Colored)
ScriptType: v4.00+
WrapStyle: 0
ScaledBorderAndShadow: yes
YCbCr Matrix: TV.709
PlayResX: 1920
PlayResY: 1080

[V4+ Styles]
Format: Name, Fontname, Fontsize, PrimaryColour, SecondaryColour, OutlineColour, BackColour, Bold, Italic, Underline, StrikeOut, ScaleX, ScaleY, Spacing, Angle, BorderStyle, Outline, Shadow, Alignment, MarginL, MarginR, MarginV, Encoding
Style: Oshen,Meiryo,54,&H00814FFF,&H000000FF,&H00000000,&H80000000,-1,0,0,0,100,100,0,0,1,3.5,1.5,2,40,40,45,1
Style: Klana,Meiryo,54,&H00FFD200,&H000000FF,&H00000000,&H80000000,-1,0,0,0,100,100,0,0,1,3.5,1.5,2,40,40,45,1
Style: Bup,Meiryo,54,&H002CD644,&H000000FF,&H00000000,&H80000000,-1,0,0,0,100,100,0,0,1,3.5,1.5,2,40,40,45,1
Style: Maya,Meiryo,54,&H0000B3FF,&H000000FF,&H00000000,&H80000000,-1,0,0,0,100,100,0,0,1,3.5,1.5,2,40,40,45,1

[Events]
Format: Layer, Start, End, Style, Name, MarginL, MarginR, MarginV, Effect, Text
"""

    speaker_styles = {
        0: ("Oshen", "Oshen"),
        1: ("Klana", "Klana"),
        2: ("Bup", "Bup"),
        3: ("Maya", "Maya")
    }

    ass_events = []
    for cue in merged:
        style_name, spk_label = speaker_styles[cue['speaker']]
        start_t = format_timestamp_ass(cue['start'])
        end_t = format_timestamp_ass(cue['end'])
        
        # 1行が22文字を超える場合は適切な位置で改行
        ja = cue['ja'].strip()
        if len(ja) > 22 and "、" in ja:
            parts = ja.split("、", 1)
            ja_fmt = parts[0] + "、\\N" + parts[1]
        elif len(ja) > 24:
            mid = len(ja) // 2
            ja_fmt = ja[:mid] + "\\N" + ja[mid:]
        else:
            ja_fmt = ja

        text_ass = f"【{spk_label}】 {ja_fmt}"
        ass_events.append(f"Dialogue: 0,{start_t},{end_t},{style_name},{spk_label},0,0,0,,{text_ass}")

    with open(OUT_ASS, 'w', encoding='utf-8') as f:
        f.write(ass_header + "\n".join(ass_events) + "\n")
    print(f"Saved ASS subtitles to: {OUT_ASS}")

    # 4. カラー対応SRTファイル生成
    # HTML <font color="..."> 形式
    speaker_colors = {
        0: ("#FF4F81", "Oshen"),
        1: ("#00D2FF", "Klana"),
        2: ("#44D62C", "Bup"),
        3: ("#FFB300", "Maya")
    }

    srt_entries = []
    for idx, cue in enumerate(merged, start=1):
        color_code, spk_label = speaker_colors[cue['speaker']]
        start_t = format_timestamp_srt(cue['start'])
        end_t = format_timestamp_srt(cue['end'])
        
        ja = cue['ja'].strip()
        if len(ja) > 22 and "、" in ja:
            parts = ja.split("、", 1)
            ja_fmt = parts[0] + "、\n" + parts[1]
        elif len(ja) > 24:
            mid = len(ja) // 2
            ja_fmt = ja[:mid] + "\n" + ja[mid:]
        else:
            ja_fmt = ja

        srt_text = f"<font color=\"{color_code}\"><b>【{spk_label}】</b> {ja_fmt}</font>"
        srt_entries.append(f"{idx}\n{start_t} --> {end_t}\n{srt_text}\n")

    with open(OUT_SRT, 'w', encoding='utf-8') as f:
        f.write("\n".join(srt_entries))
    print(f"Saved SRT subtitles to: {OUT_SRT}")

if __name__ == "__main__":
    build_subtitles()
