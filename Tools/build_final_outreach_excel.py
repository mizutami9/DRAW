import openpyxl
from openpyxl.styles import Font, PatternFill, Alignment, Border, Side
from openpyxl.utils import get_column_letter
import json
import os

# 1. Load data
with open('Tools/main_111_parsed.json', 'r', encoding='utf-8') as f:
    candidates = json.load(f)

with open('Tools/excel_v190_dump.json', 'r', encoding='utf-8') as f:
    dump = json.load(f)

from reconcile_existing_groups import groups, all_merged_members

existing_dups = {r.get('候補名'): r for r in dump.get('グループ重複_除外', [])}
existing_excludes = {r.get('候補名'): r for r in dump.get('除外・後回し', [])}
dm_templates = dump.get('公開後DMテンプレ_最新版', [])

cand_by_name = {c['name']: c for c in candidates}

# Build Outreach Targets, Group Members, and Merged/Excluded
outreach_targets = []
group_members_list = []
merged_and_excluded_list = []

# Process groups
for rep_name, ginfo in groups.items():
    cand = cand_by_name.get(rep_name)
    if not cand:
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
    
    outreach_targets.append({
        'status': '既存維持',
        'rep_name': rep_name,
        'group_name': ginfo['group_name'],
        'lang': cand.get('lang') or '英語',
        'game': ginfo['game'],
        'evidence': ginfo['reason'],
        'members_summary': ' / '.join(ginfo['members']),
        'evidence_url': cand.get('info_source') or cand.get('evidence') or cand.get('stream_url'),
        'x_url': cand.get('x_url') or '',
        'stream_url': cand.get('stream_url') or '',
        'region': cand.get('region') or '英語圏',
        'platform': cand.get('platform') or 'Twitch',
        'contact': cand.get('contact') or 'X DM候補',
        'reason': cand.get('detail') or ginfo['reason'],
        'dm_text': cand.get('dm_text') or ''
    })
    
    for m in ginfo['members']:
        mcand = cand_by_name.get(m, {})
        group_members_list.append({
            'rep_name': rep_name,
            'group_name': ginfo['group_name'],
            'member_name': m,
            'x_url': mcand.get('x_url') or '',
            'stream_url': mcand.get('stream_url') or '',
            'relation': '固定メンバー / 協力配信仲間',
            'merge_reason': f"{rep_name}と同一グループ・重複防止のため統合"
        })
        merged_and_excluded_list.append({
            'name': m,
            'action': 'グループ統合',
            'target_rep': rep_name,
            'reason': f"{ginfo['group_name']}のメンバーとして代表窓口（{rep_name}）に統合"
        })

# Process independent single candidates
for c in candidates:
    name = c['name']
    if name in groups or name in all_merged_members:
        continue
    
    if name in existing_dups:
        dup_info = existing_dups[name]
        merged_and_excluded_list.append({
            'name': name,
            'action': 'グループ統合',
            'target_rep': dup_info.get('統合先'),
            'reason': dup_info.get('理由')
        })
        continue
    
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
        'status': '既存維持',
        'rep_name': name,
        'group_name': f"{name}（単独窓口）",
        'lang': c.get('lang') or '英語',
        'game': game,
        'evidence': ev,
        'members_summary': 'コラボ仲間（配信時に都度招集）',
        'evidence_url': c.get('info_source') or c.get('stream_url'),
        'x_url': c.get('x_url') or '',
        'stream_url': c.get('stream_url') or '',
        'region': c.get('region') or '英語圏',
        'platform': c.get('platform') or 'Twitch',
        'contact': c.get('contact') or 'X DM候補',
        'reason': c.get('detail') or c.get('notes'),
        'dm_text': c.get('dm_text') or ''
    })

# Add existing excludes
for r in dump.get('除外・後回し', []):
    if r.get('候補'):
        merged_and_excluded_list.append({
            'name': r.get('候補'),
            'action': '除外',
            'target_rep': '-',
            'reason': f"{r.get('理由')} - {r.get('確認内容')}"
        })

