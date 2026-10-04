from __future__ import annotations

import csv
import json
import re
from collections import Counter
from pathlib import Path


ROOT = Path(r"D:\04_tiktok_game\NICODROW\output")
DIARIZATION = Path(r"C:\workSpace\PICO\.speaker_audit\diarization_4speakers.json")
DEST = ROOT / "revised_voice_verified"

SPEAKER_NAMES = {0: "Oshen", 1: "Kiana", 2: "Nova", 3: "Bup"}


def read_utf8(path: Path) -> str:
    return path.read_text(encoding="utf-8-sig")


def write_utf8(path: Path, text: str) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(text, encoding="utf-8-sig", newline="")


def parse_time(value: str) -> float:
    parts = re.split(r"[:,]", value.strip())
    h, m, s, frac = parts
    return int(h) * 3600 + int(m) * 60 + int(s) + int((frac + "000")[:3]) / 1000


def parse_span(value: str) -> tuple[float, float]:
    start, end = value.split(" --> ")
    return parse_time(start), parse_time(end)


def parse_srt(text: str) -> list[dict[str, object]]:
    result = []
    for block in re.split(r"\r?\n\r?\n+", text.strip()):
        lines = block.splitlines()
        if len(lines) >= 3 and " --> " in lines[1]:
            result.append({"index": lines[0], "time": lines[1], "text": "\n".join(lines[2:])})
    return result


def dominant_speaker(start: float, end: float, turns: list[dict]) -> tuple[str, float, str]:
    overlaps: Counter[int] = Counter()
    for turn in turns:
        overlap = max(0.0, min(end, turn["end"]) - max(start, turn["start"]))
        if overlap:
            overlaps[turn["speaker"]] += overlap

    if not overlaps:
        midpoint = (start + end) / 2
        nearest = min(
            turns,
            key=lambda turn: min(abs(midpoint - turn["start"]), abs(midpoint - turn["end"])),
        )
        distance = min(abs(midpoint - nearest["start"]), abs(midpoint - nearest["end"]))
        if distance <= 0.75:
            return SPEAKER_NAMES[nearest["speaker"]], 0.0, "nearest"
        return "Unknown", 0.0, "none"

    ranked = overlaps.most_common()
    speaker_id, best = ranked[0]
    total = sum(overlaps.values())
    confidence = best / total if total else 0.0
    mode = "overlap" if len(ranked) == 1 else "mixed"
    return SPEAKER_NAMES[speaker_id], confidence, mode


def replace_visible_speaker(text: str, speaker: str) -> str:
    if re.match(r"^【[^】]+】", text):
        return re.sub(r"^【[^】]+】", f"【{speaker}】", text, count=1)
    return f"【{speaker}】 {text}"


def main() -> None:
    turns = json.loads(DIARIZATION.read_text(encoding="utf-8"))
    DEST.mkdir(parents=True, exist_ok=True)

    cues = parse_srt(read_utf8(ROOT / "editor_subtitles" / "all_subtitles_clean.srt"))
    english = parse_srt(read_utf8(ROOT / "english_subtitles.srt"))
    assigned: list[dict[str, object]] = []

    for cue in cues:
        start, end = parse_span(str(cue["time"]))
        speaker, confidence, mode = dominant_speaker(start, end, turns)
        text = replace_visible_speaker(str(cue["text"]), speaker)
        assigned.append({**cue, "start": start, "end": end, "speaker": speaker,
                         "confidence": confidence, "mode": mode, "text": text})

    srt_blocks = [
        f"{i}\n{cue['time']}\n{cue['text']}"
        for i, cue in enumerate(assigned, 1)
    ]
    write_utf8(DEST / "all_subtitles_voice_reassigned.srt", "\n\n".join(srt_blocks) + "\n")

    for speaker in (*SPEAKER_NAMES.values(), "Unknown"):
        own = [cue for cue in assigned if cue["speaker"] == speaker]
        blocks = []
        for i, cue in enumerate(own, 1):
            plain = re.sub(r"^【[^】]+】\s*", "", str(cue["text"]))
            blocks.append(f"{i}\n{cue['time']}\n{plain}")
        write_utf8(DEST / f"{speaker}_subtitles.srt", "\n\n".join(blocks) + ("\n" if blocks else ""))

    # Reassign ASS styles and visible names while preserving each event's timing/text.
    ass_lines = read_utf8(ROOT / "twitch_fixed_subtitled.ass").splitlines()
    style_map = {
        "Oshen": "Bup",   # red style becomes Bup
        "Klana": "Kiana", # blue style becomes Kiana
        "Bup": "Nova",    # green style becomes Nova
        "Maya": "Oshen",  # yellow style becomes Oshen
    }
    out_lines = []
    for line in ass_lines:
        if line.startswith("Style: "):
            add_unknown_style = False
            for old, new in style_map.items():
                if line.startswith(f"Style: {old},"):
                    line = f"Style: {new}," + line.split(",", 1)[1]
                    add_unknown_style = old == "Maya"
                    break
        elif line.startswith("Dialogue:"):
            fields = line.split(",", 9)
            start = parse_time(fields[1].replace(".", ","))
            end = parse_time(fields[2].replace(".", ","))
            speaker, _, _ = dominant_speaker(start, end, turns)
            fields[3] = speaker
            fields[4] = speaker
            fields[9] = replace_visible_speaker(fields[9], speaker)
            line = ",".join(fields)
        out_lines.append(line)
        if line.startswith("Style: Oshen,") and add_unknown_style:
            unknown = line.replace("Style: Oshen,", "Style: Unknown,", 1)
            unknown = re.sub(r"&H[0-9A-Fa-f]{8}", "&H00FFFFFF", unknown, count=1)
            out_lines.append(unknown)
    write_utf8(DEST / "twitch_fixed_voice_reassigned.ass", "\n".join(out_lines) + "\n")

    # Human-review sheet: source English, current Japanese, and assignment confidence.
    with (DEST / "speaker_translation_review.csv").open("w", encoding="utf-8-sig", newline="") as handle:
        writer = csv.writer(handle)
        writer.writerow(["No", "Time", "Speaker", "Speaker confidence", "Mode",
                         "English", "Current Japanese", "Revised Japanese", "Checked"])
        for i, cue in enumerate(assigned, 1):
            source_parts = []
            for en in english:
                en_start, en_end = parse_span(str(en["time"]))
                if min(cue["end"], en_end) > max(cue["start"], en_start):
                    source_parts.append(str(en["text"]).replace("\n", " "))
            ja = re.sub(r"^【[^】]+】\s*", "", str(cue["text"])).replace("\n", " ")
            writer.writerow([i, cue["time"], cue["speaker"], f"{cue['confidence']:.3f}",
                             cue["mode"], " ".join(source_parts), ja, "", ""])

    counts = Counter(str(cue["speaker"]) for cue in assigned)
    low = sum(
        1
        for cue in assigned
        if cue["speaker"] == "Unknown" or cue["confidence"] < 0.6
    )
    summary = {
        "mapping": SPEAKER_NAMES,
        "subtitle_counts": counts,
        "total_subtitles": len(assigned),
        "needs_review": low,
    }
    write_utf8(DEST / "speaker_assignment_summary.json", json.dumps(summary, indent=2, ensure_ascii=False))


if __name__ == "__main__":
    main()
