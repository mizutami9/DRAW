import urllib.request
import urllib.parse
import json
import time

def test_synthesis():
    text = "こんにちは、NICO DRAWをプレイします！"
    speaker = 8  # 春日部つむぎ
    
    # 1. audio_query
    q_url = f"http://127.0.0.1:50021/audio_query?speaker={speaker}&text=" + urllib.parse.quote(text)
    req = urllib.request.Request(q_url, method="POST")
    with urllib.request.urlopen(req) as resp:
        query_data = json.loads(resp.read())
        
    # 話速を少し上げる
    query_data["speedScale"] = 1.15
    
    # 2. synthesis
    s_url = f"http://127.0.0.1:50021/synthesis?speaker={speaker}"
    data_bytes = json.dumps(query_data).encode('utf-8')
    req2 = urllib.request.Request(s_url, data=data_bytes, headers={"Content-Type": "application/json"}, method="POST")
    
    t0 = time.time()
    with urllib.request.urlopen(req2) as resp2:
        wav_data = resp2.read()
    elapsed = time.time() - t0
    
    out_path = r"D:\04_tiktok_game\NICODROW\work\test_voicevox.wav"
    with open(out_path, "wb") as f:
        f.write(wav_data)
        
    print(f"Synthesized {len(wav_data)} bytes in {elapsed:.3f}s! Saved to {out_path}")

if __name__ == "__main__":
    test_synthesis()