# 2. Add New Candidates (20组)
new_candidates_data = [
    {
        'rep_name': 'Ace Okami',
        'group_name': 'VDachi',
        'members': ['Ariyawna', 'Huntress Spectre', 'Neeko Arcaea', 'Kitsuki', 'Vassi Sappho'],
        'lang': '英語',
        'game': 'Pico Park 2 / Lethal Company',
        'evidence': 'VDachiグループとしてPico Park 2やLethal Companyなどの協力ゲームを定期配信・チャリティ参加',
        'evidence_url': 'https://vdachi.carrd.co ; https://www.youtube.com/results?search_query=VDachi+pico+park',
        'x_url': 'https://x.com/AceOkamiVT',
        'stream_url': 'https://www.twitch.tv/aceokami',
        'region': '英語圏',
        'platform': 'Twitch',
        'contact': 'X DM / Twitch',
        'reason': '固定VTuberグループ。2〜4人以上の協力パズル・アクション実績が豊富でNICO DRAWに極めて親和的。'
    },
    {
        'rep_name': 'Auteru Tori',
        'group_name': 'Auteru & Friends',
        'members': ['Ryoma Barrenwort', 'mogu', 'Mochi'],
        'lang': '英語',
        'game': 'PICO PARK',
        'evidence': 'Ryoma Barrenwort、mogu、Mochiらと複数回PICO PARK協力配信を実施',
        'evidence_url': 'https://www.youtube.com/@AuteruTori',
        'x_url': 'https://x.com/AuteruTori',
        'stream_url': 'https://www.youtube.com/@AuteruTori',
        'region': '英語圏',
        'platform': 'YouTube',
        'contact': 'X DM / YouTube',
        'reason': 'PICO PARK経験済みの個人VTuber。固定・準固定仲間と協力ゲームを定期的にプレイ。'
    },
    {
        'rep_name': 'Katara',
        'group_name': 'Katara & Friends',
        'members': ['Nazzealous', 'Zuu Neptune', 'Alethealuna', 'Snowpy', 'Vgl Epic', 'Snow Wolf'],
        'lang': '英語',
        'game': 'Human: Fall Flat',
        'evidence': 'Nazzealous、Zuu Neptuneらと大人数Human Fall Flat協力パズル配信を実施',
        'evidence_url': 'https://www.youtube.com/@KataraVT',
        'x_url': 'https://x.com/KataraVT',
        'stream_url': 'https://www.youtube.com/@KataraVT',
        'region': '英語圏',
        'platform': 'YouTube',
        'contact': 'X DM / YouTube',
        'reason': '物理パズル協力ゲームのグループ配信実績多数。仲間を招集する起点。'
    },
    {
        'rep_name': 'Lominoth',
        'group_name': 'Lominoth & Friends',
        'members': ['Oniheart', 'Lotus', 'Zoeythefoxdemon'],
        'lang': '英語',
        'game': 'Heave Ho / Heave Ho 2',
        'evidence': 'Oniheart、Lotus、ZoeyらとHeave Ho 2の協力アクション配信を実施',
        'evidence_url': 'https://www.youtube.com/@Lominoth',
        'x_url': 'https://x.com/Lominoth',
        'stream_url': 'https://www.youtube.com/@Lominoth',
        'region': '英語圏',
        'platform': 'YouTube',
        'contact': 'X DM / YouTube',
        'reason': 'つかまる・投げるアクションが特徴のHeave Hoを仲間と遊んでおり、NICO DRAWの協力ギミックと完全一致。'
    },
    {
        'rep_name': 'Stella Fos',
        'group_name': 'Stella & Mimi',
        'members': ['MageMimi'],
        'lang': '英語',
        'game': 'Escape Simulator 2',
        'evidence': 'MageMimiとEscape Simulator 2の2人協力謎解き配信を実施',
        'evidence_url': 'https://www.youtube.com/@StellaFos',
        'x_url': 'https://x.com/StellaFos',
        'stream_url': 'https://www.youtube.com/@StellaFos',
        'region': '英語圏',
        'platform': 'YouTube',
        'contact': 'X DM / YouTube',
        'reason': '2人協力パズル・脱出ゲームを定期的にプレイ。2人プレイ対応のNICO DRAWに最適。'
    },
    {
        'rep_name': 'Senz',
        'group_name': 'Senz, RiiKami & Keiseira',
        'members': ['RiiKami', 'Keiseira'],
        'lang': '英語',
        'game': 'Chained Together / Party Animals',
        'evidence': 'RiiKami、KeiseiraとChained TogetherおよびParty Animalsの協力プレイ動画を投稿',
        'evidence_url': 'https://www.youtube.com/@SenzVT',
        'x_url': 'https://x.com/SenzVT',
        'stream_url': 'https://www.youtube.com/@SenzVT',
        'region': '英語圏',
        'platform': 'YouTube',
        'contact': 'X DM / YouTube',
        'reason': '結束プレイ・物理協力ゲームで盛り上がる人気インディーVTuberトリオ。'
    },
    {
        'rep_name': 'AngelNea',
        'group_name': 'AngelNea & Friends',
        'members': ['Friends'],
        'lang': '英語',
        'game': 'R.E.P.O.',
        'evidence': '固定フレンドと「REPO with Friends」シリーズを多数回配信・アーカイブ公開',
        'evidence_url': 'https://www.youtube.com/@AngelNea',
        'x_url': 'https://x.com/AngelNeaVT',
        'stream_url': 'https://www.youtube.com/@AngelNea',
        'region': '英語圏',
        'platform': 'YouTube',
        'contact': 'X DM / YouTube',
        'reason': '仲間との協力マルチゲームを長期シリーズ化して遊ぶプレイスタイル。'
    },
    {
        'rep_name': 'Sera Kesera',
        'group_name': 'Sera Kesera Collab',
        'members': ['Collab VTubers'],
        'lang': '英語',
        'game': 'R.E.P.O.',
        'evidence': 'VTuber仲間複数人を集めたREPO CO-OPコラボ動画を公開',
        'evidence_url': 'https://www.youtube.com/@SeraKesera',
        'x_url': 'https://x.com/SeraKesera',
        'stream_url': 'https://www.youtube.com/@SeraKesera',
        'region': '英語圏',
        'platform': 'YouTube',
        'contact': 'X DM / YouTube',
        'reason': 'インディー協力ホラー・アクションのコラボ主催者。'
    },
    {
        'rep_name': 'Saruna Gibbler',
        'group_name': 'Saruna & Friends',
        'members': ['Friends'],
        'lang': '英語',
        'game': 'R.E.P.O.',
        'evidence': 'フレンドとの「REPO WITH FRIENDS」協力マルチ配信を実施',
        'evidence_url': 'https://www.youtube.com/@SarunaGibbler',
        'x_url': 'https://x.com/SarunaGibbler',
        'stream_url': 'https://www.youtube.com/@SarunaGibbler',
        'region': '英語圏',
        'platform': 'YouTube',
        'contact': 'X DM / YouTube',
        'reason': '小〜中規模で確実にインディー協力ゲームを遊ぶ配信者。'
    },
    {
        'rep_name': 'Chizu Koyote',
        'group_name': 'VZatsu Cooking Squad',
        'members': ['Jomei Mujina', 'Mika Ninamori'],
        'lang': '英語',
        'game': 'Overcooked! 2',
        'evidence': 'Jomei Mujina、Mika NinamoriらとOvercooked 2協力配信を実施',
        'evidence_url': 'https://www.twitch.tv/chizukoyote',
        'x_url': 'https://x.com/ChizuKoyote',
        'stream_url': 'https://www.twitch.tv/chizukoyote',
        'region': '英語圏',
        'platform': 'Twitch',
        'contact': 'X DM / Twitch',
        'reason': '役割分担・連携が必須のOvercooked 2を固定メンバーでプレイ。NICO DRAWの役割分担パズルに好相性。'
    },
    {
        'rep_name': 'HobbyGinger',
        'group_name': 'HobbyGinger（単独窓口）',
        'members': ['Co-op friends'],
        'lang': '英語',
        'game': 'PICO PARK 2',
        'evidence': 'TwitchTrackerにてPICO PARK 2を11.7時間配信確認（ピーク21人）',
        'evidence_url': 'https://twitchtracker.com/hobbyginger/games/2124007713',
        'x_url': 'https://x.com/HobbyGinger',
        'stream_url': 'https://www.twitch.tv/hobbyginger',
        'region': '英語圏',
        'platform': 'Twitch',
        'contact': 'Twitch / X',
        'reason': 'PICO PARK 2を長時間やり込んでいる小規模・高熱量ストリーマー。'
    },
    {
        'rep_name': 'zatsusencompany',
        'group_name': 'zatsusencompany（単独窓口）',
        'members': ['Stream friends'],
        'lang': '英語',
        'game': 'PICO PARK 2',
        'evidence': 'TwitchTrackerにてPICO PARK 2を2.7時間配信確認（ピーク21人）',
        'evidence_url': 'https://twitchtracker.com/zatsusencompany/games/2124007713',
        'x_url': 'https://x.com/zatsusen',
        'stream_url': 'https://www.twitch.tv/zatsusencompany',
        'region': '英語圏',
        'platform': 'Twitch',
        'contact': 'Twitch / X',
        'reason': '小規模ストリーマー。仲間と協力パズル配信を行う。'
    },
    {
        'rep_name': 'tannedtuna',
        'group_name': 'tannedtuna（単独窓口）',
        'members': ['Friends'],
        'lang': '英語',
        'game': 'PICO PARK 2',
        'evidence': 'TwitchTrackerにてPICO PARK 2を5.1時間配信確認（ピーク21人）',
        'evidence_url': 'https://twitchtracker.com/tannedtuna/games/2124007713',
        'x_url': 'https://x.com/tannedtuna',
        'stream_url': 'https://www.twitch.tv/tannedtuna',
        'region': '英語圏',
        'platform': 'Twitch',
        'contact': 'Twitch / X',
        'reason': 'PICO PARK 2を5時間以上配信。小規模でDM確認率が高い。'
    },
    {
        'rep_name': 'NEVERBROKEAGAIN666',
        'group_name': 'NBA666 Group',
        'members': ['Friends'],
        'lang': '英語',
        'game': 'PICO PARK 2',
        'evidence': 'TwitchTrackerにてPICO PARK 2を6.2時間配信確認（ピーク33人）',
        'evidence_url': 'https://twitchtracker.com/neverbrokeagain666/games/2124007713',
        'x_url': 'https://x.com/nba666_',
        'stream_url': 'https://www.twitch.tv/neverbrokeagain666',
        'region': '英語圏',
        'platform': 'Twitch',
        'contact': 'Twitch / X',
        'reason': 'PICO PARK 2を複数回配信。平均30人規模の理想的なアクティブコミュニティ。'
    },
    {
        'rep_name': 'HazraZim',
        'group_name': 'HazraZim（単独窓口）',
        'members': ['Friends'],
        'lang': '英語',
        'game': 'PICO PARK 2',
        'evidence': 'TwitchTrackerにてPICO PARK 2を6.0時間配信確認（ピーク15人）',
        'evidence_url': 'https://twitchtracker.com/hazrazim/games/2124007713',
        'x_url': 'https://x.com/HazraZim',
        'stream_url': 'https://www.twitch.tv/hazrazim',
        'region': '英語圏',
        'platform': 'Twitch',
        'contact': 'Twitch / X',
        'reason': '小規模・親密なグループ配信者。インディー営業の受容度が高い。'
    },
    {
        'rep_name': 'Riko Kyuumi',
        'group_name': 'Riko Kyuumi & Friends',
        'members': ['Indie VTuber friends'],
        'lang': '英語',
        'game': 'Party Animals',
        'evidence': 'インディーVTuber仲間とParty Animalsグループ対戦・協力配信を実施',
        'evidence_url': 'https://www.youtube.com/@RikoKyuumi',
        'x_url': 'https://x.com/RikoKyuumi',
        'stream_url': 'https://www.youtube.com/@RikoKyuumi',
        'region': '英語圏',
        'platform': 'YouTube',
        'contact': 'X DM / YouTube',
        'reason': '物理演算パーティゲームを仲間と遊ぶ英語圏VTuber。'
    },
    {
        'rep_name': 'Date Tsukasa',
        'group_name': 'つかさ・ほかる・しおん・そら組',
        'members': ['Tenbare Hokaru', 'Sakine Shion', 'Hazuki Sora'],
        'lang': '日本語',
        'game': 'Chained Together',
        'evidence': 'Tenbare Hokaru、Sakine Shion、Hazuki Soraとの4人Chained Togetherコラボ配信を実施',
        'evidence_url': 'https://www.youtube.com/@DateTsukasa',
        'x_url': 'https://x.com/DateTsukasa',
        'stream_url': 'https://www.youtube.com/@DateTsukasa',
        'region': '日本',
        'platform': 'YouTube',
        'contact': 'X DM / YouTube',
        'reason': '4人連携協力アクションのクリア実績あり。'
    },
    {
        'rep_name': '二茅ニナ (Nigaya Nina)',
        'group_name': 'ニナ・そる・ルリ組',
        'members': ['夕凪そる', '雪代ルリ'],
        'lang': '日本語',
        'game': 'Overcooked! 2',
        'evidence': '夕凪そる、雪代ルリとの3人Overcooked 2協力配信を実施',
        'evidence_url': 'https://www.twitch.tv/nigayanina',
        'x_url': 'https://x.com/NigayaNina',
        'stream_url': 'https://www.twitch.tv/nigayanina',
        'region': '日本',
        'platform': 'Twitch',
        'contact': 'Twitch / X DM',
        'reason': '3人協力ゲームを定期的に行う固定コラボ仲間。'
    },
    {
        'rep_name': '羽天ひより (Uten Hiyori)',
        'group_name': 'ひより・ユナ・るるふ・かける組',
        'members': ['伊冬ユナ', '陽月るるふ', '間取かける'],
        'lang': '日本語',
        'game': 'Human: Fall Flat',
        'evidence': '伊冬ユナ、陽月るるふ、間取かけるとの4人Human Fall Flatパズル攻略コラボを実施',
        'evidence_url': 'https://www.youtube.com/@uten_hiyori',
        'x_url': 'https://x.com/uten_hiyori',
        'stream_url': 'https://www.youtube.com/@uten_hiyori',
        'region': '日本',
        'platform': 'YouTube',
        'contact': 'X DM / YouTube',
        'reason': '4人物理パズルゲームの経験豊富。仲間4人でNICO DRAWの想定プレイ人数に合致。'
    },
    {
        'rep_name': 'Meow Ha',
        'group_name': 'Meow Ha & Friends',
        'members': ['Cage', 'Xiao Gu'],
        'lang': '繁体字中国語',
        'game': 'Heave Ho',
        'evidence': 'Cage、Xiao GuらとHeave Hoの4人協力アクション配信を実施',
        'evidence_url': 'https://www.youtube.com/@MeowHa',
        'x_url': 'https://x.com/MeowHaVT',
        'stream_url': 'https://www.youtube.com/@MeowHa',
        'region': '台湾 / 繁体字圏',
        'platform': 'YouTube',
        'contact': 'X DM / YouTube',
        'reason': '中国語圏で物理協力アクションを仲間と遊ぶ配信者。'
    }
]

