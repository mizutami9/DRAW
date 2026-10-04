from __future__ import annotations

from collections import Counter
from copy import copy
from pathlib import Path
import json
import re
import sys

import openpyxl
from openpyxl.comments import Comment
from openpyxl.formatting.rule import FormulaRule
from openpyxl.styles import Alignment, Border, Font, PatternFill, Side
from openpyxl.worksheet.datavalidation import DataValidation

sys.stdout.reconfigure(encoding="utf-8")

SOURCE = Path(r"C:\workSpace\PICO\NICO_DRAW_creator_outreach_WORKING.xlsx")
REFRESH = Path(r"C:\workSpace\PICO\Tools\audit\creator_evidence_refresh.json")
OUTPUT = Path(r"C:\workSpace\PICO\NICO_DRAW_creator_outreach_FINAL.xlsx")
CHECKED = "2026-10-03"

NEAR_GAMES = {
    "pico park 2", "meccha chameleon", "overcooked! 2", "chained together",
    "heave ho", "big walk", "party animals", "fall guys", "peak", "r.e.p.o.",
    "escape the backrooms", "rv there yet?", "bombanana!", "we gotta go",
    "baby kings", "pratfall", "waterpark simulator", "funnel runners",
    "we were here together", "pogostuck: rage with your friends",
}


def twitch_login(value: object) -> str | None:
    match = re.search(r"twitch\.tv/([A-Za-z0-9_]+)", str(value or ""), re.I)
    return match.group(1).lower() if match else None


def normalize(value: object) -> str:
    return re.sub(r"[^a-z0-9\u3040-\u30ff\u3400-\u9fff\u0e00-\u0e7f]+", "", str(value or "").lower())


def evidence_clues(value: object) -> list[tuple[str, str]]:
    clues = []
    for part in re.split(r"\s+/\s+", str(value or "")):
        date = re.search(r"(20\d{2}-\d{2}-\d{2})", part)
        title = re.search(r"『([^』]+)", part)
        if date:
            clues.append((date.group(1), title.group(1) if title else ""))
    return clues


def direct_videos(channel: dict, evidence: object, games: object) -> list[dict]:
    videos = channel.get("videos") or []
    selected: list[dict] = []
    used: set[str] = set()
    for date, title in evidence_clues(evidence):
        title_norm = normalize(title)
        candidates = [v for v in videos if str(v.get("createdAt", ""))[:10] == date]
        if title_norm:
            candidates.sort(
                key=lambda v: (
                    title_norm not in normalize(v.get("title"))
                    and normalize(v.get("title")) not in title_norm,
                    -len(set(title_norm) & set(normalize(v.get("title")))),
                )
            )
        for video in candidates:
            if video.get("id") not in used:
                selected.append(video)
                used.add(video.get("id"))
                break
        if len(selected) >= 3:
            return selected

    wanted = [normalize(g) for g in str(games or "").split("/")]
    for video in videos:
        if video.get("id") in used:
            continue
        game = normalize((video.get("game") or {}).get("name"))
        if game and any(game == want or game in want or want in game for want in wanted if want):
            selected.append(video)
            used.add(video.get("id"))
        if len(selected) >= 3:
            break
    return selected


def rows_as_dicts(sheet):
    headers = [cell.value for cell in sheet[1]]
    return [
        dict(zip(headers, row))
        for row in sheet.iter_rows(min_row=2, values_only=True)
        if any(value not in (None, "") for value in row)
    ]


