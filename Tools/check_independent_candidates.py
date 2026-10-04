import json

with open('Tools/main_111_parsed.json', 'r', encoding='utf-8') as f:
    candidates = json.load(f)

with open('Tools/reconcile_existing_groups.py', 'r', encoding='utf-8') as f:
    # Just import all_merged_members and groups
    pass

from reconcile_existing_groups import groups, all_merged_members

reps = set(groups.keys())
independent = []
for c in candidates:
    name = c['name']
    if name not in reps and name not in all_merged_members:
        independent.append(c)

print(f"Total independent: {len(independent)}")

with_evidence = []
without_evidence = []

for c in independent:
    ev = c['evidence'] or ''
    x = c['x_url'] or ''
    stream = c['stream_url'] or ''
    # check if info_source or evidence has an actual url
    src = c['info_source'] or ''
    has_url = 'http' in str(ev) or 'http' in str(src) or 'http' in str(stream)
    
    if has_url:
        with_evidence.append(c)
    else:
        without_evidence.append(c)

print(f"With evidence link/source: {len(with_evidence)}")
print(f"Without evidence link/source: {len(without_evidence)}")

with open('Tools/independent_with_evidence.json', 'w', encoding='utf-8') as f:
    json.dump(with_evidence, f, ensure_ascii=False, indent=2)

with open('Tools/independent_without_evidence.json', 'w', encoding='utf-8') as f:
    json.dump(without_evidence, f, ensure_ascii=False, indent=2)