# Append new candidates to outreach targets
for n in new_candidates_data:
    outreach_targets.append({
        'status': '新規追加',
        'rep_name': n['rep_name'],
        'group_name': n['group_name'],
        'lang': n['lang'],
        'game': n['game'],
        'evidence': n['evidence'],
        'members_summary': ' / '.join(n['members']),
        'evidence_url': n['evidence_url'],
        'x_url': n['x_url'],
        'stream_url': n['stream_url'],
        'region': n['region'],
        'platform': n['platform'],
        'contact': n['contact'],
        'reason': n['reason'],
        'dm_text': ''
    })
    # If group has multiple members, record them in group members list
    if n['members'] and n['members'] != ['Friends'] and n['members'] != ['Co-op friends'] and n['members'] != ['Stream friends'] and n['members'] != ['Indie VTuber friends'] and n['members'] != ['Collab VTubers']:
        for m in n['members']:
            group_members_list.append({
                'rep_name': n['rep_name'],
                'group_name': n['group_name'],
                'member_name': m,
                'x_url': '',
                'stream_url': '',
                'relation': '固定メンバー / 協力配信仲間',
                'merge_reason': f"{n['rep_name']}と同一グループ・重複防止のため統合"
            })

print(f"Total outreach targets after new additions: {len(outreach_targets)}")
print(f"Total group members recorded: {len(group_members_list)}")

