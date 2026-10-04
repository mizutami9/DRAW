import json

transcript_path = r'C:\Users\mizut\.gemini\antigravity-ide\brain\c32ff6cc-888a-4ceb-a6b2-c12aa29fe6ea\.system_generated\logs\transcript_full.jsonl'

with open(transcript_path, 'r', encoding='utf-8') as f:
    for line in f:
        if 'ffmpeg' in line.lower():
            obj = json.loads(line)
            calls = obj.get('tool_calls', [])
            for c in calls:
                if 'ffmpeg' in str(c).lower():
                    print("Tool call:", c)
            out = obj.get('content', '')
            if 'ffmpeg' in out.lower() and obj.get('type') == 'PLANNER_RESPONSE':
                print("Planner snippet:", out[:300])
