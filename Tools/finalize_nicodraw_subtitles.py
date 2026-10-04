from __future__ import annotations

import csv
import json
import re
from collections import Counter
from pathlib import Path


ROOT = Path(r"D:\04_tiktok_game\NICODROW\output")
WORK = Path(r"C:\workSpace\PICO\.speaker_audit")
SOURCE_SRT = ROOT / "editor_subtitles" / "all_subtitles_clean.srt"
SOURCE_ASS = ROOT / "revised_voice_verified" / "twitch_fixed_voice_reassigned.ass"
TRANSLATIONS = WORK / "speaker_translation_review_translated.csv"
DIARIZATION = WORK / "diarization_4speakers.json"
SEGMENTS = ROOT / "segments.jsonl"
DEST = WORK / "final_translated_reviewed"

SPEAKERS = {0: "Oshen", 1: "Kiana", 2: "Nova", 3: "Bup"}
COLORS = {"Bup": "赤", "Kiana": "青", "Nova": "緑", "Oshen": "黄", "Unknown": "白"}


def parse_time(value: str) -> float:
    h, m, s, frac = re.split(r"[:,.]", value.strip())
    return int(h) * 3600 + int(m) * 60 + int(s) + int((frac + "000")[:3]) / 1000


def parse_span(value: str) -> tuple[float, float]:
    a, b = value.split(" --> ")
    return parse_time(a), parse_time(b)


def parse_srt(text: str) -> list[dict[str, object]]:
    cues = []
    for block in re.split(r"\r?\n\r?\n+", text.strip()):
        lines = block.splitlines()
        if len(lines) >= 3 and " --> " in lines[1]:
            start, end = parse_span(lines[1])
            cues.append({"time": lines[1], "start": start, "end": end, "text": "\n".join(lines[2:])})
    return cues


def word_speaker(word: dict, turns: list[dict]) -> str | None:
    start, end = float(word["start"]), float(word["end"])
    scores: Counter[int] = Counter()
    for turn in turns:
        overlap = max(0.0, min(end, float(turn["end"])) - max(start, float(turn["start"])))
        if overlap:
            scores[int(turn["speaker"])] += overlap
    if not scores:
        return None
    return SPEAKERS[scores.most_common(1)[0][0]]


def assign_by_words(cues: list[dict], segments: list[dict], turns: list[dict]) -> None:
    words = [word for segment in segments for word in segment.get("words", [])]
    for cue in cues:
        scores: Counter[str] = Counter()
        probs = []
        for word in words:
            if float(word["end"]) <= cue["start"]:
                continue
            if float(word["start"]) >= cue["end"]:
                continue
            speaker = word_speaker(word, turns)
            if speaker:
                duration = max(0.05, float(word["end"]) - float(word["start"]))
                probability = float(word.get("probability", 1.0))
                scores[speaker] += duration * max(0.25, probability)
                probs.append(probability)
        if scores:
            speaker, best = scores.most_common(1)[0]
            cue["speaker"] = speaker
            cue["speaker_confidence"] = best / sum(scores.values())
            cue["speaker_mode"] = "word" if len(scores) == 1 else "word-mixed"
            cue["asr_confidence"] = sum(probs) / len(probs) if probs else 0.0
        else:
            cue["speaker"] = "Unknown"
            cue["speaker_confidence"] = 0.0
            cue["speaker_mode"] = "no-word-overlap"
            cue["asr_confidence"] = 0.0

    # Very short interjections can be omitted by diarization. Use a nearby turn
    # only inside a narrow window; otherwise retain Unknown rather than guessing.
    for cue in cues:
        if cue["speaker"] != "Unknown":
            continue
        midpoint = (cue["start"] + cue["end"]) / 2
        nearest = min(
            turns,
            key=lambda turn: min(abs(midpoint - float(turn["start"])), abs(midpoint - float(turn["end"]))),
        )
        distance = min(abs(midpoint - float(nearest["start"])), abs(midpoint - float(nearest["end"])))
        if distance <= 1.60:
            cue["speaker"] = SPEAKERS[int(nearest["speaker"])]
            cue["speaker_mode"] = "nearest-short-voice"

    # Resolve only very short unassigned fillers when both neighboring cues agree.
    for index, cue in enumerate(cues):
        if cue["speaker"] != "Unknown" or index == 0 or index + 1 == len(cues):
            continue
        previous = cues[index - 1]["speaker"]
        following = cues[index + 1]["speaker"]
        if previous == following and previous != "Unknown":
            cue["speaker"] = previous
            cue["speaker_mode"] = "context-neighbors"


def visible_text(speaker: str, translation: str) -> str:
    return f"【{speaker}】 {translation.strip()}"