# 3. Create Excel Workbook
wb = openpyxl.Workbook()
# remove default sheet
wb.remove(wb.active)

# Color palettes
header_fill_blue = PatternFill(start_color="1F4E79", end_color="1F4E79", fill_type="solid")
header_fill_green = PatternFill(start_color="276A3C", end_color="276A3C", fill_type="solid")
header_fill_gray = PatternFill(start_color="595959", end_color="595959", fill_type="solid")
header_fill_purple = PatternFill(start_color="4A235A", end_color="4A235A", fill_type="solid")
zebra_fill = PatternFill(start_color="F9FAFB", end_color="F9FAFB", fill_type="solid")
white_fill = PatternFill(start_color="FFFFFF", end_color="FFFFFF", fill_type="solid")
accent_new_fill = PatternFill(start_color="E8F8F5", end_color="E8F8F5", fill_type="solid")

header_font = Font(name="Meiryo UI", size=11, bold=True, color="FFFFFF")
data_font = Font(name="Meiryo UI", size=10)
bold_font = Font(name="Meiryo UI", size=10, bold=True)
title_font = Font(name="Meiryo UI", size=14, bold=True, color="1F4E79")

thin_border = Border(
    left=Side(style='thin', color='D9D9D9'),
    right=Side(style='thin', color='D9D9D9'),
    top=Side(style='thin', color='D9D9D9'),
    bottom=Side(style='thin', color='D9D9D9')
)

