import json
import sys

sys.stdout.reconfigure(encoding='utf-8')

with open(r'D:\04_tiktok_game\NICODROW\output\segments_ja.jsonl', 'r', encoding='utf-8') as f:
    segs = [json.loads(line) for line in f]

# 最初の100セグメントの英語と日本語を見てみる
for s in segs[50:90]:
    t = f"[{s['start']:06.1f} - {s['end']:06.1f}]"
    print(f"{t} EN: {s['text'][:50]} | JA: {s.get('ja_text', '')[:50]}")
