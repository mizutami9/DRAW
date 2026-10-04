import json
d = json.load(open(r'c:\workSpace\PICO\Tools\audit\audit_raw.json', encoding='utf-8'))
f = open(r'c:\workSpace\PICO\Tools\audit\summary.txt', 'w', encoding='utf-8')
for k, u in d['twitch'].items():
    if not u.get('exists'):
        print('TW', k, 'NOTFOUND/ERR', u.get('error'), file=f); continue
    soc = ','.join(f"{s['name']}={s['url']}" for s in (u['channel'] or {}).get('socialMedias') or [])
    lb = (u.get('lastBroadcast') or {}).get('startedAt')
    vids = ' || '.join(f"{v['createdAt'][:10]} {(v['game'] or {}).get('name')} {round((v['lengthSeconds'] or 0)/3600,1)}h {v['title'][:45]}" for v in u['videos'][:5])
    print(f"TW {k} fol={u['followers']['totalCount']} last={lb} soc=[{soc}]\n    {vids}", file=f)
for k, u in d['youtube'].items():
    v = d['youtube_videos'].get(k, {}).get('videos', [])
    print(f"YT {k} st={u.get('status')} subs={u.get('subs')} x={u.get('x_links')} err={u.get('error')}\n    " + ' || '.join(f"{x['published']} {x['title'][:45]}" for x in v[:4]), file=f)
