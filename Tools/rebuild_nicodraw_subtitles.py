from __future__ import annotations

import csv
import re
from pathlib import Path


ROOT = Path(r"D:\04_tiktok_game\NICODROW\output")
DEST = ROOT / "revised_speaker_fixed"

# The old labels were attached to the correct on-screen colors but to the
# wrong people. Keep the timing/color assignment and correct only the names.
NAME_MAP = {
    "Oshen": "Bup",    # red
    "Klana": "Kiana",  # blue (also fixes the typo)
    "Bup": "Nova",     # green
    "Maya": "Oshen",   # yellow
}


def replace_names(text: str) -> str:
    placeholders = {old: f"@@SPEAKER_{i}@@" for i, old in enumerate(NAME_MAP)}
    for old, placeholder in placeholders.items():
        text = text.replace(old, placeholder)
    for old, placeholder in placeholders.items():
        text = text.replace(placeholder, NAME_MAP[old])
    return text


def read_utf8(path: Path) -> str:
    return path.read_text(encoding="utf-8-sig")


def write_utf8(path: Path, text: str) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(text, encoding="utf-8-sig", newline="")


def parse_srt(text: str) -> list[tuple[str, str, str]]:
    blocks = re.split(r"\r?\n\r?\n+", text.strip())
    result: list[tuple[str, str, str]] = []
    for block in blocks:
        lines = block.splitlines()
        if len(lines) >= 3 and " --> " in lines[1]:
            result.append((lines[0], lines[1], "\n".join(lines[2:])))
    return result


def time_to_ms(value: str) -> int:
    match = re.fullmatch(r"(\d+):(\d+):(\d+)[,.](\d+)", value.strip())
    if not match:
        raise ValueError(value)
    h, m, s, frac = match.groups()
    frac_ms = int((frac + "000")[:3])
    return ((int(h) * 60 + int(m)) * 60 + int(s)) * 1000 + frac_ms


def span(value: str) -> tuple[int, int]:
    start, end = value.split(" --> ")
    return time_to_ms(start), time_to_ms(end)


def build_review_csv() -> None:
    english = parse_srt(read_utf8(ROOT / "english_subtitles.srt"))
    japanese = parse_srt(
        replace_names(read_utf8(ROOT / "editor_subtitles" / "all_subtitles_clean.srt"))
    )
    en_spans = [(span(t), text.replace("\n", " ")) for _, t, text in english]

    out = DEST / "translation_review.csv"
    with out.open("w", encoding="utf-8-sig", newline="") as handle:
        writer = csv.writer(handle)
        writer.writerow(["No", "Time", "Speaker", "English", "Current Japanese", "Revised Japanese"])
        for i, (_, timing, current_ja) in enumerate(japanese, 1):
            start, end = span(timing)
            candidates: list[str] = []
            for (en_start, en_end), en_text in en_spans:
                overlap = min(end, en_end) - max(start, en_start)
                if overlap > 0:
                    candidates.append(en_text)
            speaker_match = re.match(r"【([^】]+)】\s*", current_ja)
            speaker = speaker_match.group(1) if speaker_match else ""
            ja_text = re.sub(r"^【[^】]+】\s*", "", current_ja).replace("\n", " ")
            writer.writerow([i, timing, speaker, " ".join(candidates), ja_text, ""])


def main() -> None:
    DEST.mkdir(parents=True, exist_ok=True)

    ass_text = replace_names(read_utf8(ROOT / "twitch_fixed_subtitled.ass"))
    write_utf8(DEST / "twitch_fixed_speaker_corrected.ass", ass_text)

    clean_text = replace_names(read_utf8(ROOT / "editor_subtitles" / "all_subtitles_clean.srt"))
    write_utf8(DEST / "all_subtitles_speaker_corrected.srt", clean_text)

    for old, new in NAME_MAP.items():
        source = ROOT / "editor_subtitles" / f"{old}_subtitles.srt"
        write_utf8(DEST / f"{new}_subtitles.srt", read_utf8(source))

    build_review_csv()

    note = """NICO DRAW subtitle revision workspace

Speaker/color mapping applied:
- Red: Bup
- Blue: Kiana
- Green: Nova
- Yellow: Oshen

The original files were not overwritten. Japanese wording is still the current
machine translation; use translation_review.csv for semantic proofreading.
"""
    write_utf8(DEST / "README.txt", note)


if __name__ == "__main__":
    main()
