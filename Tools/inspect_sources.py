import json

with open('Tools/independent_with_evidence.json', 'r', encoding='utf-8') as f:
    indep = json.load(f)

lines = []
for i, c in enumerate(indep, 1):
    lines.append(f"[{i}] {c['name']} ({c['region']}, {c['lang']})")
    lines.append(f"    Evidence: {c['evidence']}")
    lines.append(f"    Source: {c['info_source']}")
    lines.append(f"    X: {c['x_url']} | Stream: {c['stream_url']}")

with open('Tools/sample_sources_utf8.txt', 'w', encoding='utf-8') as f:
    f.write('\n'.join(lines))

print("Saved sample_sources_utf8.txt")
