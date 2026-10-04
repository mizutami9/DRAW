import re, json, datetime, collections, copy
import openpyxl
from openpyxl.styles import Font, PatternFill, Alignment, Border, Side
from openpyxl.utils import get_column_letter

SRC = r'C:\Users\mizut\Downloads\NICO_DRAW_creator_outreach_group_leaders_v200.xlsx'
RAW = r'c:\workSpace\PICO\Tools\audit\audit_raw2.json'
OUT = r'C:\Users\mizut\Downloads\NICO_DRAW_creator_outreach_FINAL.xlsx'
TODAY = datetime.date(2026, 10, 3)
TODAY_S = TODAY.isoformat()
raw = json.load(open(RAW, encoding='utf-8'))
TW, YT = raw['twitch'], raw['youtube']
ytn = {k.lower().rstrip('/'): v for k, v in YT.items()}

wb0 = openpyxl.load_workbook(SRC)
ws_main, ws_mem, ws_exc, ws_dash, ws_tpl = wb0.worksheets[:5]
hdr = [c.value for c in ws_main[1]]
rows = [dict(zip(hdr, r)) for r in ws_main.iter_rows(min_row=2, values_only=True)]
mem_rows = [r for r in ws_mem.iter_rows(min_row=2, values_only=True)]
exc_rows = [r for r in ws_exc.iter_rows(min_row=2, values_only=True)]

# ---------- 判定用定義 ----------
STRICT = ['pico park', 'meccha chameleon', 'overcooked', 'chained together', 'heave ho', 'big walk',
          'party animals', 'fall guys', 'gamble with your friends', 'bombanana', 'we gotta go', 'baby kings',
          'human: fall flat', 'pratfall', 'rv there yet', 'pogostuck']
SOFT = ['peak', 'r.e.p.o.', 'lethal company', 'phasmophobia', 'helldivers', 'escape simulator', 'escape the backrooms',
        'content warning', 'ghosted', 'dread flats', 'storebound', 'left 4 dead', 'ready or not', 'funnel runners',
        'we were here', 'waterpark simulator', 'the choicer voicer', 'dont drop the cake', "don't drop the cake", 'pratfall']
NEAR = ['pico park', 'meccha chameleon', 'overcooked', 'chained together', 'heave ho', 'big walk', 'peak', 'r.e.p.o.',
        'human: fall flat', 'bombanana', 'pratfall', 'party animals', 'escape simulator', 'escape the backrooms',
        'lethal company', 'we gotta go', 'baby kings', 'fall guys', 'rv there yet', 'content warning', 'ghosted']
KW = re.compile(r"(w/|with friends|w/ friends|collab|co-?op|friends|goobers|squad|duo|trio|gorls|siblings|peeps|friendos|amiwas|amigos|"
                r"コラボ|協力|みんなで|鼎談|参加|ft\.|feat|@\w{3,})", re.I)
KW_FRIEND = re.compile(r"(w/|with friends|collab|friends|goobers|squad|duo|trio|gorls|siblings|peeps|friendos|amiwas|コラボ|ft\.|feat|@\w{3,}|avec)", re.I)
SENT = ['lominoth', 'oniheart', 'lotus', 'kino', 'mizukokanagawa', 'cure', 'キュレ', 'tomobean', 'minitomo69']


def handle_of(url):
    m = re.search(r'(?:x|twitter)\.com/([A-Za-z0-9_]+)', str(url or ''), re.I)
    return m.group(1).lower() if m else None


def tw_login(s):
    ms = re.findall(r'twitch\.tv/([A-Za-z0-9_]+)', str(s or ''))
    return [m.lower() for m in ms if m.lower() not in ('subs', 'videos')]


def yt_urls(s):
    return re.findall(r'(https?://(?:www\.)?youtube\.com/(?:@[^\s;/?]+|channel/[\w-]+))', str(s or ''))


def parse_num(s):
    if s is None:
        return None
    s = str(s).replace(',', '')
    m = re.match(r'([\d.]+)([KMB]?)', s)
    if not m:
        return None
    v = float(m.group(1)) * {'': 1, 'K': 1e3, 'M': 1e6, 'B': 1e9}[m.group(2)]
    return int(v)