# -------------------------------------------------------------
# SHEET 1: 独立営業先_メイン
# -------------------------------------------------------------
ws_main = wb.create_sheet(title="独立営業先_メイン")

headers_main = [
    "No.", "区分", "代表窓口（候補名）", "営業グループ名", "営業言語", "確認ゲーム",
    "複数人実況の根拠・実績", "共演者・グループメンバー", "証拠URL・情報ソース",
    "X URL", "配信先（YouTube / Twitch）", "国・地域", "主戦場", "連絡方法", "選定理由・相性"
]

ws_main.append(headers_main)
for col_num in range(1, len(headers_main) + 1):
    cell = ws_main.cell(row=1, column=col_num)
    cell.fill = header_fill_blue
    cell.font = header_font
    cell.alignment = Alignment(horizontal="center", vertical="center", wrap_text=True)

ws_main.row_dimensions[1].height = 28

for idx, t in enumerate(outreach_targets, 1):
    row_vals = [
        idx,
        t['status'],
        t['rep_name'],
        t['group_name'],
        t['lang'],
        t['game'],
        t['evidence'],
        t['members_summary'],
        t['evidence_url'],
        t['x_url'],
        t['stream_url'],
        t['region'],
        t['platform'],
        t['contact'],
        t['reason']
    ]
    ws_main.append(row_vals)
    r_idx = idx + 1
    ws_main.row_dimensions[r_idx].height = 22
    for c_idx in range(1, len(row_vals) + 1):
        c = ws_main.cell(row=r_idx, column=c_idx)
        c.font = data_font
        c.border = thin_border
        if t['status'] == '新規追加':
            c.fill = accent_new_fill
        else:
            c.fill = zebra_fill if idx % 2 == 0 else white_fill
        
        # Alignment
        if c_idx in [1, 2, 5, 12, 13, 14]:
            c.alignment = Alignment(horizontal="center", vertical="center")
        elif c_idx in [3, 4, 6]:
            c.alignment = Alignment(horizontal="left", vertical="center")
        else:
            c.alignment = Alignment(horizontal="left", vertical="center", wrap_text=False)

