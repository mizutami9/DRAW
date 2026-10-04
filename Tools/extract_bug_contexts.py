import json
import sys

sys.stdout.reconfigure(encoding='utf-8')

KLANA_JSONL = r"D:\04_tiktok_game\NICODROW\output_klana\segments_ja.jsonl"
segments = [json.loads(line) for line in open(KLANA_JSONL, encoding='utf-8') if line.strip()]

def get_context(target_id, window=3):
    idx = None
    for i, s in enumerate(segments):
        if s["id"] == target_id:
            idx = i
            break
    if idx is None:
        return []
    start_i = max(0, idx - window)
    end_i = min(len(segments), idx + window + 1)
    return segments[start_i:end_i]

# 対象シーンのセグメントIDを探す
target_keywords = [
    ("dead people / ghost stuck", ["dead people", "ghosts", "wherever you guys die"]),
    ("infinite respawn / falling", ["permanently respawning", "hitbox isn't loading"]),
    ("detached head / body gap", ["detached from my body", "gap between my legs"]),
    ("random species change", ["Why am I a turtle", "why am I a bird", "waggly looking bird"]),
    ("ball stuck in air", ["ball is stuck", "stuck in the air"]),
    ("arm hitbox ignored", ["arm hitbox counts", "torso hitbox"]),
    ("presets disappeared", ["presets go away", "What happened to it"]),
    ("glitching into wall / sideways", ["glitching into the wall", "sideways", "still broken"])
]

results = {}
for category, kws in target_keywords:
    results[category] = []
    for s in segments:
        text = s["text"].lower()
        if any(kw.lower() in text for kw in kws):
            ctx = get_context(s["id"], window=2)
            results[category].append({
                "match_id": s["id"],
                "start": s["start"],
                "context": [{"start": c["start"], "text": c["text"], "ja": c["ja_text"]} for c in ctx]
            })

def fmt_ts(sec):
    m = int(sec) // 60
    s = int(sec) % 60
    return f"{m:02d}:{s:02d}"

for cat, occurrences in results.items():
    print(f"\n==========================================")
    print(f"CATEGORY: {cat} (Found {len(occurrences)} occurrences)")
    print(f"==========================================")
    for occ in occurrences[:2]:  # 代表例
        print(f"--- Time: {fmt_ts(occ['start'])} ---")
        for line in occ["context"]:
            print(f"  [{fmt_ts(line['start'])}] {line['text']}")
            print(f"         -> {line['ja']}")