def detect_lang(titles, default):
    t = ' '.join(titles)
    letters = re.findall(r'[A-Za-z\u3040-\u30ff\u3400-\u9fff\u0e00-\u0e7f\uac00-\ud7af]', t)
    if len(letters) < 12:
        return default, None
    kana = sum(1 for c in letters if '\u3040' <= c <= '\u30ff') / len(letters)
    han = sum(1 for c in letters if '\u3400' <= c <= '\u9fff') / len(letters)
    thai = sum(1 for c in letters if '\u0e00' <= c <= '\u0e7f') / len(letters)
    hang = sum(1 for c in letters if '\uac00' <= c <= '\ud7af') / len(letters)
    det = None
    if thai > .3: det = 'タイ語'
    elif hang > .3: det = '韓国語'
    elif kana > .12: det = '日本語'
    elif han > .3: det = '繁体字中国語'
    if det and det != default:
        return det, f'配信/動画タイトルは{det}中心のため旧記載({default})から修正'
    return default, None


def analyze(c):
    """c: dict(name, group, x_claim, tw[], yt[], lang, region, old_ev, src, old_no)"""
    r = dict(c)
    tws = [TW[l] for l in c['tw'] if l in TW]
    twd = next((t for t in tws if t.get('exists')), None)
    ytd = [ytn.get(u.lower().rstrip('/')) for u in c['yt']]
    ytd = [y for y in ytd if y and y.get('status') == 200]
    r['twd'], r['ytd'] = twd, ytd
    notes = []
    # 存在
    r['exists'] = bool(twd or ytd)
    # X本人確認
    linked = collections.OrderedDict()
    if twd:
        for s in (twd.get('channel') or {}).get('socialMedias') or []:
            h = handle_of(s['url'])
            if h: linked[h] = 'Twitchプロフィール'
    for y in ytd:
        for x in y.get('x_links') or []:
            h = handle_of(x)
            if h: linked.setdefault(h, 'YouTubeチャンネル')
    claim = handle_of(c['x_claim'])
    xurl, xstat = None, '未確認'
    if claim and claim in linked:
        xurl, xstat = f'https://x.com/{claim}', f'確認済({linked[claim]}からリンク)'
    elif linked:
        base = (c['tw'][0] if c['tw'] else (re.search(r'@([^/]+)', c['yt'][0]).group(1).lower() if c['yt'] and '@' in c['yt'][0] else ''))
        cands = list(linked)
        pick = None
        if len(cands) == 1 and (twd or len(ytd) == 1) and (not twd or len(ytd) <= 1):
            pick = cands[0]
        else:
            for h in cands:
                if h.replace('_', '') == base.replace('_', '') or h.replace('_', '') == base.replace('_', '').rstrip('vt'):
                    pick = h
        if pick and (twd or len(ytd) == 1):
            xurl, xstat = f'https://x.com/{pick}', f'確認済({linked[pick]}からリンク)'
            if claim and claim != pick:
                notes.append(f'旧記載X @{claim} はリンク元と不一致のため不採用、本人リンクの @{pick} に修正')
        else:
            notes.append('本人チャンネルから複数のXがリンク(' + ', '.join('@' + h for h in cands[:4]) + ')。どれが本人か断定不可')
            if claim: notes.append(f'旧記載X @{claim} は未確認')
    else:
        if claim: notes.append(f'旧記載X @{claim} は本人チャンネルからのリンクが確認できず未採用')
    r['x_url'], r['x_stat'] = xurl, xstat
    # 活動
    dates = []
    if twd and (twd.get('lastBroadcast') or {}).get('startedAt'):
        dates.append(twd['lastBroadcast']['startedAt'][:10])
    for y in ytd:
        if y.get('videos'): dates.append(y['videos'][0]['date'])
    last = max(dates) if dates else None
    r['last'] = last
    r['days'] = (TODAY - datetime.date.fromisoformat(last)).days if last else None
    # 動画一覧
    items = []
    if twd:
        for v in twd['videos']:
            items.append(dict(date=v['createdAt'][:10], game=(v['game'] or {}).get('name') or '', title=v['title'],
                              src='Twitch', url=f"https://www.twitch.tv/{twd['login']}/videos", h=(v['lengthSeconds'] or 0) / 3600))
    for y in ytd:
        for v in y.get('videos') or []:
            items.append(dict(date=v['date'], game='', title=v['title'], src='YouTube', url=v['url'], h=0))
    multi = []
    for it in items:
        g = it['game'].lower(); t = it['title'].lower()
        hay = g + ' ' + t
        strict = any(s in hay for s in STRICT) and g != 'just chatting'
        soft = any(s in hay for s in SOFT) and bool(KW_FRIEND.search(it['title']) or re.search(r'(gang|goobers|crew|team|lobby|guys|peeps|frien|collab|コラボ|みんな|協力|友)', t))
        friend = bool(KW_FRIEND.search(it['title']))
        viewer = '参加' in it['title'] or 'participat' in t or 'viewer' in t or 'スナイプ' in it['title'] or 'open lobby' in t
        if strict or soft:
            it['near'] = any(s in hay for s in NEAR)
            it['viewer'] = viewer and not friend
            multi.append(it)
    r['multi'] = multi
    r['n_multi'] = len({(m['date'], m['title']) for m in multi})
    r['n_near'] = len({(m['date'], m['title']) for m in multi if m['near'] and not m['viewer']})
    r['n_viewer'] = len({(m['date'], m['title']) for m in multi if m['viewer']})
    r['n_friend'] = len({(m['date'], m['title']) for m in multi if not m['viewer']})
    # 規模
    fol = twd['followers']['totalCount'] if twd and twd.get('followers') else None
    subs = None
    for y in ytd:
        n = parse_num(y.get('subs'))
        if n is not None: subs = max(subs or 0, n)
    r['fol'], r['subs'] = fol, subs
    size = max([n for n in (fol, subs) if n is not None], default=None)
    r['size'] = size
    r['sizeb'] = '不明' if size is None else '小(〜1,000)' if size < 1000 else '小中(1,000〜4,999)' if size < 5000 else '中(5,000〜9,999)' if size < 10000 else '大(10,000〜)'
    titles = [i['title'] for i in items]
    r['lang'], ln = detect_lang(titles, c['lang'])
    if ln: notes.append(ln)
    # 連絡経路
    ch = []
    if xurl: ch.append('X DM(送信可否は未確認)')
    socs = [(s['name'], s['url']) for s in ((twd or {}).get('channel') or {}).get('socialMedias') or []]
    if any(n.lower() == 'discord' or 'discord' in u for n, u in socs): ch.append('Discord(公開招待あり)')
    if any('bsky' in n.lower() or 'bsky' in u for n, u in socs): ch.append('Bluesky')
    if any(k in (u + n).lower() for n, u in socs for k in ('carrd', 'linktr', 'beacons', 'allmylinks')): ch.append('リンク集あり')
    if twd: ch.append('Twitch')
    if ytd: ch.append('YouTube')
    r['contact'] = ' / '.join(ch) if ch else '未確認'
    r['has_contact'] = bool(xurl or any('Discord' in x or 'Bluesky' in x or 'リンク集' in x for x in ch))
    r['soc'] = socs
    r['notes'] = notes
    # 送付済み/別名義チェック
    key = ' '.join([c['name'], c['group'] or '', ' '.join(c['tw']), str(c['x_claim'])]).lower()
    soc_text = ' '.join(u for _, u in socs).lower()
    r['sent'] = next((s for s in SENT if s in key), None) or ('mizukokanagawa' if 'mizukokanagawa' in soc_text else None)
    # ---- 分類 ----
    reasons = []
    cls = None
    if r['sent']:
        cls = '除外'; reasons.append(f'送付済み/別名義の疑い({r["sent"]}): 旧ファイルの送付済み一覧と一致。同一人物へ二重DMしないため除外')
    elif not r['exists']:
        cls = '除外'; reasons.append('Twitch/YouTubeの公開ページを取得できず本人確認不可(アカウント名変更・削除の可能性)')
    elif r['days'] is None:
        cls = '保留' if (twd and twd.get('followers')) else '除外'
        reasons.append('配信/動画の最終活動日を確認できない')
        if cls == '除外': reasons[-1] += '(フォロワー極少・VOD無し)'
    elif r['days'] > 60:
        cls = '除外'; reasons.append(f'最終活動{last}（{r["days"]}日前）。現在ほぼ活動していない')
    elif size is not None and size >= 10000:
        cls = '除外'; reasons.append(f'規模が大きい({size:,})。小規模インディー営業の優先対象外')
    else:
        active = r['days'] <= 30
        if r['days'] > 30:
            reasons.append(f'最終活動{last}（{r["days"]}日前）で直近1か月は活動なし')
        if r['n_multi'] == 0:
            cls = '保留' if r['days'] <= 60 else '除外'
            reasons.append('直近の公開VOD/動画40件内に複数人プレイを確認できず(旧記載の実績は再確認不可)')
        elif not r['has_contact']:
            cls = '保留'; reasons.append('本人確認済みXも公開Discord/Blueskyも無く連絡経路が不明確')
        elif active and r['n_near'] >= 3 and xurl and (size is None or size < 6000):
            cls = 'A'
        elif active and (r['n_friend'] >= 1 or r['n_viewer'] >= 2) and (size is None or size < 10000):
            cls = 'B'
        else:
            cls = '保留'
            if r['n_friend'] == 0: reasons.append('複数人プレイは確認できるが視聴者参加型が1件のみで、友人・共演者とのプレイ頻度は不明')
    r['cls'] = cls
    r['reasons'] = reasons
    return r


