import json
import sys

with open(r'D:\04_tiktok_game\NICODROW\output\segments_ja.jsonl', 'r', encoding='utf-8') as f:
    segs = [json.loads(line) for line in f]

valid = [s for s in segs if s.get('ja_text', '').strip() and s['ja_text'].strip() not in ('[聞き取り不明]', '...')]
total_chars = sum(len(s['ja_text']) for s in valid)

print(f"Total segments: {len(segs)}")
print(f"Valid segments: {len(valid)}")
print(f"Total Japanese chars: {total_chars}")
print(f"Average chars: {total_chars/len(valid):.1f}")
