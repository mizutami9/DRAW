import json

with open('Tools/excel_v190_dump.json', 'r', encoding='utf-8') as f:
    dump = json.load(f)

sheet51 = dump.get('営業対象_確認済み51人', [])

output = []
for i, r in enumerate(sheet51, 1):
    output.append({
        'no': i,
        'name': r.get('候補名'),
        'lang': r.get('営業言語'),
        'game': r.get('確認ゲーム'),
        'evidence_desc': r.get('複数人プレイ根拠'),
        'evidence_url': r.get('証拠URL'),
        'x_url': r.get('X'),
        'stream_url': r.get('配信先'),
        'group': r.get('営業グループ'),
        'rep': r.get('代表窓口')
    })

with open('Tools/existing_33_groups.json', 'w', encoding='utf-8') as f:
    json.dump(output, f, ensure_ascii=False, indent=2)

print(f"Dumped {len(output)} existing groups")