# ---------- 候補生成 ----------
cands = []
for r in rows:
    cands.append(dict(name=r['代表窓口（候補名）'], group=r['営業グループ名'], x_claim=r['X URL'],
                      tw=tw_login(r['配信先（YouTube / Twitch）']), yt=yt_urls(r['配信先（YouTube / Twitch）']),
                      lang=r['営業言語'], region=r['国・地域'], old_ev=r['複数人実況の根拠・実績'], old_games=r['確認ゲーム'],
                      src=f"v200 No.{r['No.']}（{r['区分']}）", old_no=r['No.'], members=r['共演者・グループメンバー'],
                      ev_urls=r['証拠URL・情報ソース'], old_contact=r['連絡方法'], kind='main'))
# 統合メンバー(誤統合の可能性があるため個別に再監査)
for m in mem_rows:
    if not m[0]: continue
    cands.append(dict(name=m[3], group=m[2], x_claim=m[4], tw=tw_login(m[5]), yt=yt_urls(m[5]), lang=None, region=None,
                      old_ev=f'v200で「{m[1]}」に統合されていたメンバー', old_games='', src=f'v200 統合メンバー(旧{m[1]}配下)',
                      old_no=None, members=None, ev_urls=m[5], old_contact='', kind='member', leader=m[1]))
