from __future__ import annotations

import csv
import json
import re
import time
from pathlib import Path

import requests


ROOT = Path(r"C:\workSpace\PICO")
WORK = ROOT / ".speaker_audit"
SOURCE_CSV = WORK / "speaker_translation_review_source.csv"
CHECKPOINT = WORK / "hymt_translations.json"
PROGRESS = WORK / "hymt_progress.json"
OUTPUT_CSV = WORK / "speaker_translation_review_translated.csv"
API_URL = "http://127.0.0.1:8181/v1/chat/completions"
BATCH_SIZE = 12


BASE_INSTRUCTIONS = """[Background Information]
Four adult friends—Bup, Kiana, Nova, and Oshen—are casually playing the physics co-op game NICO DRAW on a livestream. They joke, swear, tease each other, draw characters, throw and catch players, use boxes, keys, scales, ink, and retry stages.

[Required terminology]
NICO DRAW = NICO DRAW
crayon/crayons = クレヨン
ink = インク
redraw = 描き直す
lobby = ロビー
host = ホスト
key = 鍵
box/crate = 箱/木箱
scale = 計量器
pick up / pick me up = 持ち上げる / つかむ（車で迎えに行く意味ではない）
preset = プリセット
Bow/bow is often a speech-recognition error for the name Bup; translate it as Bup when a person is being addressed.
Bup, Kiana, Nova, Oshen are names and must remain unchanged.

Translate the following spoken English into concise, natural, casual Japanese subtitles.
- Preserve jokes, repetition, surprise, and profanity; do not make everyone polite.
- Use casual spoken Japanese. Avoid formal 「です・ます」 unless the speaker is jokingly being formal.
- Do not invent details when speech recognition is malformed. Translate conservatively.
- Retain every <ID:speaker> delimiter exactly once and in the same order.
- Output only one Japanese subtitle line per delimiter. No notes or explanations.
"""


def load_checkpoint() -> dict[str, str]:
    if CHECKPOINT.exists():
        return json.loads(CHECKPOINT.read_text(encoding="utf-8"))
    return {}


def save_checkpoint(items: dict[str, str], total: int) -> None:
    CHECKPOINT.write_text(json.dumps(items, ensure_ascii=False, indent=2), encoding="utf-8")
    PROGRESS.write_text(
        json.dumps({"done": len(items), "total": total}, ensure_ascii=False, indent=2),
        encoding="utf-8",
    )


def clean_translation(text: str) -> str:
    text = re.sub(r"\s+", " ", text).strip()
    text = text.replace("クレヨン画", "クレヨン")
    return text


def request_batch(batch: list[dict[str, str]]) -> dict[str, str]:
    lines = []
    for row in batch:
        source = re.sub(r"\bbow\b", "Bup", row["English"], flags=re.I)
        source = re.sub(r"\bit always legs\b", "it always lags", source, flags=re.I)
        lines.append(f"<{row['No']}:{row['Speaker']}> {source}")
    payload = {
        "model": "local",
        "messages": [{"role": "user", "content": BASE_INSTRUCTIONS + "\n" + "\n".join(lines)}],
        "temperature": 0.5,
        "top_p": 0.6,
        "top_k": 20,
        "repeat_penalty": 1.05,
        "max_tokens": 1200,
    }
    response = requests.post(API_URL, json=payload, timeout=600)
    response.raise_for_status()
    response.encoding = "utf-8"
    content = response.json()["choices"][0]["message"]["content"]
    parsed: dict[str, str] = {}
    pattern = re.compile(r"<(?P<id>\d+):(?P<speaker>[^>]+)>\s*(?P<text>.*?)(?=\n<\d+:|\Z)", re.S)
    for match in pattern.finditer(content.strip()):
        parsed[match.group("id")] = clean_translation(match.group("text"))
    return parsed


def main() -> None:
    WORK.mkdir(parents=True, exist_ok=True)
    with SOURCE_CSV.open(encoding="utf-8-sig", newline="") as handle:
        rows = list(csv.DictReader(handle))

    translations = load_checkpoint()
    pending = [row for row in rows if row["No"] not in translations]
    total = len(rows)

    for offset in range(0, len(pending), BATCH_SIZE):
        batch = pending[offset : offset + BATCH_SIZE]
        expected = {row["No"] for row in batch}
        parsed: dict[str, str] = {}
        for attempt in range(3):
            try:
                parsed = request_batch(batch)
                if expected <= parsed.keys():
                    break
            except Exception as exc:
                print(f"batch error attempt {attempt + 1}: {exc}", flush=True)
            time.sleep(2)

        # Retry missing rows individually, using the current Japanese only as a last-resort fallback.
        for row in batch:
            key = row["No"]
            value = parsed.get(key, "").strip()
            if not value:
                try:
                    single = request_batch([row])
                    value = single.get(key, "").strip()
                except Exception:
                    value = ""
            translations[key] = value or row["Current Japanese"]

        save_checkpoint(translations, total)
        print(f"PROGRESS {len(translations)}/{total}", flush=True)

    fieldnames = list(rows[0].keys())
    with OUTPUT_CSV.open("w", encoding="utf-8-sig", newline="") as handle:
        writer = csv.DictWriter(handle, fieldnames=fieldnames)
        writer.writeheader()
        for row in rows:
            row["Revised Japanese"] = translations[row["No"]]
            row["Checked"] = "AUTO-HyMT"
            writer.writerow(row)

    print(f"WROTE {OUTPUT_CSV}", flush=True)


if __name__ == "__main__":
    main()
