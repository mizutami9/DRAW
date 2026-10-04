import os
import sys
import json
import time
import wave
import struct
import numpy as np

MERGED_JSON = r"D:\04_tiktok_game\NICODROW\work\merged_segments.json"
CACHE_DIR = r"D:\04_tiktok_game\NICODROW\work\tts_cache"
ORIGINAL_WAV = r"D:\04_tiktok_game\NICODROW\work\original_audio_48k.wav"
OUTPUT_WAV = r"D:\04_tiktok_game\NICODROW\work\final_dubbed_audio.wav"

def mix_audio():
    print("Loading merged segments...")
    with open(MERGED_JSON, 'r', encoding='utf-8') as f:
        segments = json.load(f)

    # 1. 元音声のWAV情報を取得
    print(f"Opening original audio: {ORIGINAL_WAV}")
    orig_wf = wave.open(ORIGINAL_WAV, 'rb')
    n_channels = orig_wf.getnchannels()
    sampwidth = orig_wf.getsampwidth()
    framerate = orig_wf.getframerate()
    n_frames = orig_wf.getnframes()
    duration_sec = n_frames / framerate
    print(f"Original WAV: {n_channels}ch, {framerate}Hz, {sampwidth*8}bit, {n_frames} frames ({duration_sec/60:.1f}min)")

    # 2. ダッキングエンベロープ（音量マスク）とTTS配置用バッファを作成
    # 巨大メモリを避けるため、1分（60秒 = 2,880,000フレーム）単位で処理
    CHUNK_SEC = 60
    CHUNK_FRAMES = CHUNK_SEC * framerate

    # 事前に各セグメントの配置情報を整理
    # VOICEVOX の wav (24kHz mono) を読み込んで 48kHz stereo に変換するヘルパー
    def load_tts_wav(idx):
        path = os.path.join(CACHE_DIR, f"seg_{idx:04d}.wav")
        if not os.path.exists(path) or os.path.getsize(path) < 100:
            return None
        try:
            with wave.open(path, 'rb') as wf:
                tts_rate = wf.getframerate()
                tts_frames = wf.getnframes()
                raw = wf.readframes(tts_frames)
                data = np.frombuffer(raw, dtype=np.int16).astype(np.float32) / 32768.0
                # 24kHz -> 48kHz アップサンプリング (2倍リピート or 線形補間)
                if tts_rate == 24000 and framerate == 48000:
                    data = np.repeat(data, 2)
                elif tts_rate != framerate:
                    # 簡易リサンプリング
                    target_len = int(len(data) * framerate / tts_rate)
                    data = np.interp(np.linspace(0, len(data), target_len), np.arange(len(data)), data)
                return data
        except Exception as e:
            print(f"Error loading {path}: {e}")
            return None

    # 各セグメントのキャッシュされた音声データをロード
    print("Pre-loading TTS audio samples...")
    tts_data_map = {}
    valid_count = 0
    for s in segments:
        idx = s['idx']
        audio = load_tts_wav(idx)
        if audio is not None:
            tts_data_map[idx] = audio
            valid_count += 1

    print(f"Loaded {valid_count} TTS audio clips.")

    # 3. ダッキングタイムライン（秒単位またはフレーム単位で元音声音量を計算）
    # 全フレームのゲイン配列 (float32) を作る
    print("Calculating ducking curve...")
    duck_gain = np.ones(n_frames, dtype=np.float32)

    FADE_IN_FRAMES = int(0.15 * framerate)   # 0.15秒で下がる
    FADE_OUT_FRAMES = int(0.35 * framerate)  # 0.35秒で戻る
    DUCKED_VOL = 0.16                        # 元音声（英語＋BGM）の音量を16%に抑える

    for s in segments:
        idx = s['idx']
        if idx not in tts_data_map:
            continue
        audio_len = len(tts_data_map[idx])
        start_frame = int(s['start'] * framerate)
        end_frame = start_frame + audio_len

        if start_frame >= n_frames:
            continue
        end_frame = min(end_frame, n_frames)

        # ダッキング範囲を計算 (開始前FADE_IN_FRAMES 〜 終了後FADE_OUT_FRAMES)
        f_start = max(0, start_frame - FADE_IN_FRAMES)
        f_end = min(n_frames, end_frame + FADE_OUT_FRAMES)

        # ダッキング区間を適用
        # 音声を喋っている区間
        duck_gain[start_frame:end_frame] = np.minimum(duck_gain[start_frame:end_frame], DUCKED_VOL)
        
        # フェードイン区間
        if start_frame > f_start:
            fade_in = np.linspace(1.0, DUCKED_VOL, start_frame - f_start, dtype=np.float32)
            duck_gain[f_start:start_frame] = np.minimum(duck_gain[f_start:start_frame], fade_in)
            
        # フェードアウト区間
        if f_end > end_frame:
            fade_out = np.linspace(DUCKED_VOL, 1.0, f_end - end_frame, dtype=np.float32)
            duck_gain[end_frame:f_end] = np.minimum(duck_gain[end_frame:f_end], fade_out)

    print("Ducking curve calculated. Writing mixed output audio...")

    # 4. 出力WAVの書き出し
    out_wf = wave.open(OUTPUT_WAV, 'wb')
    out_wf.setnchannels(2)
    out_wf.setsampwidth(2)
    out_wf.setframerate(framerate)

    # チャンクごとに元音声を読み込み、ダッキングとTTS加算を行って書き出す
    TTS_GAIN = 1.35  # 日本語吹き替え音声を明瞭にするためのゲイン

    processed_frames = 0
    t0 = time.time()

    # セグメントのインデックスを追跡
    # チャンク内に重なるセグメントを検出して加算する
    while processed_frames < n_frames:
        curr_chunk = min(CHUNK_FRAMES, n_frames - processed_frames)
        chunk_start_f = processed_frames
        chunk_end_f = processed_frames + curr_chunk

        # 元音声を読み込み
        raw_orig = orig_wf.readframes(curr_chunk)
        orig_data = np.frombuffer(raw_orig, dtype=np.int16).astype(np.float32) / 32768.0

        if n_channels == 1:
            # モノラルをステレオに拡張
            orig_stereo = np.column_stack((orig_data, orig_data))
        else:
            orig_stereo = orig_data.reshape(-1, 2)

        # ダッキングゲインを適用
        gain_chunk = duck_gain[chunk_start_f:chunk_end_f, np.newaxis]
        mixed_stereo = orig_stereo * gain_chunk

        # このチャンクに重なるTTS音声を加算
        for s in segments:
            idx = s['idx']
            if idx not in tts_data_map:
                continue
            tts_clip = tts_data_map[idx]
            clip_len = len(tts_clip)
            clip_start_f = int(s['start'] * framerate)
            clip_end_f = clip_start_f + clip_len

            # チャンクと交差しているか
            if clip_end_f <= chunk_start_f or clip_start_f >= chunk_end_f:
                continue

            # チャンク内でのオフセットとクリップ内でのオフセットを計算
            src_start = max(0, chunk_start_f - clip_start_f)
            src_end = min(clip_len, chunk_end_f - clip_start_f)

            dst_start = max(0, clip_start_f - chunk_start_f)
            dst_end = dst_start + (src_end - src_start)

            part = tts_clip[src_start:src_end] * TTS_GAIN
            # ステレオ両チャンネルに加算
            mixed_stereo[dst_start:dst_end, 0] += part
            mixed_stereo[dst_start:dst_end, 1] += part

        # クリッピング防止（リミッター）
        np.clip(mixed_stereo, -0.98, 0.98, out=mixed_stereo)

        # int16に変換して書き出し
        out_int16 = (mixed_stereo * 32767.0).astype(np.int16)
        out_wf.writeframes(out_int16.tobytes())

        processed_frames += curr_chunk
        if processed_frames % (CHUNK_FRAMES * 5) == 0 or processed_frames >= n_frames:
            pct = processed_frames / n_frames * 100
            print(f"Mixing progress: {pct:.1f}% ({processed_frames/framerate/60:.1f} / {duration_sec/60:.1f} min)")

    orig_wf.close()
    out_wf.close()
    print(f"Successfully mixed audio in {time.time() - t0:.1f}s! Saved to: {OUTPUT_WAV}")

if __name__ == "__main__":
    mix_audio()
