import json
import sys

with open('Tools/excel_v190_dump.json', 'r', encoding='utf-8') as f:
    dump = json.load(f)

lines = []
collab_net = dump.get('コラボ網', [])
lines.append("=== コラボ網シート ===")
for r in collab_net:
    lines.append(f"起点: {r.get('起点')} | 共演者: {r.get('共演者/グループ')} | 人数: {r.get('人数')} | 根拠: {r.get('根拠')} | 活用: {r.get('NICO DRAW活用')} | 元No: {r.get('元No.')}")

sheet_main = dump.get('現実的営業対象_メイン', [])
lines.append(f"\nTotal in main: {len(sheet_main)}")
for r in sheet_main[:15]:
    lines.append(f"No.{r.get('No.')}: {r.get('候補名')} ({r.get('国・地域')}, {r.get('主戦場')}, {r.get('DM言語')}) - {r.get('複数人実況の根拠')}")

with open('Tools/collab_network_utf8.txt', 'w', encoding='utf-8') as f:
    f.write('\n'.join(lines))

print("Saved to Tools/collab_network_utf8.txt")
