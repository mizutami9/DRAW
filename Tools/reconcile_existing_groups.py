import json

with open('Tools/main_111_parsed.json', 'r', encoding='utf-8') as f:
    candidates = json.load(f)

with open('Tools/excel_v190_dump.json', 'r', encoding='utf-8') as f:
    dump = json.load(f)

# Existing duplicates
existing_dups = {r.get('候補名'): r for r in dump.get('グループ重複_除外', [])}

# Group mapping definitions (including new discoveries)
# Group: (Rep Name, Group Name, [Member Names])
groups = {
    'Lonroce': {
        'rep': 'Lonroce',
        'group_name': 'Lonroce グループ',
        'members': ['舒鼠鼠數'],
        'game': 'PICO PARK 2',
        'reason': '固定メンバー4人を明記した複数回のPICO PARK 2告知を確認'
    },
    '柊むらさき / Hiragi Murasaki': {
        'rep': '柊むらさき / Hiragi Murasaki',
        'group_name': '台湾 MECCHAコラボグループ',
        'members': ['柳葉魚子醬 / Yanagiba Nanako', '夢見ルノ / Runo', '涅菈 Nera / Nera_Reverie'],
        'game': 'MECCHA CHAMELEON',
        'reason': '7/10大型コラボ・連動対象として確認'
    },
    'Kymani / kymaniklowni': {
        'rep': 'Kymani / kymaniklowni',
        'group_name': 'Kymani MECCHAグループ',
        'members': ['Midorin', 'Lemon / theLemonCitron', 'Codename: Echo', 'TheLarxa'],
        'game': 'MECCHA CHAMELEON',
        'reason': '共演者5名をタイトルに明記したMECCHA配信'
    },
    'krillianvt': {
        'rep': 'krillianvt',
        'group_name': 'krillian PICO PARK 2グループ',
        'members': ['payavt', 'starpriism'],
        'game': 'PICO PARK 2',
        'reason': '6/17に5名の共演者を明記、3時間プレイ'
    },
    'Koemi Eimii': {
        'rep': 'Koemi Eimii',
        'group_name': 'タイ MECCHAコラボグループ',
        'members': ['PopKorn163', 'Miledy Z', 'Harumoon', 'Mizuhara Yami', 'Sienna [RE:S]', 'Meteona Mars'],
        'game': 'MECCHA CHAMELEON',
        'reason': '7/27本人コラボ告知・複数VTuber共演'
    },
    'Bebe Duck': {
        'rep': 'Bebe Duck',
        'group_name': 'DayJobÜ PH',
        'members': ['SralZoren / Zoren', 'Kyun Shino'],
        'game': 'L4D2 / 協力ゲーム',
        'reason': 'DayJobÜ固定コラボ相手として複数回確認'
    },
    'PsychoDcast': {
        'rep': 'PsychoDcast',
        'group_name': 'PsychoDcast PICO PARK 2 グループ',
        'members': ['Kotoki', 'tigerish / TIGERINTOKYO', 'Aurelia Lillia', 'Steamypot', 'AngryOwlCat', 'HanahPanda', 'yueho'],
        'game': 'PICO PARK 2',
        'reason': '2026-01-01 PICO PARK 2 8人コラボ告知・英語圏ネットワーク'
    },
    '月奏あず / Lunon Azu': {
        'rep': '月奏あず / Lunon Azu',
        'group_name': 'めっちゃカメレオン (MECCHA CHAMELEON組)',
        'members': ['宇宙色きのこ / Sorairo Kinoko', 'Amelie Kanon / アメリー・カノン', 'さんご。', 'シャルロット・リリー', '恣ノ宮うか'],
        'game': 'Overcooked! 2 / Content Warning',
        'reason': '「めっちゃカメレオン」メンバーとしてOvercooked! 2やContent Warningを定期共演'
    },
    '雨閖ナムフォン': {
        'rep': '雨閖ナムフォン',
        'group_name': 'ナムフォン＆つくる PICO PARK 2組',
        'members': ['黒須つくる', '潤田一佐'],
        'game': 'PICO PARK 2',
        'reason': 'PICO PARK 2 鼎談コラボ配信・複数回共演'
    },
    'BlurCyrus': {
        'rep': 'BlurCyrus',
        'group_name': 'BlurCyrus Game Night組',
        'members': ['madmill', 'lanvoine'],
        'game': 'PICO PARK 2',
        'reason': '3人Game Night / PICO PARK 2 Chaos配信'
    }
}

# Collect all members that are mapped into a group
all_merged_members = set()
for g in groups.values():
    for m in g['members']:
        all_merged_members.add(m)

print(f"Total merged members: {len(all_merged_members)}")
print(f"Total defined groups: {len(groups)}")

# Now check how many of the 111 candidates are accounted for
candidate_map = {c['name']: c for c in candidates}
matched_reps = [name for name in groups.keys() if name in candidate_map]
matched_members = [name for name in all_merged_members if name in candidate_map]

print(f"Matched reps in 111: {len(matched_reps)}")
print(f"Matched members in 111: {len(matched_members)}")

independent_candidates = []
for c in candidates:
    name = c['name']
    if name not in groups and name not in all_merged_members:
        independent_candidates.append(c)

print(f"Independent single candidates in 111: {len(independent_candidates)}")
print(f"Total independent outreach targets from existing 111: {len(groups) + len(independent_candidates)}")
