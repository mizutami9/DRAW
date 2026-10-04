import json
import re

with open('Tools/main_111_parsed.json', 'r', encoding='utf-8') as f:
    candidates = json.load(f)

# Also load existing group duplicates
with open('Tools/excel_v190_dump.json', 'r', encoding='utf-8') as f:
    dump = json.load(f)

existing_dups = {r.get('候補名'): r for r in dump.get('グループ重複_除外', [])}
existing_excludes = {r.get('候補名'): r for r in dump.get('除外・後回し', [])}

analysis = []
for c in candidates:
    name = c['name']
    ev = str(c['evidence'] or '')
    detail = str(c['detail'] or '')
    notes = str(c['notes'] or '')
    
    # Check if in known dups
    is_dup = name in existing_dups
    dup_target = existing_dups[name].get('統合先') if is_dup else None
    
    # Check if in known excludes
    is_ex = name in existing_excludes
    ex_reason = existing_excludes[name].get('理由') if is_ex else None
    
    analysis.append({
        'no': c['no'],
        'name': name,
        'region': c['region'],
        'lang': c['lang'],
        'is_dup': is_dup,
        'dup_target': dup_target,
        'is_ex': is_ex,
        'ex_reason': ex_reason,
        'x_url': c['x_url'],
        'stream_url': c['stream_url'],
        'evidence': ev
    })

with open('Tools/audit_analysis_111.json', 'w', encoding='utf-8') as f:
    json.dump(analysis, f, ensure_ascii=False, indent=2)

print(f"Audited {len(analysis)} candidates")