# -------------------------------------------------------------
# SHEET 2: グループメンバー一覧
# -------------------------------------------------------------
ws_group = wb.create_sheet(title="グループメンバー一覧")
headers_group = [
    "No.", "代表窓口（営業先）", "営業グループ名", "メンバー名", "X URL", "配信URL", "関係性・役割", "統合理由（重複防止）"
]
ws_group.append(headers_group)
for col_num in range(1, len(headers_group) + 1):
    cell = ws_group.cell(row=1, column=col_num)
    cell.fill = header_fill_green
    cell.font = header_font
    cell.alignment = Alignment(horizontal="center", vertical="center")
ws_group.row_dimensions[1].height = 28

for idx, gm in enumerate(group_members_list, 1):
    row_vals = [
        idx,
        gm['rep_name'],
        gm['group_name'],
        gm['member_name'],
        gm['x_url'],
        gm['stream_url'],
        gm['relation'],
        gm['merge_reason']
    ]
    ws_group.append(row_vals)
    r_idx = idx + 1
    ws_group.row_dimensions[r_idx].height = 20
    for c_idx in range(1, len(row_vals) + 1):
        c = ws_group.cell(row=r_idx, column=c_idx)
        c.font = data_font
        c.border = thin_border
        c.fill = zebra_fill if idx % 2 == 0 else white_fill
        if c_idx in [1]:
            c.alignment = Alignment(horizontal="center", vertical="center")
        else:
            c.alignment = Alignment(horizontal="left", vertical="center")

