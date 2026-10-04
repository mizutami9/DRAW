import os
import sys
import json
import time
import math
import urllib.request
import urllib.parse
from concurrent.futures import ThreadPoolExecutor

CACHE_DIR = r"D:\04_tiktok_game\NICODROW\work\tts_cache"
os.makedirs(CACHE_DIR, exist_ok=True)

INPUT_JSONL = r"D:\04_tiktok_game\NICODROW\output\segments_ja.jsonl"
MERGED_JSON = r"D:\04_tiktok_game\NICODROW\work\merged_segments.json"

# 1. セグメントの結合と話者割り当て
def prepare_segments():
    with open(INPUT_JSONL, 'r', encoding='utf-8') as f:
        segs = [json.loads(line) for line in f]

    merged = []
    current = None

    for s in segs:
        text = s.get('ja_text', '').strip()
        if not text or text == '[聞き取り不明]':
            continue
        
        if current is None:
            current = {
                'id': s['id'],
                'start': s['start'],
                'end': s['end'],
                'text_en': s['text'].strip(),
                'text_ja': text
            }
        else:
            gap = s['start'] - current['end']
            combined_len = len(current['text_ja']) + len(text)
            
            if gap <= 0.8 and combined_len <= 35 and (s['end'] - current['start']) <= 7.0:
                current['end'] = s['end']
                current['text_en'] += " " + s['text'].strip()
                current['text_ja'] += "、" + text if not current['text_ja'].endswith(('。', '！', '？', '…')) else text
            else:
                merged.append(current)
                current = {
                    'id': s['id'],
                    'start': s['start'],
                    'end': s['end'],
                    'text_en': s['text'].strip(),
                    'text_ja': text
                }

    if current:
        merged.append(current)

    # 話者割り当て
    # 8: 春日部つむぎ (Oshen - メイン配信者, 元気な女性)
    # 2: 四国めたん (Klana / Maya - 落ち着いた女性)
    # 13: 青山龍星 (Bup - 低音男性)
    for idx, m in enumerate(merged):
        m['idx'] = idx
        en_lower = m['text_en'].lower()
        ja = m['text_ja']
        
        # Bup / 男性判定
        if any(k in en_lower for k in ['bup', 'bro', 'dude', 'man', 'he said', 'his ']) or any(k in ja for k in ['野郎', '男', 'やつ']):
            m['speaker'] = 13  # 青山龍星
        # 別女性判定 (Klana / Maya / 会話の掛け合い)
        elif any(k in en_lower for k in ['klana', 'kiana', 'maya', 'nova']):
            m['speaker'] = 2   # 四国めたん
        elif idx % 5 == 3:  # 自然なバリエーションとして時々相槌を別の声に
            m['speaker'] = 2
        else:
            m['speaker'] = 8   # 春日部つむぎ

    with open(MERGED_JSON, 'w', encoding='utf-8') as f:
        json.dump(merged, f, ensure_ascii=False, indent=2)

    print(f"Prepared {len(merged)} segments. Saved to {MERGED_JSON}")
    return merged

if __name__ == "__main__":
    prepare_segments()
