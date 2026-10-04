import json
import re
import sys

sys.stdout.reconfigure(encoding='utf-8')

KLANA_JSONL = r"D:\04_tiktok_game\NICODROW\output_klana\segments_ja.jsonl"

BUG_PATTERNS = [
    r"\bbug", r"\bglitch", r"\bstuck", r"\bbreak", r"\bbroke", r"\blag",
    r"\bfreeze", r"\bteleport", r"\bdesync", r"\binvisible", r"\bdisappear",
    r"can('?t| not) move", r"can('?t| not) jump", r"can('?t| not) draw",
    r"can('?t| not) pick", r"can('?t| not) reach", r"fall through", r"\bclipping\b",
    r"\bclipped\b", r"\bfloating\b", r"doesn'?t work", r"not working",
    r"what happened", r"wait what", r"why is", r"why am i", r"where did",
    r"where am i", r"\bdie\b", r"\bdied\b", r"hitbox", r"respawn", r"reset"
]
compiled_patterns = [re.compile(p, re.IGNORECASE) for p in BUG_PATTERNS]

segments = []
with open(KLANA_JSONL, "r", encoding="utf-8") as f:
    for line in f:
        if line.strip():
            segments.append(json.loads(line))

print(f"Total segments loaded: {len(segments)}")

matches = []
for s in segments:
    text = s.get("text", "")
    ja_text = s.get("ja_text", "")
    matched_words = []
    for pat in compiled_patterns:
        m = pat.findall(text)
        if m:
            matched_words.extend(m)
    if matched_words:
        matches.append({
            "id": s["id"],
            "start": s["start"],
            "end": s["end"],
            "text": text,
            "ja_text": ja_text,
            "matched": list(set(matched_words))
        })

print(f"Found {len(matches)} potential bug/issue mentions in Klana transcript.")

def fmt_ts(sec):
    m = int(sec) // 60
    s = int(sec) % 60
    return f"{m:02d}:{s:02d}"

out_path = r"D:\04_tiktok_game\NICODROW\output_klana\klana_bug_mentions.json"
with open(out_path, "w", encoding="utf-8") as f:
    json.dump(matches, f, ensure_ascii=False, indent=2)

print(f"Saved to {out_path}")
for m in matches[:30]:
    print(f"[{fmt_ts(m['start'])}] (Klana) {m['text']} -> {m['ja_text']}")