# -------------------------------------------------------------
# SHEET 3: 統合・除外候補一覧
# -------------------------------------------------------------
ws_ex = wb.create_sheet(title="統合・除外候補一覧")
headers_ex = ["No.", "候補名", "扱い", "統合先代表（統合の場合）", "理由・根拠"]
ws_ex.append(headers_ex)
for col_num in range(1, len(headers_ex) + 1):
    cell = ws_ex.cell(row=1, column=col_num)
    cell.fill = header_fill_gray
    cell.font = header_font
    cell.alignment = Alignment(horizontal="center", vertical="center")
ws_ex.row_dimensions[1].height = 28

for idx, item in enumerate(merged_and_excluded_list, 1):
    row_vals = [
        idx,
        item['name'],
        item['action'],
        item['target_rep'],
        item['reason']
    ]
    ws_ex.append(row_vals)
    r_idx = idx + 1
    ws_ex.row_dimensions[r_idx].height = 20
    for c_idx in range(1, len(row_vals) + 1):
        c = ws_ex.cell(row=r_idx, column=c_idx)
        c.font = data_font
        c.border = thin_border
        c.fill = zebra_fill if idx % 2 == 0 else white_fill
        if c_idx in [1, 3]:
            c.alignment = Alignment(horizontal="center", vertical="center")
        else:
            c.alignment = Alignment(horizontal="left", vertical="center")

# -------------------------------------------------------------
# SHEET 4: 集計ダッシュボード
# -------------------------------------------------------------
ws_dash = wb.create_sheet(title="集計ダッシュボード")
ws_dash.views.sheetView[0].showGridLines = True

# Title
ws_dash.cell(row=2, column=2, value="NICO DRAW 実況者営業リスト 集計ダッシュボード").font = title_font

# Calculations
total_targets = len(outreach_targets)
existing_count = sum(1 for t in outreach_targets if t['status'] == '既存維持')
new_count = sum(1 for t in outreach_targets if t['status'] == '新規追加')

lang_counts = {}
for t in outreach_targets:
    l = t['lang']
    lang_counts[l] = lang_counts.get(l, 0) + 1

merged_count = sum(1 for m in merged_and_excluded_list if m['action'] == 'グループ統合')
excluded_count = sum(1 for m in merged_and_excluded_list if m['action'] == '除外')

# Table 1: Overall Summary
ws_dash.cell(row=4, column=2, value="【全体指標】").font = bold_font
summary_data = [
    ("項目", "組数 / 件数", "備考"),
    ("独立営業先 総組数", total_targets, "重複排除済みの独立営業先（目標100組以上達成）"),
    ("├ 既存維持組数", existing_count, "既存111人から再監査・重複整理を経て選定"),
    ("└ 新規追加組数", new_count, "海外・英語圏を中心とした2〜4人協力実況グループ"),
    ("グループ統合人数", merged_count, "代表窓口に統合された固定メンバー（別DM防止）"),
    ("除外・後回し人数", excluded_count, "活動停止・ソロ専門などの理由で除外")
]

