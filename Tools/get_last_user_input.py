import json

transcript_path = r'C:\Users\mizut\.gemini\antigravity-ide\brain\c32ff6cc-888a-4ceb-a6b2-c12aa29fe6ea\.system_generated\logs\transcript_full.jsonl'

inputs = []
with open(transcript_path, 'r', encoding='utf-8') as f:
    for line in f:
        if '"type":"USER_INPUT"' in line:
            obj = json.loads(line)
            c = obj.get('content', '')
            req = c.split('<USER_REQUEST>')[1].split('</USER_REQUEST>')[0].strip() if '<USER_REQUEST>' in c else c
            if req:
                inputs.append(req)

print(f"Total non-empty requests: {len(inputs)}")
for i, req in enumerate(inputs[-15:]):
    print(f"--- {i} ---")
    print(req[:300])
