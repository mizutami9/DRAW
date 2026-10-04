import json
import sys

sys.stdout.reconfigure(encoding='utf-8')

f0 = r'C:\workSpace\PICO\Temp\DemoMultiplayerSmoke\20260929-095504\telemetry-client-0.jsonl'
f2 = r'C:\workSpace\PICO\Temp\DemoMultiplayerSmoke\20260929-095504\telemetry-client-2.jsonl'

def load(f):
    with open(f, 'r', encoding='utf-8') as fp:
        return [json.loads(line) for line in fp]

d0 = load(f0)
d2 = load(f2)

print('=== Client 0 Frames around t=11~17s ===')
for fr in d0:
    t = fr.get('elapsedSeconds', 0)
    if 11 <= t <= 17:
        pls = {p['slot']: (p['species'], round(p['position']['x'], 2), round(p['position']['y'], 2)) for p in fr.get('players', [])}
        print(f"t={t:.1f}s st={fr.get('stage')} act={fr.get('lastAction')}")
        print('  Players:', pls)

print('\n=== Client 2 Frames around t=11~17s ===')
for fr in d2:
    t = fr.get('elapsedSeconds', 0)
    if 11 <= t <= 17:
        pls = {p['slot']: (p['species'], round(p['position']['x'], 2), round(p['position']['y'], 2)) for p in fr.get('players', [])}
        print(f"t={t:.1f}s st={fr.get('stage')} act={fr.get('lastAction')}")
        print('  Players:', pls)
