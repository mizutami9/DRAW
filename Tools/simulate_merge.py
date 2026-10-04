import json

with open(r'D:\04_tiktok_game\NICODROW\output\segments_ja.jsonl', 'r', encoding='utf-8') as f:
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
        
        # ギャップが0.8秒以下かつ合計35文字以下の場合は結合
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

print(f"Original segments: {len(segs)}")
print(f"Merged segments: {len(merged)}")
total_chars = sum(len(m['text_ja']) for m in merged)
print(f"Total chars: {total_chars}")
for i in range(10):
    m = merged[i]
    print(f"[{m['start']:.1f}s - {m['end']:.1f}s] ({m['end']-m['start']:.1f}s) {m['text_ja']}")