lang_by_leader = {c['name']: (c['lang'], c['region']) for c in cands if c['kind'] == 'main'}
for c in cands:
    if c['kind'] == 'member':
        c['lang'], c['region'] = lang_by_leader.get(c['leader'], ('英語', ''))
res = [analyze(c) for c in cands]
REG_BY_LANG = {'日本語': '日本', 'タイ語': 'タイ', '繁体字中国語': '台湾', '韓国語': '韓国'}
REG_NORM = {'EN': '英語圏', 'US': 'アメリカ', 'US/EN': 'アメリカ', '英語 / フランス語圏': '英語/フランス語圏', '日本 / インドネシア': '日本', '台湾 / 繁体字圏': '台湾', 'ドイツ/スイス': 'ドイツ'}
for r, c in zip(res, cands):
    reg = REG_NORM.get(str(r['region']), r['region'])
    if r['lang'] != c['lang'] and r['lang'] in REG_BY_LANG:
        reg = REG_BY_LANG[r['lang']]
    r['region'] = reg
for r in res:
    if r['kind'] == 'member' and not r['tw'] and not r['yt'] and not r['x_claim']:
        r['cls'] = '保留'; r['reasons'] = ['v200で統合されていたがURL・本人情報が一切なく未調査(調査材料なし)。統合解除により個別候補として保留']
    elif r['kind'] == 'member' and not r['tw'] and not r['yt']:
        r['cls'] = '保留'; r['reasons'] = ['配信URLなし・Xのみ記載で本人確認不可']

# ---------- 重複チェック ----------
dups = []
seen = {}
for r in res:
    if r['cls'] in ('A', 'B'):
        keys = []
        if r['x_url']: keys.append(('X', r['x_url'].lower()))
        for l in r['tw']: keys.append(('Twitch', l))
        for u in r['yt']: keys.append(('YouTube', u.lower().rstrip('/')))
        for k in keys:
            if k in seen and seen[k] is not r:
                r['cls'] = '除外'; r['reasons'].append(f'重複: {k[0]} {k[1]} が {seen[k]["name"]} と同一'); dups.append((r['name'], seen[k]['name'], k))
                break
            seen[k] = r
# 1つの旧統合から複数メンバーが最終入りした場合の二重DM防止はグループ監査で処理

# ---------- グループ統合監査 ----------
groups = collections.OrderedDict()
for r in res:
    if r['kind'] == 'main' and r['members'] and str(r['members']).strip() and '配信時に都度招集' not in str(r['members']):
        groups[r['name']] = r