def polish_translation(english: str, translated: str, previous: str) -> str:
    """Apply conservative, game-specific fixes after machine translation."""
    exact = {
        "Yeah": "うん", "Oh": "あっ", "No": "いや", "Hi": "やあ",
        "Okay": "オーケー", "What?": "何？",
        "Huh?": "え？", "Let's go": "よし！", "Dude": "おい", "Alright": "よし",
        "but": "でも", "Thank you": "ありがとう", "Come here": "こっち来て",
    }
    key = english.strip().rstrip(".")
    if key in exact:
        translated = exact[key]
    manual = {
        "I hate it How many of those do we need? Oh no, we're chilling for uh":
            "最悪。これ何個必要なの？ あ、いや、もう十分か…",
        "Long long man, you are I plug it instantly. Don't worry worry about this one. Okay, but I can't dodge the boxes":
            "長い、長いって！ すぐ差し込むから心配すんな。いや、でも箱を避けられない！",
        "It's it's it's the frisbee No, no, no, you can't shoot No, no":
            "そ、それフリスビーだよ。いやいや、撃てないって！",
        "Why am I in the void screen again?": "また真っ暗な画面なんだけど、なんで？",
        "It was doing that to me too": "私も同じことになってた",
        "Almost Oh, god": "あと少し！ ああ、もう！",
        "I am Tiny custom moon Hey Cheerio Let's share": "俺、小さいぞ。ねえCheerio、一緒にやろう",
    }
    if english.strip() in manual:
        translated = manual[english.strip()]
    # Keep the participants' established spellings instead of katakana variants.
    for pattern, name in [
        (r"キアナ", "Kiana"), (r"ノヴァ|ノバ", "Nova"),
        (r"バップ|バプ", "Bup"), (r"オシェン|オーシェン", "Oshen"),
    ]:
        translated = re.sub(pattern, name, translated, flags=re.I)
    if re.search(r"\bbow\b", english, re.I):
        translated = translated.replace("弓", "Bup").replace("ボウ", "Bup")
        if "Bup" not in translated and re.search(r"\bthrow bow\b", english, re.I):
            translated = "Bupを投げようとすると、いつもラグる" if "always legs" in english.lower() else translated
    if re.search(r"\bit always legs when i try to throw bow\b", english, re.I):
        translated = "Bupを投げようとすると、いつもラグる"
        if english.lower().lstrip().startswith("i'm, sorry"):
            translated = "ごめん。" + translated
    if re.search(r"\bBo\b", english):
        translated = re.sub(r"ボップ|ボー|ボウ", "Bup", translated)
    if re.search(r"\balmost\b", english, re.I):
        translated = re.sub(r"ほとんど|もうすぐ|ほぼ", "あと少し", translated)
    if re.search(r"\bpreset\b", english, re.I) and "プリセット" not in translated:
        if "プリセット" in previous:
            translated = previous
        else:
            translated = translated.replace("決まった", "プリセットの")
    # In NICO DRAW, picking another player up means grabbing/lifting them.
    if re.search(r"\bpick(?:ing)? (?:me|you|him|her|them) up\b", english, re.I):
        translated = re.sub(r"迎えに(?:行|来)[^、。！？]*", "持ち上げ", translated)
    return re.sub(r"\s+", " ", translated).strip()


