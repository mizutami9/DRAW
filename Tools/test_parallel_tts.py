import urllib.request
import urllib.parse
import json
import time
from concurrent.futures import ThreadPoolExecutor

sample_texts = [
    "ここに私たちはいます、皆さんはクレヨンを食べていますか？",
    "はい、キアナはまだ自分自身を認識しようとしています。",
    "彼女に少し時間をあげてください。",
    "タップして再描画します。",
    "すごい！これはとても面白いゲームですね！"
]

def synthesize_one(item):
    idx, text = item
    speaker = 8  # 春日部つむぎ
    q_url = f"http://127.0.0.1:50021/audio_query?speaker={speaker}&text=" + urllib.parse.quote(text)
    req = urllib.request.Request(q_url, method="POST")
    with urllib.request.urlopen(req) as resp:
        query_data = json.loads(resp.read())
    
    query_data["speedScale"] = 1.15
    s_url = f"http://127.0.0.1:50021/synthesis?speaker={speaker}"
    data_bytes = json.dumps(query_data).encode('utf-8')
    req2 = urllib.request.Request(s_url, data=data_bytes, headers={"Content-Type": "application/json"}, method="POST")
    with urllib.request.urlopen(req2) as resp2:
        wav = resp2.read()
    return idx, len(wav)

t0 = time.time()
with ThreadPoolExecutor(max_workers=3) as executor:
    results = list(executor.map(synthesize_one, enumerate(sample_texts)))
elapsed = time.time() - t0

print(f"Synthesized 5 items in {elapsed:.2f}s ({elapsed/5:.2f}s per item)")
