import numpy as np
import wave
import sys
import time

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
    # in1 is long (search), in2 is short (template)
    n1 = len(in1)
    n2 = len(in2)
    # FFT length
    n_fft = 1 << (n1 + n2 - 1).bit_length()
    f1 = np.fft.rfft(in1, n_fft)
    f2 = np.fft.rfft(in2[::-1], n_fft)
    conv = np.fft.irfft(f1 * f2, n_fft)
    # valid part
    valid_start = n2 - 1
    valid_end = n1
    return conv[valid_start:valid_end]

print("Loading audio segments for cross-correlation...")
REF_START = 600.0   # Oshen 10:00
REF_DUR = 120.0     # 2 minutes template
ref_audio, rate = read_wav_segment(wav_oshen, REF_START, REF_DUR)

SEARCH_START = 0.0  # Klana 00:00
SEARCH_DUR = 1200.0 # 20 minutes search window
search_audio, _ = read_wav_segment(wav_klana, SEARCH_START, SEARCH_DUR)

print(f"Reference: {len(ref_audio)/rate:.1f}s, Search Window: {len(search_audio)/rate:.1f}s, SampleRate: {rate}Hz")

# Normalize
ref_norm = (ref_audio - np.mean(ref_audio)) / (np.std(ref_audio) + 1e-6)
search_norm = (search_audio - np.mean(search_audio)) / (np.std(search_audio) + 1e-6)

# Downsample by 8x for fast coarse search (2000Hz)
factor = 8
ref_coarse = ref_norm[::factor]
search_coarse = search_norm[::factor]
rate_coarse = rate // factor

print("Computing coarse correlation via FFT...")
t0 = time.time()
corr = fft_correlate_valid(search_coarse, ref_coarse)
best_idx = np.argmax(corr)
peak_val = corr[best_idx] / len(ref_coarse)

offset_sec_coarse = (SEARCH_START + best_idx / rate_coarse) - REF_START
print(f"Coarse peak at index {best_idx} (Score: {peak_val:.3f}) in {time.time()-t0:.2f}s")
print(f"Coarse estimated offset: {offset_sec_coarse:+.3f}s")

# Fine search around coarse peak (+- 3.0s)
fine_center = int(best_idx * factor)
fine_range = int(rate * 3.0)
fine_start = max(0, fine_center - fine_range)
fine_end = min(len(search_norm) - len(ref_norm), fine_center + fine_range)

print(f"Refining correlation around fine index {fine_center}...")
sub_search = search_norm[fine_start : fine_end + len(ref_norm)]
fine_corr = fft_correlate_valid(sub_search, ref_norm)
best_fine_sub_idx = np.argmax(fine_corr)
best_fine_idx = fine_start + best_fine_sub_idx
fine_peak_val = fine_corr[best_fine_sub_idx] / len(ref_norm)

klana_match_sec = SEARCH_START + best_fine_idx / rate
offset_sec = klana_match_sec - REF_START

print(f"\n==================================================")
print(f"ALIGNMENT RESULT:")
print(f"  Peak Correlation Score: {fine_peak_val:.4f}")
print(f"  Oshen t={REF_START:.3f}s ({int(REF_START//60)}m{int(REF_START%60)}s) corresponds to Klana t={klana_match_sec:.3f}s ({int(klana_match_sec//60)}m{int(klana_match_sec%60)}s)")
print(f"  OFFSET: {offset_sec:+.3f} seconds")
print(f"==================================================")

if offset_sec > 0:
    print(f"Klana video started {abs(offset_sec):.3f}s BEFORE Oshen video.")
    print(f"To match Oshen timing, Klana needs to be trimmed by {abs(offset_sec):.3f}s at the beginning.")
else:
    print(f"Klana video started {abs(offset_sec):.3f}s AFTER Oshen video.")
    print(f"To match Oshen timing, Oshen started earlier by {abs(offset_sec):.3f}s.")
