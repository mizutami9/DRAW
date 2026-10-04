from __future__ import annotations

import json
from pathlib import Path

import sherpa_onnx
import soundfile as sf


ROOT = Path(r"C:\workSpace\PICO")
MODEL_ROOT = ROOT / ".speaker_models"
AUDIO = Path(r"D:\04_tiktok_game\NICODROW\work\audio_16k.wav")
OUTPUT = ROOT / ".speaker_audit" / "diarization_4speakers.json"


def progress(done: int, total: int) -> int:
    # Keep progress output useful without printing several thousand lines.
    pct = int(done * 100 / total)
    if pct != progress.last_pct:
        print(f"PROGRESS {pct}%", flush=True)
        progress.last_pct = pct
    return 0


progress.last_pct = -1


def main() -> None:
    config = sherpa_onnx.OfflineSpeakerDiarizationConfig(
        segmentation=sherpa_onnx.OfflineSpeakerSegmentationModelConfig(
            pyannote=sherpa_onnx.OfflineSpeakerSegmentationPyannoteModelConfig(
                model=str(
                    MODEL_ROOT
                    / "sherpa-onnx-pyannote-segmentation-3-0"
                    / "model.int8.onnx"
                ),
                window_shift_ratio=0.1,
            )
        ),
        embedding=sherpa_onnx.SpeakerEmbeddingExtractorConfig(
            model=str(MODEL_ROOT / "nemo_speakernet.onnx")
        ),
        clustering=sherpa_onnx.FastClusteringConfig(
            num_clusters=4,
            threshold=0.5,
        ),
        min_duration_on=0.3,
        min_duration_off=0.5,
    )
    if not config.validate():
        raise RuntimeError("Invalid speaker diarization configuration")

    engine = sherpa_onnx.OfflineSpeakerDiarization(config)
    audio, sample_rate = sf.read(AUDIO, dtype="float32", always_2d=True)
    if sample_rate != engine.sample_rate:
        raise RuntimeError(f"Expected {engine.sample_rate} Hz, got {sample_rate} Hz")

    result = engine.process(audio[:, 0], callback=progress).sort_by_start_time()
    records = [
        {"start": round(item.start, 3), "end": round(item.end, 3), "speaker": item.speaker}
        for item in result
    ]
    OUTPUT.parent.mkdir(parents=True, exist_ok=True)
    OUTPUT.write_text(json.dumps(records, indent=2), encoding="utf-8")
    print(f"WROTE {OUTPUT} ({len(records)} turns)", flush=True)


if __name__ == "__main__":
    main()
