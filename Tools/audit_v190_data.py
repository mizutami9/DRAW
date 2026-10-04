import json
import sys

with open('Tools/excel_v190_dump.json', 'r', encoding='utf-8') as f:
    dump = json.load(f)

lines = []

lines.append("=== Sheet: 全111人_複数人プレイ再監査 ===")
sheet111 = dump.get('全111人_複数人プレイ再監査', [])
judgments = {}
for r in sheet111:
    j = r.get('判定', '未設定')
    judgments[j] = judgments.get(j, 0) + 1
lines.append(f"Judgments in 111: {judgments}")

lines.append("\n=== Sheet: 営業対象_確認済み51人 (33組) ===")
sheet51 = dump.get('営業対象_確認済み51人', [])
lines.append(f"Total entries: {len(sheet51)}")
langs51 = {}
for r in sheet51:
    l = r.get('営業言語', '未設定')
    langs51[l] = langs51.get(l, 0) + 1
lines.append(f"Languages in 33 confirmed groups: {langs51}")

lines.append("\n=== Sheet: グループ重複_除外 ===")
sheet_dup = dump.get('グループ重複_除外', [])
lines.append(f"Total duplicates excluded: {len(sheet_dup)}")
for r in sheet_dup:
    lines.append(f"  {r.get('候補名')} -> 統合先: {r.get('統合先')} (理由: {r.get('理由')})")

lines.append("\n=== Remaining in 111 (Not in confirmed 33 or duplicates) ===")
confirmed_names = set(r.get('候補名') for r in sheet51 if r.get('候補名'))
dup_names = set(r.get('候補名') for r in sheet_dup if r.get('候補名'))
all_names = set(r.get('候補名') for r in sheet111 if r.get('候補名'))
other_names = all_names - confirmed_names - dup_names

lines.append(f"Count of other names: {len(other_names)}")
for r in sheet111:
    name = r.get('候補名')
    if name in other_names:
        lines.append(f"  {name} | 判定: {r.get('判定')} | ゲーム: {r.get('確認ゲーム')} | 根拠: {r.get('根拠')}")

with open('Tools/audit_output_utf8.txt', 'w', encoding='utf-8') as f:
    f.write('\n'.join(lines))

print("Saved to audit_output_utf8.txt")