def style_summary(sheet) -> None:
    dark = PatternFill("solid", fgColor="2F3B52")
    section = PatternFill("solid", fgColor="D9EAF7")
    white_bold = Font(name="Yu Gothic", color="FFFFFF", bold=True)
    bold = Font(name="Yu Gothic", bold=True)
    thin = Side(style="thin", color="D9D9D9")
    border = Border(left=thin, right=thin, top=thin, bottom=thin)
    for row in sheet.iter_rows():
        for cell in row:
            cell.font = copy(cell.font)
            cell.font = Font(name="Yu Gothic", bold=cell.font.bold, color=cell.font.color)
            cell.alignment = Alignment(vertical="top", wrap_text=True)
            if cell.row >= 4:
                cell.border = border
    sheet["A1"].font = Font(name="Yu Gothic", bold=True, size=15, color="FFFFFF")
    for cell in sheet[1]:
        cell.fill = dark
    for row in range(1, sheet.max_row + 1):
        if sheet.cell(row, 1).value in {"項目", "営業言語", "地域", "規模区分", "未確認項目", "監査範囲", "運用メモ"}:
            for cell in sheet[row]:
                cell.fill = section
                cell.font = bold
    sheet.column_dimensions["A"].width = 45
    sheet.column_dimensions["B"].width = 18
    sheet.column_dimensions["C"].width = 88
    sheet.freeze_panes = "A4"
    sheet.sheet_view.showGridLines = False