GROUP_PROOF = {
    'Ace Okami': ('グループ名VDachiが実在: 本人Twitchプロフィールにvdachiteam.carrd.coを掲載', 'https://vdachiteam.carrd.co/', 'https://www.twitch.tv/aceokami'),
    'Bebe Duck': ('グループ名DayJobÜが実在: Bebe Duck/SralZoren両YouTubeチャンネルが同一X(@dayjobuTV)をリンク', 'https://twitter.com/dayjobuTV', 'https://www.youtube.com/@bebeduckVT'),
}
audit_rows = []
by_name = {r['name']: r for r in res}
for gname, lead in groups.items():
    mem = [m for m in mem_rows if m[0] and m[1] == gname]
    mnames = [m[3] for m in mem]
    # 直近VODタイトルでの共演言及回数
    handles = set()
    for m in mem:
        h = handle_of(m[4]);
        if h: handles.add(h)
        handles |= set(tw_login(m[5]))
    cnt_dates, cnt_games = set(), set()
    for it in lead['multi']:
        tl = it['title'].lower()
        if any(('@' + h) in tl or h in tl for h in handles) or any(n.lower() in tl for n in mnames if len(n) > 3):
            cnt_dates.add(it['date']); cnt_games.add(it['game'])
    proof = GROUP_PROOF.get(gname)
    keep = bool(proof) or (len(cnt_dates) >= 3 and len(cnt_games) >= 2)
    if proof:
        kind = '固定/準固定(グループ名あり)'; reason = proof[0] + '。ただし代表者である根拠は無いため「営業窓口候補」として扱う。協力ゲームの反復プレイ自体は直近VODで未確認'
        e = [proof[1], proof[2], lead['ev_urls'] and str(lead['ev_urls']).split(' ; ')[0]]
    else:
        kind = '単発/不明(固定の根拠なし)'
        reason = ('旧ファイルの根拠は「' + str(lead['old_ev']) + '」(単発の大型コラボ/配信告知)のみ。グループ名・反復共演・本人による固定メンバー表現を公開情報で確認できず、'
                  f'直近VOD内の共演言及は{len(cnt_dates)}日/{len(cnt_games)}ゲーム。→統合を解除し各自を個別の営業先として再評価')
        e = (str(lead['ev_urls']).split(' ; ') + ['', '', ''])[:3]
    e = (e + ['', '', ''])[:3]
    lead['group_keep'] = keep
    audit_rows.append([lead['group'], lead['name'] + ('（窓口候補・代表者の根拠なし）' if keep else '（統合解除により個別扱い）'), ' / '.join(mnames),
                       kind, f'直近VOD内で確認できた共演: {len(cnt_dates)}回', lead['old_games'], e[0], e[1], e[2],
                       '統合維持' if keep else '統合解除', reason])
# 解除グループの元メンバーを備考へ / 維持グループは同一グループ内で最大1DMに
for r in res:
    if r['kind'] == 'member':
        g = groups.get(r['leader'])
        r['group_keep'] = bool(g and g.get('group_keep'))
keep_groups = {g for g, l in groups.items() if l.get('group_keep')}
# 維持グループは代表窓口のみ営業対象(メンバーは重複防止で保留に)
for r in res:
    if r['kind'] == 'member' and r['group_keep'] and r['cls'] in ('A', 'B'):
        r['cls'] = '保留'; r['reasons'].append('統合維持グループの一員。同じグループへ複数DMを送らないため窓口候補1名のみ営業')

# ---------- 営業言語・順序 ----------
order = {'A': 0, 'B': 1}
final = [r for r in res if r['cls'] in ('A', 'B')]
final.sort(key=lambda r: (order[r['cls']], r['lang'], -(r['n_friend'] + r['n_near'])))
hold = [r for r in res if r['cls'] == '保留']
excl = [r for r in res if r['cls'] == '除外']

json.dump({'final': [r['name'] for r in final]}, open(r'c:\workSpace\PICO\Tools\audit\final_names.json', 'w', encoding='utf-8'), ensure_ascii=False)

# ---------- Excel出力 ----------
HEAD_FILL = PatternFill('solid', fgColor='2F3B52'); HEAD_FONT = Font(bold=True, color='FFFFFF', name='Yu Gothic')
thin = Side(style='thin', color='CCCCCC'); BORDER = Border(left=thin, right=thin, top=thin, bottom=thin)
FILL_A = PatternFill('solid', fgColor='FFF2CC'); FILL_B = PatternFill('solid', fgColor='E2EFDA'); FILL_C = PatternFill('solid', fgColor='DDEBF7')
FILL_X = PatternFill('solid', fgColor='F2DCDB')


