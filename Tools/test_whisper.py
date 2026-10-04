import faster_whisper
import time

print("Testing WhisperModel loading...")
start = time.time()
try:
    # まずは tiny または base でダウンロードと CUDA 動作をテスト
    model = faster_whisper.WhisperModel("base.en", device="cuda", compute_type="float16")
    print(f"Successfully loaded base.en in {time.time() - start:.2f}s!")
except Exception as e:
    print(f"CUDA float16 failed: {e}")
    try:
        model = faster_whisper.WhisperModel("base.en", device="cuda", compute_type="int8")
        print(f"Successfully loaded base.en with int8 in {time.time() - start:.2f}s!")
    except Exception as e2:
        print(f"CUDA int8 failed: {e2}")
        model = faster_whisper.WhisperModel("base.en", device="cpu", compute_type="int8")
        print(f"Successfully loaded base.en on CPU in {time.time() - start:.2f}s!")