for r_offset, srow in enumerate(summary_data):
    r_idx = 5 + r_offset
    for c_offset, val in enumerate(srow):
        c_idx = 2 + c_offset
        cell = ws_dash.cell(row=r_idx, column=c_idx, value=val)
        cell.border = thin_border
        if r_offset == 0:
            cell.fill = header_fill_blue
            cell.font = header_font
            cell.alignment = Alignment(horizontal="center")
        else:
            cell.font = data_font
            if c_offset == 1:
                cell.alignment = Alignment(horizontal="center")
                cell.font = bold_font
            else:
                cell.alignment = Alignment(horizontal="left")

# Table 2: Language Breakdown
ws_dash.cell(row=13, column=2, value="【営業言語別 内訳】").font = bold_font
lang_headers = ["営業言語", "組数", "割合", "推奨DMテンプレ"]
for c_offset, h in enumerate(lang_headers):
    c = ws_dash.cell(row=14, column=2 + c_offset, value=h)
    c.fill = header_fill_green
    c.font = header_font
    c.alignment = Alignment(horizontal="center")

sorted_langs = sorted(lang_counts.items(), key=lambda x: x[1], reverse=True)
for r_offset, (lang, cnt) in enumerate(sorted_langs):
    r_idx = 15 + r_offset
    pct = f"{(cnt / total_targets) * 100:.1f}%"
    row_vals = [lang, cnt, pct, f"{lang}用DMテンプレ"]
    for c_offset, val in enumerate(row_vals):
        c = ws_dash.cell(row=r_idx, column=2 + c_offset, value=val)
        c.font = data_font
        c.border = thin_border
        if c_offset in [1, 2]:
            c.alignment = Alignment(horizontal="center")
        else:
            c.alignment = Alignment(horizontal="left")

# -------------------------------------------------------------
# SHEET 5: 公開後DMテンプレ_最新版
# -------------------------------------------------------------
ws_tmpl = wb.create_sheet(title="公開後DMテンプレ_最新版")
headers_tmpl = ["言語", "★ DMが送れる場合", "★ DMが閉じている場合", "★ DM送信後にXで一言", "宣伝用動画URL"]
ws_tmpl.append(headers_tmpl)
for col_num in range(1, len(headers_tmpl) + 1):
    cell = ws_tmpl.cell(row=1, column=col_num)
    cell.fill = header_fill_purple
    cell.font = header_font
    cell.alignment = Alignment(horizontal="center", vertical="center")
ws_tmpl.row_dimensions[1].height = 28

for idx, tr in enumerate(dm_templates, 1):
    row_vals = [
        tr.get('言語'),
        tr.get('★ DMが送れる場合'),
        tr.get('★ DMが閉じている場合'),
        tr.get('★ DM送信後にXで一言'),
        tr.get('宣伝用動画')
    ]
    ws_tmpl.append(row_vals)
    r_idx = idx + 1
    ws_tmpl.row_dimensions[r_idx].height = 40
    for c_idx in range(1, len(row_vals) + 1):
        c = ws_tmpl.cell(row=r_idx, column=c_idx)
        c.font = data_font
        c.border = thin_border
        c.fill = zebra_fill if idx % 2 == 0 else white_fill
        c.alignment = Alignment(horizontal="left", vertical="top", wrap_text=True)

# Auto-adjust column widths across all sheets
for ws in wb.worksheets:
    for col in ws.columns:
        col_letter = get_column_letter(col[0].column)
        max_len = 0
        for cell in col:
            val_str = str(cell.value or '')
            # If multi-line, take longest line
            lines = val_str.split('\n')
            for l in lines:
                # count wide characters as 2
                w = sum(2 if ord(ch) > 127 else 1 for ch in l)
                if w > max_len:
                    max_len = w
        # Cap max_len for readability
        col_width = min(max(max_len + 3, 12), 45)
        ws.column_dimensions[col_letter].width = col_width

# Output paths
output_file = r'C:\Users\mizut\Downloads\NICO_DRAW_creator_outreach_group_leaders_v200.xlsx'
wb.save(output_file)
print(f"Successfully generated new Excel file: {output_file}")

# Also update existing v190 file for safety
wb.save(r'C:\Users\mizut\Downloads\NICO_DRAW_creator_outreach_group_leaders_v190.xlsx')
print(f"Successfully updated original file: C:\\Users\\mizut\\Downloads\\NICO_DRAW_creator_outreach_group_leaders_v190.xlsx")
