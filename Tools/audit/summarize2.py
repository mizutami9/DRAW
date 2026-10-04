import json
d = json.load(open(r'c:\workSpace\PICO\Tools\audit\audit_raw2.json', encoding='utf-8'))
f = open(r'c:\workSpace\PICO\Tools\audit\summary2.txt', 'w', encoding='utf-8')
for k, u in d['youtube'].items():
    v = u.get('videos') or []
    print(f"YT {k} st={u.get('status')} subs={u.get('subs')} id={u.get('ytid')} x={u.get('x_links')}\n   " +
          ' || '.join(f"{x['date']} {x['title'][:40]}" for x in v[:4]), file=f)
for k, u in d['twitch'].items():
    if not u.get('exists'):
        print('TW', k, 'NOTFOUND', file=f)