def write(ws, headers, data, widths, fills=None):
    ws.append(headers)
    for c in ws[1]:
        c.fill = HEAD_FILL; c.font = HEAD_FONT; c.alignment = Alignment(wrap_text=True, vertical='center'); c.border = BORDER
    for i, row in enumerate(data, start=2):
        ws.append(row)
        for c in ws[i]:
            c.alignment = Alignment(wrap_text=True, vertical='top'); c.border = BORDER
            if fills and fills[i - 2]: c.fill = fills[i - 2]
    for i, w in enumerate(widths, start=1):
        ws.column_dimensions[get_column_letter(i)].width = w
    ws.freeze_panes = 'D2'
    ws.auto_filter.ref = ws.dimensions


def tw_url(r): return f"https://www.twitch.tv/{r['twd']['login']}" if r['twd'] else '未確認'
def yt_url(r): return r['ytd'][0]['url'] if r['ytd'] else '未確認'


def evidence(r):
    ms = sorted(r['multi'], key=lambda m: m['date'], reverse=True)[:4]
    if not ms: return '直近公開VOD/動画に該当なし(旧記載: ' + str(r['old_ev']) + ' ← 再確認不可)'
    return ' / '.join(f"{m['date']} {m['game'] or m['src']}『{m['title'][:40]}』" for m in ms)


def ev_urls(r):
    urls = []
    ms = sorted(r['multi'], key=lambda m: m['date'], reverse=True)
    for m in ms:
        if m['url'] not in urls: urls.append(m['url'])
        if len(urls) >= 3: break
    if r['x_url'] and len(urls) < 4: urls.append(r['x_url'])
    return '\n'.join(urls) if urls else '未確認'


def games(r):
    g = collections.Counter(m['game'] for m in r['multi'] if m['game'])
    return ' / '.join(k for k, _ in g.most_common(4)) or ('(タイトルから判断) ' + str(r['old_games'] or '未確認'))


def partners(r):
    ps = set()
    for m in r['multi']:
        for h in re.findall(r'@\w{3,}', m['title']): ps.add(h)
    return ' '.join(sorted(ps)[:6]) or '特定できず(都度コラボ/不特定)'


def kind(r):
    if r['kind'] == 'main' and r.get('group_keep'): return '固定/準固定(グループ名あり)'
    if r['n_friend'] >= 3: return '準固定の可能性(複数回の共演を確認・固定とは断定せず)'
    if r['n_friend'] >= 1: return '単発〜不定期'
    return '未確認'


def fit(r):
    bits = []
    near = collections.Counter(m['game'] for m in r['multi'] if m['near'] and m['game'])
    if near: bits.append('直近で' + '・'.join(list(near)[:3]) + 'など協力/複数人ゲームを配信')
    elif r['n_multi']: bits.append('直近で複数人プレイ配信を確認')
    if r['n_friend']: bits.append(f"友人/共演者とのプレイ示唆を{r['n_friend']}件確認")
    if r['size'] is not None: bits.append(f"規模{r['size']:,}で小〜中規模")
    return '。'.join(bits) or '要確認'


FINAL_H = ['No.', '優先度', '営業先名', 'グループ名', '営業単位', '営業言語', '地域', 'X URL(本人確認済のみ)', 'Xフォロワー', 'YouTube URL', 'YouTube登録者',
           'Twitch URL', 'Twitchフォロワー', '主な連絡方法', 'DM可否', '公開メール', '複数人プレイ確認', '一緒に遊ぶ相手', '固定/準固定/単発', '確認ゲーム',
           '根拠', '証拠URL', '最終活動日', '確認日', 'NICO DRAW適合理由', 'DM送信済', '返信', 'デモプレイ', '配信/動画化', '製品版キー予定人数', '備考', 'X本人確認', '規模区分']
frows, ffills = [], []
for i, r in enumerate(final, 1):
    unit = '個人(営業窓口候補)' if not r.get('group_keep') else f"グループ「{r['group']}」(窓口候補・代表者の根拠なし)"
    gname = r['group'] if r.get('group_keep') else '(固定グループなし・個人単位)'
    mp = f"確認({r['n_multi']}件: 友人/共演系{r['n_friend']}件・視聴者参加型{r['n_viewer']}件)"
    note = '; '.join(r['notes'] + r['reasons'] + ([f"出所: {r['src']}"]))
    frows.append([i, r['cls'], r['name'], gname, unit, r['lang'], r['region'], r['x_url'] or '未確認', '未確認(Xは非ログインで取得不可)',
                  yt_url(r), (f"{r['ytd'][0].get('subs')}人" if r['ytd'] and r['ytd'][0].get('subs') else '未確認') if r['ytd'] else '-',
                  tw_url(r), r['fol'] if r['fol'] is not None else '未確認', r['contact'],
                  'X DMを試行(開放可否は未確認)' if r['x_url'] else '未確認(代替: 上記連絡方法)', '未確認', mp, partners(r), kind(r), games(r),
                  evidence(r), ev_urls(r), r['last'] or '未確認', TODAY_S, fit(r), '', '', '', '', '', note,
                  r['x_stat'], r['sizeb']])
    ffills.append(FILL_A if r['cls'] == 'A' else FILL_B)

