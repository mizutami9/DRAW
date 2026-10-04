import json
import sys

sys.stdout.reconfigure(encoding='utf-8')

transcript_path = r'C:\Users\mizut\.gemini\antigravity-ide\brain\c32ff6cc-888a-4ceb-a6b2-c12aa29fe6ea\.system_generated\logs\transcript_full.jsonl'

planner_responses = []
with open(transcript_path, 'r', encoding='utf-8') as f:
    for line in f:
        if '"type":"PLANNER_RESPONSE"' in line:
            obj = json.loads(line)
            c = obj.get('content', '')
            if c:
                planner_responses.append(c)

print(f'Total responses: {len(planner_responses)}')
if planner_responses:
    print('Last response:')
    print(planner_responses[-1])
