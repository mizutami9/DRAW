import json
import sys

sys.stdout.reconfigure(encoding='utf-8')

with open(r'D:\04_tiktok_game\NICODROW\output\segments_ja.jsonl', 'r', encoding='utf-8') as f:
    segs = [json.loads(line) for line in f]

# 候補となるタイムレンジを精査
candidate_ranges = [
    ("初お絵かき・飛行機とお尻", 480, 560),          # 08:00 - 09:20
    ("友達持ち上げ投げ・短腕トス", 530, 600),          # 08:50 - 10:00
    ("車輪だらけの怪異", 760, 830),                  # 12:40 - 13:50
    ("鍵投げと計量器ギミックのドタバタ", 1080, 1320),  # 18:00 - 22:00
    ("パックマンを描いたはずが...", 2060, 2150),     # 34:20 - 35:50
    ("太すぎて挟まる・巨大腕侵入", 2850, 3250),       # 47:30 - 54:10
    ("大玉転がしパニック", 3360, 3680),              # 56:00 - 61:20
    ("パックマン再び・描画拒否？", 4920, 5040),        # 82:00 - 84:00
    ("耳テロ大爆音・リスポーン連続死", 5570, 5620),    # 92:50 - 93:40
]

for title, start_s, end_s in candidate_ranges:
    print(f"=== {title} ({start_s//60:02d}:{start_s%60:02d} - {end_s//60:02d}:{end_s%60:02d}) ===")
    sub_segs = [s for s in segs if start_s <= s['start'] <= end_s]
    for s in sub_segs[:8]:
        st = f"{int(s['start']//60):02d}:{int(s['start']%60):02d}"
        print(f"  [{st}] {s['text'][:40]} -> {s.get('ja_text', '')[:40]}")
    if len(sub_segs) > 8:
        print(f"  ... ({len(sub_segs)} lines total)")
    print()
