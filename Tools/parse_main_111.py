import json

with open('Tools/excel_v190_dump.json', 'r', encoding='utf-8') as f:
    dump = json.load(f)

sheet_main = dump.get('現実的営業対象_メイン', [])

parsed = []
for r in sheet_main:
    parsed.append({
        'no': r.get('No.'),
        'region': r.get('国・地域'),
        'name': r.get('候補名'),
        'platform': r.get('主戦場'),
        'scale': r.get('登録者/フォロワー数'),
        'x_url': r.get('X URL'),
        'stream_url': r.get('YouTube / Twitch URL'),
        'detail': r.get('詳細・活動内容'),
        'evidence': r.get('複数人実況の根拠'),
        'fit': r.get('NICO DRAWとの相性'),
        'contact': r.get('連絡方法'),
        'priority': r.get('優先度'),
        'info_source': r.get('情報ソース'),
        'notes': r.get('選定メモ'),
        'lang': r.get('DM言語'),
        'dm_text': r.get('送信用DM')
    })

with open('Tools/main_111_parsed.json', 'w', encoding='utf-8') as f:
    json.dump(parsed, f, ensure_ascii=False, indent=2)

print(f"Parsed {len(parsed)} candidates from main sheet")
