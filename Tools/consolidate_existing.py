import json
import re

# Load parsed candidates
with open('Tools/main_111_parsed.json', 'r', encoding='utf-8') as f:
    candidates = json.load(f)

with open('Tools/excel_v190_dump.json', 'r', encoding='utf-8') as f:
    dump = json.load(f)

existing_dups = {r.get('候補名'): r for r in dump.get('グループ重複_除外', [])}
existing_excludes = {r.get('候補名'): r for r in dump.get('除外・後回し', [])}
templates = dump.get('公開後DMテンプレ_最新版', [])

# Defined groups
from reconcile_existing_groups import groups, all_merged_members

# Map candidates by name
cand_by_name = {c['name']: c for c in candidates}

# 1. Build Group Rep Entries
outreach_targets = []
group_members_list = []
merged_and_excluded_list = []

# Process groups
for rep_name, ginfo in groups.items():
    cand = cand_by_name.get(rep_name)
    
    # If rep not in candidates (e.g. PsychoDcast was in collab net but not main 111, or different naming)
    if not cand:
        # Create synthesized record from collab net / known data
        cand = {
            'no': 175,
            'region': '英語圏',
            'name': rep_name,
            'platform': 'Twitch / X',
            'scale': '中規模',
            'x_url': 'https://x.com/PsychoDcast',
            'stream_url': 'https://www.twitch.tv/psychodcast',
            'detail': '英語圏ポッドキャスト・ゲーム配信者。8人規模のPICO PARK 2コラボ主催。',
            'evidence': ginfo['reason'],
            'fit': '非常に高い：8人規模の固定/協力ネットワークの起点',
            'contact': 'X DM候補',
            'priority': 'A',
            'info_source': '2026-01-01 PICO PARK 2 collab告知',
            'notes': '代表1人へのDMから複数人へ波及可能。',
            'lang': '英語',
            'dm_text': ''
        }
    
    # Target entry
    outreach_targets.append({
        'type': '既存維持',
        'rep_name': rep_name,
        'group_name': ginfo['group_name'],
        'lang': cand.get('lang') or '英語',
        'game': ginfo['game'],
        'evidence': ginfo['reason'],
        'members_summary': ' / '.join(ginfo['members']),
        'evidence_url': cand.get('info_source') or cand.get('evidence') or cand.get('stream_url'),
        'x_url': cand.get('x_url'),
        'stream_url': cand.get('stream_url'),
        'region': cand.get('region'),
        'platform': cand.get('platform'),
        'contact': cand.get('contact') or 'X DM',
        'reason': cand.get('detail') or ginfo['reason'],
        'dm_text': cand.get('dm_text')
    })
    
    # Group members entry
    for m in ginfo['members']:
        mcand = cand_by_name.get(m, {})
        group_members_list.append({
            'rep_name': rep_name,
            'group_name': ginfo['group_name'],
            'member_name': m,
            'x_url': mcand.get('x_url'),
            'stream_url': mcand.get('stream_url'),
            'relation': '固定メンバー / 協力配信仲間',
            'merge_reason': f"{rep_name}と同一グループ・重複防止のため統合"
        })
        merged_and_excluded_list.append({
            'name': m,
            'action': 'グループ統合',
            'target_rep': rep_name,
            'reason': f"{ginfo['group_name']}のメンバーとして代表窓口（{rep_name}）に統合"
        })

# Process independent single candidates from existing 111
for c in candidates:
    name = c['name']
    if name in groups or name in all_merged_members:
        continue
    
    # Check if duplicate in v190 dup list
    if name in existing_dups:
        dup_info = existing_dups[name]
        merged_and_excluded_list.append({
            'name': name,
            'action': 'グループ統合',
            'target_rep': dup_info.get('統合先'),
            'reason': dup_info.get('理由')
        })
        continue
    
    # Extract game
    ev = c.get('evidence') or ''
    game = '協力ゲーム'
    if 'PICO PARK 2' in ev or 'Pico Park 2' in ev:
        game = 'PICO PARK 2'
    elif 'PICO PARK' in ev or 'Pico Park' in ev:
        game = 'PICO PARK'
    elif 'MECCHA' in ev or 'Meccha' in ev:
        game = 'MECCHA CHAMELEON'
    elif 'REPO' in ev or 'R.E.P.O.' in ev:
        game = 'R.E.P.O.'
    elif 'Phasmophobia' in ev:
        game = 'Phasmophobia / 協力ホラー'
    elif 'Big Walk' in ev:
        game = 'Big Walk / 協力ゲーム'
    
    outreach_targets.append({
        'type': '既存維持',
        'rep_name': name,
        'group_name': f"{name}（単独窓口）",
        'lang': c.get('lang') or '英語',
        'game': game,
        'evidence': ev,
        'members_summary': 'コラボ仲間（配信時に招集）',
        'evidence_url': c.get('info_source') or c.get('stream_url'),
        'x_url': c.get('x_url'),
        'stream_url': c.get('stream_url'),
        'region': c.get('region'),
        'platform': c.get('platform'),
        'contact': c.get('contact') or 'X DM',
        'reason': c.get('detail') or c.get('notes'),
        'dm_text': c.get('dm_text')
    })

# Add existing excludes from '除外・後回し'
for r in dump.get('除外・後回し', []):
    if r.get('候補'):
        merged_and_excluded_list.append({
            'name': r.get('候補'),
            'action': '除外',
            'target_rep': '-',
            'reason': f"{r.get('理由')} - {r.get('確認内容')}"
        })

print(f"Existing outreach targets so far: {len(outreach_targets)}")
print(f"Group members recorded: {len(group_members_list)}")
print(f"Merged / excluded recorded: {len(merged_and_excluded_list)}")