wb = openpyxl.Workbook()
ws = wb.active; ws.title = '最終営業リスト'
write(ws, FINAL_H, frows, [5, 7, 20, 22, 22, 11, 12, 30, 14, 30, 12, 30, 12, 28, 22, 10, 28, 24, 24, 28, 50, 50, 12, 11, 40, 9, 8, 9, 9, 10, 50, 22, 16], ffills)
ws.freeze_panes = 'D2'

ws = wb.create_sheet('グループ統合監査')
write(ws, ['グループ名', '代表窓口', '統合メンバー', '固定/準固定/単発', '繰り返し共演回数', '確認したゲーム', '証拠URL1', '証拠URL2', '証拠URL3', '統合維持/解除', '判断理由'],
      audit_rows, [26, 28, 40, 22, 22, 24, 40, 40, 40, 12, 80], [FILL_B if a[9] == '統合維持' else FILL_X for a in audit_rows])
ws.freeze_panes = 'B2'


def listrows(lst):
    out, fl = [], []
    for r in lst:
        out.append([r['src'], r['name'], r['group'] if r['kind'] == 'main' else f"(旧統合: {r['group']})", r['cls'], ' / '.join(r['reasons']) or '-',
                    r['lang'], r['region'], r['x_url'] or '未確認', tw_url(r), yt_url(r), r['last'] or '未確認', r['fol'] if r['fol'] is not None else '',
                    r['n_multi'], r['n_friend'], '; '.join(r['notes']), TODAY_S])
        fl.append(FILL_C if r['cls'] == '保留' else FILL_X)
    return out, fl


LH = ['出所', '候補名', '旧グループ名', '分類', '理由', '営業言語', '地域', 'X URL(確認済のみ)', 'Twitch', 'YouTube', '最終活動日', 'Twitchフォロワー',
      '複数人プレイ確認数', '友人/共演系確認数', '補足', '監査日']
LW = [22, 24, 24, 8, 60, 10, 12, 30, 34, 34, 12, 12, 10, 10, 50, 11]
d, f = listrows(hold)
ws = wb.create_sheet('保留'); write(ws, LH, d, LW, f)
d, f = listrows(excl)
# 旧版の除外一覧を引き継ぐ
legacy = []
for e in exc_rows:
    if e[2] == '除外' and e[1] and e[1] not in ('再監査日', '2026-10-03'):
        legacy.append(['v200 除外シート', e[1], '-', '除外', e[4], '', '', '', '', '', '', '', '', '', '旧版の除外理由を継承(後で戻せるよう保存)', TODAY_S])
for e in exc_rows:
    if e[1] == '2026-10-03':
        nm = str(e[4]).rsplit(' - ', 1)
        legacy.append(['v200 除外シート(重複削除済)', nm[0], '-', '除外', '旧版で重複削除済み(旧No.' + (nm[1] if len(nm) > 1 else '?') + ')', '', '', '', '', '', '', '', '', '', '旧版の除外理由を継承', TODAY_S])
ws = wb.create_sheet('除外'); write(ws, LH, d + legacy, LW, f + [FILL_X] * len(legacy))

# 全件監査ログ
ws = wb.create_sheet('全件監査ログ')
log = []
for r in res:
    log.append([r['src'], r['name'], r['group'] or '', r['cls'], r['x_stat'], r['x_url'] or '', r['last'] or '', r['days'] if r['days'] is not None else '',
                r['fol'] if r['fol'] is not None else '', r['subs'] if r['subs'] is not None else '', r['n_multi'], r['n_near'], r['n_friend'], r['n_viewer'],
                r['lang'], r['contact'], ' / '.join(r['reasons']), '; '.join(r['notes']), evidence(r)])
write(ws, ['出所', '候補名', 'グループ名', '分類', 'X本人確認', 'X URL', '最終活動日', '経過日数', 'Twitchフォロワー', 'YouTube登録者(数値)', '複数人プレイ確認数',
           '協力ゲーム系', '友人/共演系', '視聴者参加型', '営業言語', '連絡経路', '理由', '補足', '根拠'], log,
      [24, 24, 24, 8, 26, 30, 12, 8, 12, 12, 10, 8, 8, 8, 10, 30, 60, 50, 80])