def main() -> None:
    DEST.mkdir(parents=True, exist_ok=True)
    cues = parse_srt(SOURCE_SRT.read_text(encoding="utf-8-sig"))
    turns = json.loads(DIARIZATION.read_text(encoding="utf-8"))
    segments = [json.loads(line) for line in SEGMENTS.read_text(encoding="utf-8").splitlines() if line.strip()]
    with TRANSLATIONS.open(encoding="utf-8-sig", newline="") as handle:
        translated = list(csv.DictReader(handle))
    if len(cues) != len(translated):
        raise RuntimeError(f"cue/translation mismatch: {len(cues)} != {len(translated)}")

    assign_by_words(cues, segments, turns)
    for cue, row in zip(cues, translated):
        cue["english"] = row["English"]
        cue["old_japanese"] = row["Current Japanese"]
        raw_translation = row["Revised Japanese"].strip() or row["Current Japanese"].strip()
        cue["translation"] = polish_translation(
            row["English"], raw_translation, row["Current Japanese"]
        )
        flags = []
        if cue["speaker"] == "Unknown":
            flags.append("話者未確定")
        elif cue["speaker_mode"] == "context-neighbors":
            flags.append("前後文脈で話者補完")
        elif cue["speaker_mode"] == "nearest-short-voice":
            flags.append("近接音声で話者補完")
        elif cue["speaker_confidence"] < 0.60:
            flags.append("同時発話あり")
        if cue["asr_confidence"] and cue["asr_confidence"] < 0.55:
            flags.append("英語音声認識要注意")
        cue["review"] = " / ".join(flags) if flags else "校正済み"

    blocks = [
        f"{i}\n{cue['time']}\n{visible_text(str(cue['speaker']), str(cue['translation']))}"
        for i, cue in enumerate(cues, 1)
    ]
    (DEST / "all_subtitles_ja_reviewed.srt").write_text(
        "\n\n".join(blocks) + "\n", encoding="utf-8-sig", newline=""
    )

    for speaker in ["Bup", "Kiana", "Nova", "Oshen", "Unknown"]:
        own = [cue for cue in cues if cue["speaker"] == speaker]
        speaker_blocks = [
            f"{i}\n{cue['time']}\n{cue['translation']}"
            for i, cue in enumerate(own, 1)
        ]
        (DEST / f"{speaker}_subtitles_ja.srt").write_text(
            "\n\n".join(speaker_blocks) + ("\n" if speaker_blocks else ""),
            encoding="utf-8-sig", newline=""
        )

    ass_lines = SOURCE_ASS.read_text(encoding="utf-8-sig").splitlines()
    dialogue_index = 0
    output_ass = []
    for line in ass_lines:
        if line.startswith("Dialogue:"):
            cue = cues[dialogue_index]
            fields = line.split(",", 9)
            fields[3] = str(cue["speaker"])
            fields[4] = str(cue["speaker"])
            fields[9] = visible_text(str(cue["speaker"]), str(cue["translation"])).replace("\n", r"\N")
            line = ",".join(fields)
            dialogue_index += 1
        output_ass.append(line)
    if dialogue_index != len(cues):
        raise RuntimeError(f"ASS dialogue mismatch: {dialogue_index} != {len(cues)}")
    (DEST / "twitch_fixed_ja_reviewed.ass").write_text(
        "\n".join(output_ass) + "\n", encoding="utf-8-sig", newline=""
    )

    fieldnames = ["No", "Time", "Color", "Speaker", "Speaker confidence", "Mode",
                  "ASR confidence", "English", "Previous Japanese", "Revised Japanese", "Review"]
    with (DEST / "subtitle_review.csv").open("w", encoding="utf-8-sig", newline="") as handle:
        writer = csv.DictWriter(handle, fieldnames=fieldnames)
        writer.writeheader()
        for i, cue in enumerate(cues, 1):
            writer.writerow({
                "No": i, "Time": cue["time"], "Color": COLORS[str(cue["speaker"])],
                "Speaker": cue["speaker"], "Speaker confidence": f"{cue['speaker_confidence']:.3f}",
                "Mode": cue["speaker_mode"], "ASR confidence": f"{cue['asr_confidence']:.3f}",
                "English": cue["english"], "Previous Japanese": cue["old_japanese"],
                "Revised Japanese": cue["translation"], "Review": cue["review"],
            })

    counts = Counter(str(cue["speaker"]) for cue in cues)
    review_counts = Counter(str(cue["review"]) for cue in cues)
    changed = sum(cue["translation"] != cue["old_japanese"] for cue in cues)
    summary = {
        "total": len(cues), "speaker_counts": counts, "review_counts": review_counts,
        "translation_changed": changed,
        "speaker_method": "word timestamps matched against four-speaker diarization",
        "color_mapping": {"red": "Bup", "blue": "Kiana", "green": "Nova", "yellow": "Oshen"},
    }
    (DEST / "summary.json").write_text(
        json.dumps(summary, ensure_ascii=False, indent=2), encoding="utf-8-sig"
    )
    readme = """NICO DRAW 日本語字幕（再校正版）

投稿用:
- twitch_fixed_ja_reviewed.ass: 色分け・話者名付きASS
- all_subtitles_ja_reviewed.srt: 話者名付きSRT

確認用:
- subtitle_review.csv: 英語、旧訳、修正版、話者信頼度、要確認理由
- summary.json: 件数集計

色と話者:
- 赤 Bup
- 青 Kiana
- 緑 Nova
- 黄 Oshen
- 白 Unknown（音声だけでは確定できない箇所）

話者は字幕テキストの旧ラベルではなく、実音声の4話者ダイアライゼーションと
英語ASRの単語時刻を照合して割り当てています。同時発話や英語ASR自体が不鮮明な
箇所はsubtitle_review.csvに明示しています。吹き替え動画は変更していません。
"""
    (DEST / "README.txt").write_text(readme, encoding="utf-8-sig", newline="")


if __name__ == "__main__":
    main()