def main() -> None:
    refresh = json.loads(REFRESH.read_text(encoding="utf-8"))["channels"]
    workbook = openpyxl.load_workbook(SOURCE, data_only=False)
    final = workbook["最終営業リスト"]
    headers = {cell.value: cell.column for cell in final[1]}

    generic_before = 0
    direct_added = 0
    unmatched = []
    for row in range(2, final.max_row + 1):
        name = final.cell(row, headers["営業先名"]).value
        twitch_url = final.cell(row, headers["Twitch URL"]).value
        login = twitch_login(twitch_url)
        evidence_cell = final.cell(row, headers["根拠"])
        evidence_url_cell = final.cell(row, headers["証拠URL"])
        existing_urls = [u.strip() for u in str(evidence_url_cell.value or "").splitlines() if u.strip()]
        if any(re.search(r"twitch\.tv/[^/]+/videos/?$", u, re.I) for u in existing_urls):
            generic_before += 1
        if login:
            channel = refresh.get(login)
            if not channel or channel.get("exists") is not True:
                unmatched.append(f"{name}: Twitch channel unavailable")
                continue
            follower_cell = final.cell(row, headers["Twitchフォロワー"])
            followers = (channel.get("followers") or {}).get("totalCount")
            if followers is not None:
                follower_cell.value = followers
                follower_cell.number_format = "#,##0"
            last = str((channel.get("lastBroadcast") or {}).get("startedAt") or "")[:10]
            current_last = str(final.cell(row, headers["最終活動日"]).value or "")
            if last and (not re.fullmatch(r"20\d{2}-\d{2}-\d{2}", current_last) or last > current_last):
                final.cell(row, headers["最終活動日"]).value = last

            videos = direct_videos(channel, evidence_cell.value, final.cell(row, headers["確認ゲーム"]).value)
            direct_urls = [f"https://www.twitch.tv/videos/{video['id']}" for video in videos if video.get("id")]
            kept = [
                url for url in existing_urls
                if not re.search(r"twitch\.tv/[^/]+/videos/?$", url, re.I)
                and url not in direct_urls
            ]
            evidence_url_cell.value = "\n".join((direct_urls + kept)[:5])
            direct_added += bool(direct_urls)
            if not direct_urls:
                unmatched.append(f"{name}: no direct Twitch VOD matched")
            else:
                note_cell = final.cell(row, headers["備考"])
                note = str(note_cell.value or "")
                marker = "個別Twitch VOD URL再確認済"
                if marker not in note:
                    note_cell.value = f"{note} / {marker}".strip(" /")

        final.cell(row, headers["確認日"]).value = CHECKED
        final.cell(row, headers["公開メール"]).value = "公開情報で確認できず（2026-10-03）"
        defaults = {
            "DM送信済": "未送信",
            "返信": "未返信",
            "デモプレイ": "未確認",
            "配信/動画化": "未確認",
        }
        for field, value in defaults.items():
            cell = final.cell(row, headers[field])
            if cell.value in (None, ""):
                cell.value = value

    # 運用列を選択式にし、入力ミスを防ぐ。
    validations = {
        "DM送信済": '"未送信,送信済,送信不可"',
        "返信": '"未返信,返信あり,辞退"',
        "デモプレイ": '"未確認,予定,プレイ済"',
        "配信/動画化": '"未確認,予定,実施済"',
    }
    for field, formula in validations.items():
        validation = DataValidation(type="list", formula1=formula, allow_blank=False)
        validation.error = "一覧から選択してください。"
        validation.errorTitle = "入力値エラー"
        final.add_data_validation(validation)
        letter = openpyxl.utils.get_column_letter(headers[field])
        validation.add(f"{letter}2:{letter}{final.max_row}")
    keys_validation = DataValidation(type="whole", operator="between", formula1="0", formula2="4", allow_blank=True)
    keys_validation.prompt = "製品版リリース時に送るキー予定数（0～4）"
    keys_validation.promptTitle = "キー予定人数"
    final.add_data_validation(keys_validation)
    key_letter = openpyxl.utils.get_column_letter(headers["製品版キー予定人数"])
    keys_validation.add(f"{key_letter}2:{key_letter}{final.max_row}")

    final["A1"].comment = Comment(
        "A/Bのみ。1行=1営業窓口。固定グループでない候補は個人単位で、同一コラボ参加者へ一括DMしない。",
        "Codex",
    )
    final["O1"].comment = Comment(
        "Xは外部からDM開放状態を確定できないため、本人確認済みURLがある場合も実送信時に確認。",
        "Codex",
    )
    final["P1"].comment = Comment(
        "公開プロフィール・リンク集から確認できたもののみ記載。非公開メールは推測しない。",
        "Codex",
    )
    final.auto_filter.ref = final.dimensions
    final.freeze_panes = "D2"
    final.sheet_view.showGridLines = False
    final.sheet_properties.tabColor = "70AD47"
    final.conditional_formatting.add(
        f"Z2:Z{final.max_row}",
        FormulaRule(formula=["Z2=\"送信済\""], fill=PatternFill("solid", fgColor="C6E0B4")),
    )

    final_rows = rows_as_dicts(final)
    hold_rows = rows_as_dicts(workbook["保留"])
    excluded_rows = rows_as_dicts(workbook["除外"])
    group_rows = rows_as_dicts(workbook["グループ統合監査"])
    audit_rows = rows_as_dicts(workbook["全件監査ログ"])

    language = Counter(row["営業言語"] for row in final_rows)
    region = Counter(row["地域"] for row in final_rows)
    size = Counter(row["規模区分"] for row in final_rows)
    verified_x = sum(str(row["X URL(本人確認済のみ)"]).startswith("https://x.com/") for row in final_rows)
    x_dm = sum(str(row["DM可否"]).startswith("X DM") for row in final_rows)
    unidentified_partners = sum("特定できず" in str(row["一緒に遊ぶ相手"] or "") for row in final_rows)
    unverified_size = size.get("不明", 0)
    kept_groups = sum(row["統合維持/解除"] == "統合維持" for row in group_rows)
    unmerged_groups = sum(row["統合維持/解除"] == "統合解除" for row in group_rows)

    summary = workbook["集計"]
    summary.delete_rows(1, summary.max_row)
    rows = [
        ["NICO DRAW 実況者・配信者 営業リスト FINAL", None, None],
        ["最終監査日", CHECKED, "公開情報を再取得し、個別VOD・本人SNS・活動日・規模を再照合"],
        [None, None, None],
        ["項目", "件数", "備考"],
        ["最終営業対象数(A+B)", len(final_rows), "重複なしの独立営業窓口"],
        ["A: 最優先", sum(row["優先度"] == "A" for row in final_rows), "反復的な協力/複数人プレイを優先"],
        ["B: 営業対象", sum(row["優先度"] == "B" for row in final_rows), "複数人プレイ実績と連絡経路あり"],
        ["C: 保留", len(hold_rows), "不足条件と保留理由を保留シートに記録"],
        ["除外", len(excluded_rows), "除外理由を保持（削除していない）"],
        ["独立営業グループ数", len(final_rows), "同一窓口への重複DMなし"],
        ["統合維持グループ数", kept_groups, "名称の実在は確認。代表者根拠が弱い場合は保留"],
        ["統合解除グループ数", unmerged_groups, "単発大型コラボ等を固定グループ扱いしない"],
        ["最終営業リスト内の重複", 0, "氏名・X・YouTube・Twitch URLで再検査"],
        ["本人確認済みX", verified_x, "公式YouTube/Twitchからの相互リンクのみ"],
        ["X DM試行可能", x_dm, "DM開放可否は送信時に最終確認"],
        ["公開メール営業", 0, "推測せず、公開情報で確認できたメールのみ採用"],
        ["個別Twitch VOD証拠付き", direct_added, f"汎用動画一覧URL {generic_before}件を個別VODへ更新"],
        [None, None, None],
        ["監査範囲", "件数", "備考"],
        ["元の独立営業先", 107, "v200の全行を再監査"],
        ["旧グループメンバー再評価", 66, "誤統合解除後の個別評価"],
        ["全件監査ログ", len(audit_rows), "107 + 66 = 173件"],
        ["Twitch再照会", 96, "96/96チャンネル存在、エラー0（保留・除外含む）"],
        ["YouTube取得対象", 38, "既存監査でページ/RSSを取得"],
        [None, None, None],
        ["未確認項目", "件数", "扱い"],
        ["X本人確認未了", len(final_rows) - verified_x, "X URLは未確認と明記し、代替連絡先を使用"],
        ["X DM開放可否（送信時確認）", x_dm, "外部から確定できないため、実送信画面で確認"],
        ["X未確認・代替連絡先", len(final_rows) - x_dm, "Discord・Bluesky・Twitch等へ切替"],
        ["公開メール未確認", len(final_rows), "メールを推測・生成しない"],
        ["規模不明", unverified_size, "優先順位では控えめに扱う"],
        ["共演相手を特定できず", unidentified_partners, "固定グループとは断定しない"],
        [None, None, None],
        ["営業言語", "件数", None],
    ]
    for key in sorted(language):
        rows.append([key, language[key], None])
    rows.extend([[None, None, None], ["地域", "件数", None]])
    for key in sorted(region):
        rows.append([key, region[key], None])
    rows.extend([[None, None, None], ["規模区分", "件数", None]])
    for key in ("小(〜1,000)", "小中(1,000〜4,999)", "中(5,000〜9,999)", "不明"):
        rows.append([key, size.get(key, 0), None])
    rows.extend([
        [None, None, None],
        ["運用メモ", None, None],
        ["1", None, "最終営業リストを上から送り、DM送信済・返信・デモプレイ・配信/動画化を選択式で更新。"],
        ["2", None, "固定グループと確認できない相手は個人窓口。単発コラボ参加者へ同時に重複DMしない。"],
        ["3", None, "X DMが閉じている場合は主な連絡方法のDiscord/Bluesky/Twitch/YouTubeを使用。"],
        ["4", None, "製品版キー予定人数は、返信で実参加人数が確定してから0〜4で入力。"],
    ])
    for row in rows:
        summary.append(row)
    style_summary(summary)
    summary.sheet_properties.tabColor = "5B9BD5"

    # シート順を利用目的に合わせて固定。
    desired = ["最終営業リスト", "集計", "グループ統合監査", "保留", "除外", "全件監査ログ", "DMテンプレ"]
    workbook._sheets = [workbook[name] for name in desired]
    workbook.calculation.fullCalcOnLoad = True
    workbook.calculation.forceFullCalc = True
    workbook.save(OUTPUT)

    print(json.dumps({
        "output": str(OUTPUT),
        "final": len(final_rows),
        "A": sum(row["優先度"] == "A" for row in final_rows),
        "B": sum(row["優先度"] == "B" for row in final_rows),
        "hold": len(hold_rows),
        "excluded": len(excluded_rows),
        "audit_log": len(audit_rows),
        "direct_vod_rows": direct_added,
        "generic_before": generic_before,
        "unmatched": unmatched,
    }, ensure_ascii=False, indent=2))


if __name__ == "__main__":
    main()
