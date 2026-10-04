import argparse
import json
from pathlib import Path

from faster_whisper import WhisperModel


def timestamp(seconds: float) -> str:
    total_ms = max(0, round(seconds * 1000))
    hours, remainder = divmod(total_ms, 3_600_000)
    minutes, remainder = divmod(remainder, 60_000)
    secs, millis = divmod(remainder, 1000)
    return f"{hours:02d}:{minutes:02d}:{secs:02d},{millis:03d}"


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("audio")
    parser.add_argument("model")
    parser.add_argument("output_dir")
    args = parser.parse_args()

    output_dir = Path(args.output_dir)
    output_dir.mkdir(parents=True, exist_ok=True)

    model = WhisperModel(
        args.model,
        device="cpu",
        compute_type="int8",
        cpu_threads=8,
    )
    segments, info = model.transcribe(
        args.audio,
        language="en",
        beam_size=10,
        best_of=10,
        patience=1.5,
        word_timestamps=True,
        vad_filter=False,
        condition_on_previous_text=True,
        initial_prompt=(
            "NICO DRAW gameplay with Nova, Bup, Kiana, and Oshen. "
            "Natural conversational English, laughter, reactions, and game commentary."
        ),
    )

    records = []
    for segment in segments:
        records.append(
            {
                "start": segment.start,
                "end": segment.end,
                "text": segment.text.strip(),
                "avg_logprob": segment.avg_logprob,
                "no_speech_prob": segment.no_speech_prob,
                "words": [
                    {
                        "start": word.start,
                        "end": word.end,
                        "word": word.word,
                        "probability": word.probability,
                    }
                    for word in (segment.words or [])
                ],
            }
        )

    (output_dir / "raw_segments.json").write_text(
        json.dumps(
            {
                "language": info.language,
                "duration": info.duration,
                "segments": records,
            },
            ensure_ascii=False,
            indent=2,
        ),
        encoding="utf-8",
    )

    with (output_dir / "raw_transcript.txt").open("w", encoding="utf-8") as handle:
        for record in records:
            handle.write(f"[{timestamp(record['start'])}] {record['text']}\n")

    with (output_dir / "raw_subtitles.srt").open("w", encoding="utf-8") as handle:
        for index, record in enumerate(records, 1):
            handle.write(
                f"{index}\n{timestamp(record['start'])} --> {timestamp(record['end'])}\n"
                f"{record['text']}\n\n"
            )

    print(json.dumps(records, ensure_ascii=False, indent=2))


if __name__ == "__main__":
    main()
