import re, json, sys
raw = json.load(open(r'c:\workSpace\PICO\Tools\audit\audit_raw2.json', encoding='utf-8'))
KWF = re.compile(r"(w/|with friends|collab|friends|goobers|squad|duo|trio|gorls|siblings|peeps|friendos|amiwas|コラボ|ft\.|feat|@\w{3,}|avec)", re.I)
EX = re.compile(r'(gang|goobers|crew|team|lobby|guys|peeps|frien|collab|コラボ|みんな|協力|友)', re.I)
for login in ['v4_hawthorn', 'veepai']:
    for v in raw['twitch'][login]['videos'][:40]:
        t = v['title']
        g = (v['game'] or {}).get('name')
        if g in ('PEAK', 'HELLDIVERS 2', 'Waterpark Simulator'):
            print(login, g, repr(t), bool(KWF.search(t)), bool(EX.search(t.lower())))
