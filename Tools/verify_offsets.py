import numpy as np
import wave
import sys

sys.stdout.reconfigure(encoding='utf-8')

wav_oshen = r"D:\04_tiktok_game\NICODROW\work\audio_16k.wav"
wav_klana = r"D:\04_tiktok_game\NICODROW\work_klana\audio_16k.wav"

def read_wav_segment(path, start_sec, duration_sec):
    with wave.open(path, 'rb') as wf:
        rate = wf.getframerate()
        start_frame = int(start_sec * rate)
        n_frames = int(duration_sec * rate)
        wf.setpos(start_frame)
        raw = wf.readframes(n_frames)
        data = np.frombuffer(raw, dtype=np.int16).astype(np.float32)
        return data, rate

def fft_correlate_valid(in1, in2):
    n1 = len(in1)
    n2 = len(in2)
    n_fft = 1 << (n1 + n2 - 1).bit_length()
    f1 = np.fft.rfft(in1, n_fft)
    f2 = np.fft.rfft(in2[::-1], n_fft)
    conv = np.fft.irfft(f1 * f2, n_fft)
    return conv[n2 - 1 : n1]

test_points = [600.0, 1200.0, 1800.0, 2400.0]
DUR = 60.0
factor = 4

print("Verifying offset across multiple time points...")
for ref_t in test_points:
    ref_audio, rate = read_wav_segment(wav_oshen, ref_t, DUR)
    # 予想されるKlana位置: ref_t - 23.2s
    expected_klana_t = ref_t - 23.228
    search_start = max(0.0, expected_klana_t - 10.0)
    search_audio, _ = read_wav_segment(wav_klana, search_start, DUR + 20.0)

    ref_norm = (ref_audio - np.mean(ref_audio)) / (np.std(ref_audio) + 1e-6)
    search_norm = (search_audio - np.mean(search_audio)) / (np.std(search_audio) + 1e-6)

    ref_c = ref_norm[::factor]
    search_c = search_norm[::factor]
    rate_c = rate // factor

    corr = fft_correlate_valid(search_c, ref_c)
    best_idx = np.argmax(corr)
    peak_score = corr[best_idx] / len(ref_c)

    klana_t = search_start + (best_idx * factor) / rate
    offset = klana_t - ref_t
    print(f"Oshen {int(ref_t//60):02d}:{int(ref_t%60):02d} -> Klana {int(klana_t//60):02d}:{klana_t%60:05.2f} | Offset = {offset:+.3f}s | Score = {peak_score:.3f}")