# DMテンプレ(原本シートをそのままコピー)
wt = wb.create_sheet('DMテンプレ')
for row in ws_tpl.iter_rows():
    for c in row:
        n = wt.cell(row=c.row, column=c.column, value=c.value)
        if c.has_style:
            n.font = copy.copy(c.font); n.fill = copy.copy(c.fill); n.alignment = copy.copy(c.alignment); n.border = copy.copy(c.border)
for k, dim in ws_tpl.column_dimensions.items():
    wt.column_dimensions[k].width = dim.width
for k, dim in ws_tpl.row_dimensions.items():
    if dim.height: wt.row_dimensions[k].height = dim.height

# 集計(数式)
wd = wb.create_sheet('集計', 0 + 1)
FL = "'最終営業リスト'"
n = len(final)
lines = [
    ['NICO DRAW 営業リスト FINAL 集計', '', ''],
    ['作成日', TODAY_S, '公開情報(Twitch GQL / YouTubeページ・RSS)を2026-10-03に再取得'],
    ['', '', ''],
    ['項目', '件数', '備考'],
    ['最終営業対象数(A+B)', f"=COUNTA({FL}!C2:C{n+1})", ''],
    ['A:最優先', f'=COUNTIF({FL}!B2:B{n+1},"A")', ''],
    ['B:営業対象', f'=COUNTIF({FL}!B2:B{n+1},"B")', ''],
    ['C:保留(保留シート)', len(hold), '旧統合メンバーの個別化分を含む'],
    ['除外(除外シート)', len(excl) + len(legacy), f'今回判定{len(excl)} + 旧版から継承{len(legacy)}'],
    ['独立営業グループ数(最終リスト=重複なし営業先)', f"=COUNTA({FL}!C2:C{n+1})", ''],
    ['統合維持グループ数', len(keep_groups), ', '.join(keep_groups)],
    ['統合解除グループ数', len(groups) - len(keep_groups), ''],
    ['重複削除数(今回)', len(dups), '; '.join(f'{a}=={b}' for a, b, _ in dups) or '最終候補内の同一人物重複は検出されず'],
    ['本人確認済みX数(最終リスト)', f'=COUNTIF({FL}!H2:H{n+1},"https://x.com/*")', ''],
    ['X DM試行可能数', f'=COUNTIF({FL}!O2:O{n+1},"X DM*")', 'DM開放可否は実送信するまで未確認'],
    ['公開メール確認数', f'=COUNTIF({FL}!P2:P{n+1},"?*")-COUNTIF({FL}!P2:P{n+1},"未確認")', 'メール営業は今回の自動調査では確認できず0'],
    ['', '', ''],
    ['営業言語', '件数', ''],
]
for lang in sorted({r['lang'] for r in final}):
    lines.append([lang, f'=COUNTIF({FL}!F2:F{n+1},"{lang}")', ''])
lines += [['', '', ''], ['地域', '件数', '']]
for reg in sorted({str(r['region']) for r in final}):
    lines.append([reg, f'=COUNTIF({FL}!G2:G{n+1},"{reg}")', ''])
lines += [['', '', ''], ['規模区分', '件数', '']]
for s in ['小(〜1,000)', '小中(1,000〜4,999)', '中(5,000〜9,999)', '不明']:
    lines.append([s, f'=COUNTIF({FL}!AG2:AG{n+1},"{s}")', ''])
for l in lines: wd.append(l)
wd['A1'].font = Font(bold=True, size=14)
for rr in (4,):
    for c in wd[rr]: c.fill = HEAD_FILL; c.font = HEAD_FONT
wd.column_dimensions['A'].width = 44; wd.column_dimensions['B'].width = 16; wd.column_dimensions['C'].width = 70
wb.save(OUT)

summ = dict(final=len(final), A=sum(1 for r in final if r['cls'] == 'A'), B=sum(1 for r in final if r['cls'] == 'B'), hold=len(hold), excl=len(excl), legacy=len(legacy),
            keep=len(keep_groups), release=len(groups) - len(keep_groups), dups=len(dups),
            xver=sum(1 for r in final if r['x_url']), lang=collections.Counter(r['lang'] for r in final), region=collections.Counter(str(r['region']) for r in final),
            size=collections.Counter(r['sizeb'] for r in final), total=len(res))
print(json.dumps(summ, ensure_ascii=False, default=dict, indent=1))
