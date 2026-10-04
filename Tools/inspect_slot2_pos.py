import json
import sys

sys.stdout.reconfigure(encoding='utf-8')

f0 = r'C:\workSpace\PICO\Temp\DemoMultiplayerSmoke\20260929-095556\telemetry-client-0.jsonl'
f2 = r'C:\workSpace\PICO\Temp\DemoMultiplayerSmoke\20260929-095556\telemetry-client-2.jsonl'

with open(f0, 'r', encoding='utf-8') as fp:
    d0 = [json.loads(line) for line in fp]
with open(f2, 'r', encoding='utf-8') as fp:
    d2 = [json.loads(line) for line in fp]

print('=== Slot 2 from Client 0 (Host) ===')
for fr in d0:
    t = fr.get('elapsedSeconds', 0)
    if 12 <= t <= 18:
        for p in fr.get('players', []):
            if p['slot'] == 2:
                print(f"t={t:.1f}s pos=({p['position']['x']:.2f}, {p['position']['y']:.2f}) vel=({p['velocity']['x']:.2f}, {p['velocity']['y']:.2f}) gr={p['grounded']} act={fr.get('lastAction')}")

print('\n=== Slot 2 from Client 2 (Owner) ===')
for fr in d2:
    t = fr.get('elapsedSeconds', 0)
    if 12 <= t <= 18:
        for p in fr.get('players', []):
            if p['slot'] == 2:
                print(f"t={t:.1f}s pos=({p['position']['x']:.2f}, {p['position']['y']:.2f}) vel=({p['velocity']['x']:.2f}, {p['velocity']['y']:.2f}) gr={p['grounded']} act={fr.get('lastAction')}")
