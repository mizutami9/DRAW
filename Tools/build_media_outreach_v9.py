from __future__ import annotations

import argparse
import copy
from collections import Counter
from datetime import date
from pathlib import Path

import openpyxl
from openpyxl import Workbook
from openpyxl.formatting.rule import FormulaRule
from openpyxl.styles import Alignment, Border, Font, PatternFill, Side
from openpyxl.utils import get_column_letter
from openpyxl.worksheet.datavalidation import DataValidation
from openpyxl.workbook.properties import CalcProperties


STEAM_URL = "https://store.steampowered.com/app/5090120/NICO_DRAW/"
TRAILER_URL = "https://www.youtube.com/watch?v=Avq8BBCJ56w"
VERIFIED_ON = "2026-10-03"

NAVY = "1F4E78"
BLUE = "D9EAF7"
PALE_BLUE = "EAF3F8"
GREEN = "E2F0D9"
YELLOW = "FFF2CC"
ORANGE = "FCE4D6"
RED = "F4CCCC"
GRAY = "E7E6E6"
WHITE = "FFFFFF"
INK = "1F1F1F"
LINK = "0563C1"

THIN_GRAY = Side(style="thin", color="B7C9D6")
MEDIUM_NAVY = Side(style="medium", color=NAVY)


ADDED_MEDIA = [
    [
        53,
        "TheSixthAxis",
        "英国/英語圏",
        "英語",
        "https://www.thesixthaxis.com/",
        "https://www.thesixthaxis.com/staff/",
        "公式Staff/Contactの出版社・開発者向け窓口",
        "公式Staff/Contactでプレビュー/レビューコードとプレスリリースを受付。2026年9月にもSteamデモ付き小規模作品をプレスリリース出典で単独掲載。",
        "https://www.thesixthaxis.com/2026/09/09/feed-a-monstrosity-this-november-when-in-full-bloom-launches/",
        "デモ公開をニュース化する実績が直近にある。描いた身体で能力が変わる点と最大4人オンライン協力を冒頭で提示。",
        "未送信", "未確認", None, "未送信", "未送信",
        "公開メール文字列は送信直前に公式Staff/Contactで確認。推測しない。",
        "高", "第1陣", "公式Staff/Contactの送付先を当日に再確認",
    ],
    [
        54,
        "GodisaGeek.com",
        "英国/英語圏",
        "英語",
        "https://godisageek.com/",
        "pr@godisageek.com（公式Contact: https://godisageek.com/contact-us/）",
        "メール",
        "公式Contactが開発者/パブリッシャーからのプレスリリース、ニュース、素材、レビューコードの受付を明示。2026年もPC/インディーのニュースとレビューを継続。",
        "https://godisageek.com/contact-us/",
        "窓口が明確で初回評価に必要な情報も指定済み。Steam、発売予定、Press Kit、短尺動画を1通に集約。",
        "未送信", "未確認", None, "未送信", "未送信",
        "公式は@godisageek.com以外からの連絡を詐欺として注意喚起。返信元ドメインを確認。",
        "高", "第1陣", "Steam・発売予定・Press Kit・短尺動画を同封",
    ],
    [
        55,
        "COGconnected",
        "北米/英語",
        "英語",
        "https://cogconnected.com/",
        "trevor@cogconnected.com（公式Contact: https://cogconnected.com/contact/）",
        "メール",
        "公式ContactがPublishers/Developers/PR、Review Materials、News Tipsを受付。2026年3月に色彩と創作性を前面に出したSteamデモを単独掲載。",
        "https://cogconnected.com/2026/03/inkremental-splashes-its-colors-with-the-release-of-steam-demo/",
        "『描く』『色』『短時間で仕組みが伝わるSteamデモ』の相性が高い。GIFまたは短い動画を本文上部へ。",
        "未送信", "未確認", None, "未送信", "未送信",
        "公式の編集情報提供メールを確認済み。",
        "高", "第1陣", "英語本文＋GIF/動画＋Press Kit",
    ],
    [
        56,
        "Use a Potion!",
        "英国/英語圏",
        "英語",
        "https://www.useapotion.com/",
        "https://www.useapotion.com/contact-us/",
        "Contact form",
        "公式Review PolicyでPR/パブリッシャー提供キーを含むゲームを実際にプレイしてレビューすると明記。2026年もインディーを含むレビューを継続。",
        "https://www.useapotion.com/use-a-potion-review-policy/",
        "デモ速報よりレビュー/インプレッション向き。第1陣の反応後、デモ試遊または製品版レビュー打診で使う。",
        "未送信", "未確認", None, "未送信", "未送信",
        "スポンサー記事は受け付けない方針。編集レビュー依頼として送る。",
        "中", "第2陣", "広告依頼ではなくレビュー/試遊依頼と明記",
    ],
    [
        57,
        "Vandal",
        "スペイン/スペイン語",
        "スペイン語",
        "https://vandal.elespanol.com/",
        "noticias@vandal.net（公式Redacción）",
        "メール",
        "公式Redacciónで『notas de prensa』専用メールを公開。2026年6月に3人オンライン協力パズルのSteam Next Festデモを独立記事で詳しく紹介。",
        "https://vandal.elespanol.com/noticia/1350791212/un-juego-muy-mono-que-es-la-bomba-este-cooperativo-ha-explotado-en-el-steam-next-fest-por-su-su-originalidad/",
        "NICO DRAWと同じオンライン協力パズル/笑える失敗/配信映えの文脈が強い。スペイン語ローカライズ対応も明記。",
        "未送信", "未確認", None, "未送信", "未送信",
        "窓口根拠: https://vandal.elespanol.com/estaticos/redaccion",
        "高", "第1陣", "スペイン語全文＋Steam＋動画＋Press Kit",
    ],
    [
        58,
        "3DJuegos",
        "スペイン/スペイン語",
        "スペイン語",
        "https://www.3djuegos.com/",
        "3djuegos-redaccion@webedia-group.com（prensa@3djuegos.com）",
        "メール",
        "公式Contactoがプレスリリース専用メールを公開。2026年もSteam Next Festのデモ選定記事を継続掲載。",
        "https://www.3djuegos.com/pc/noticias/nuevo-festival-steam-esta-aqui-aqui-tienes-9-juegos-a-que-puedes-jugar-next-fest-evento-demos-valve/amp",
        "大型媒体だが窓口が明確。『描くことが操作性能になる』『4人オンライン協力』という見出し化しやすい事実に絞る。",
        "未送信", "未確認", None, "未送信", "未送信",
        "窓口根拠: https://www.3djuegos.com/contacto",
        "高", "第1陣", "スペイン語件名。本文は短く、素材を即利用可能に",
    ],
    [
        59,
        "Overloadr",
        "ブラジル",
        "ブラジルポルトガル語",
        "https://www.overloadr.com.br/",
        "https://www.overloadr.com.br/contato",
        "Contact form / equipe公開連絡先",
        "公式Contactが企画提案（sugestões de pauta）を受付。2026年もブラジル産を含む独自性のあるインディー作品を番組・記事で継続的に扱う。",
        "https://www.overloadr.com.br/podcasts/2026/9/motherchip-591-marvels-wolverine-magical-blush-blood-dawnwalker",
        "速報転載型より独自性/体験重視。描画が能力と攻略に直結する設計思想を短く説明し、第2陣で個別提案。",
        "未送信", "未確認", None, "未送信", "未送信",
        "一般連絡先は公式Contactから。個人メールへの一斉送信は避ける。",
        "中", "第2陣", "ポルトガル語で個別化し、企画提案として送る",
    ],
    [
        60,
        "Jogando Casualmente",
        "ブラジル",
        "ブラジルポルトガル語",
        "https://jogandocasualmente.com.br/",
        "contato@jogandocasualmente.com.br（公式Sobre nós）",
        "メール",
        "公式Aboutでインディー企業提供コードによるレビューを明示。2026年2月に1〜4人オンライン協力インディーのSteam無料プロローグを実プレイPreview化。",
        "https://jogandocasualmente.com.br/preview-bus-bro-u-survived-pc/",
        "最大4人オンライン協力とパズルの実プレイ記事に直結。小規模媒体で返信期待値も比較的高い。",
        "未送信", "未確認", None, "未送信", "未送信",
        "窓口根拠: https://jogandocasualmente.com.br/sobre-nos/",
        "高", "第1陣", "ポルトガル語本文＋デモURL＋動画。レビュー依頼を明確に",
    ],
    [
        61,
        "Gamereactor 中文版",
        "台湾/繁体字圏",
        "繁体字中国語",
        "https://www.gamereactor.cn/",
        "https://www.gamereactor.cn/contact/",
        "公式Contactの編集部窓口",
        "公式Contactがニュース情報、レビュー依頼、ゲーム情報、取材提案、素材を明示受付。2026年4月にSteamの協力パズル作品デモを単独掲載。",
        "https://www.gamereactor.cn/you-can-now-play-a-demo-for-we-were-here-tomorrow-1288273/",
        "Steamデモ＋協力パズルがそのまま掲載実績に合う。繁体字本文で日本発、4人オンライン、手描き能力変化を明記。",
        "未送信", "未確認", None, "未送信", "未送信",
        "公開メールは送信時に公式Contactで確認し、表示が保護される場合はフォーム/記載導線を使用。",
        "高", "第1陣", "繁体字全文＋Steam＋動画＋Press Kit",
    ],
    [
        62,
        "게임뷰 (GameVU)",
        "韓国",
        "韓国語",
        "https://www.gamevu.co.kr/",
        "記事記者メール（例: hyeonkim4@gamevu.co.kr）/公式記事導線",
        "記者メール",
        "2026年9月に韓国インディーの2Dパズルアクションを詳細取材し、同月には4人ローカル協力の実機記事も掲載。記事上で記者メールを公開。",
        "https://www.gamevu.co.kr/news/articleView.html?idxno=61031",
        "韓国インディー/パズルへの関心が直近にあり適合。編集部の代表窓口が見つかればそちらを優先。",
        "未送信", "未確認", None, "未送信", "未送信",
        "個別記者への大量送信は避け、関連担当1名へ個別化。4人協力実績: https://www.gamevu.co.kr/news/articleView.html?idxno=61060",
        "高", "第1陣", "韓国語本文。記者名・担当分野を当日に再確認",
    ],
]


def fill(color: str) -> PatternFill:
    return PatternFill("solid", fgColor=color)


def base_border() -> Border:
    return Border(left=THIN_GRAY, right=THIN_GRAY, top=THIN_GRAY, bottom=THIN_GRAY)


def set_hyperlink(cell, value: str | None) -> None:
    if not isinstance(value, str) or not value.startswith("http"):
        return
    cell.hyperlink = value
    cell.font = copy.copy(cell.font)
    cell.font = Font(
        name=cell.font.name or "Yu Gothic",
        size=cell.font.sz or 10,
        bold=cell.font.b,
        italic=cell.font.i,
        color=LINK,
        underline="single",
    )


def style_header(ws, row: int, start: int, end: int, color: str = NAVY) -> None:
    for col in range(start, end + 1):
        c = ws.cell(row, col)
        c.fill = fill(color)
        c.font = Font(name="Yu Gothic", size=10, bold=True, color=WHITE)
        c.alignment = Alignment(horizontal="center", vertical="center", wrap_text=True)
        c.border = base_border()


def style_table(ws, min_row: int, max_row: int, min_col: int, max_col: int) -> None:
    for row in ws.iter_rows(min_row=min_row, max_row=max_row, min_col=min_col, max_col=max_col):
        for c in row:
            c.font = Font(name="Yu Gothic", size=10, color=INK)
            c.alignment = Alignment(vertical="top", wrap_text=True)
            c.border = base_border()


def add_title(ws, title: str, end_col: int) -> None:
    ws.merge_cells(start_row=1, start_column=1, end_row=1, end_column=end_col)
    c = ws.cell(1, 1, title)
    c.fill = fill(NAVY)
    c.font = Font(name="Yu Gothic", size=16, bold=True, color=WHITE)
    c.alignment = Alignment(horizontal="left", vertical="center")
    ws.row_dimensions[1].height = 30


def setup_page(ws, landscape: bool = True) -> None:
    ws.sheet_view.showGridLines = False
    ws.page_setup.orientation = "landscape" if landscape else "portrait"
    ws.page_setup.fitToWidth = 1
    ws.page_setup.fitToHeight = 0
    ws.sheet_properties.pageSetUpPr.fitToPage = True
    ws.freeze_panes = "A2"


def add_media_rows(wb: Workbook) -> None:
    ws = wb["メディア営業リスト"]
    template_row = 53
    for offset, values in enumerate(ADDED_MEDIA, start=54):
        for col in range(1, 20):
            src = ws.cell(template_row, col)
            dst = ws.cell(offset, col)
            if src.has_style:
                dst._style = copy.copy(src._style)
            if src.number_format:
                dst.number_format = src.number_format
            dst.alignment = copy.copy(src.alignment)
        for col, value in enumerate(values, start=1):
            ws.cell(offset, col, value)
        ws.row_dimensions[offset].height = 84

    actual_last = 1 + len([r for r in ws.iter_rows(min_row=2) if r[1].value])
    ws.freeze_panes = "A2"
    ws.auto_filter.ref = f"A1:S{actual_last}"
    ws.sheet_view.showGridLines = False
    ws.row_dimensions[1].height = 40

    # Clickable links for site/evidence URLs.
    for row in range(2, actual_last + 1):
        set_hyperlink(ws.cell(row, 5), ws.cell(row, 5).value)
        set_hyperlink(ws.cell(row, 9), ws.cell(row, 9).value)

    # Confidence and send-order dropdowns.
    existing_ranges = {str(dv.sqref) for dv in ws.data_validations.dataValidation}
    if "Q2:Q200" not in existing_ranges:
        dv_conf = DataValidation(type="list", formula1='"高,中,低"', allow_blank=True)
        ws.add_data_validation(dv_conf)
        dv_conf.add("Q2:Q200")
    if "R2:R200" not in existing_ranges:
        dv_order = DataValidation(type="list", formula1='"第1陣,第2陣,保留,別枠"', allow_blank=True)
        ws.add_data_validation(dv_order)
        dv_order.add("R2:R200")

    ws.conditional_formatting.add(
        f"R2:R{actual_last}", FormulaRule(formula=["$R2=\"第1陣\""], fill=fill(GREEN))
    )
    ws.conditional_formatting.add(
        f"R2:R{actual_last}", FormulaRule(formula=["$R2=\"第2陣\""], fill=fill(YELLOW))
    )
    ws.conditional_formatting.add(
        f"R2:R{actual_last}", FormulaRule(formula=["$R2=\"保留\""], fill=fill(GRAY))
    )
    ws.conditional_formatting.add(
        f"R2:R{actual_last}", FormulaRule(formula=["$R2=\"別枠\""], fill=fill(PALE_BLUE))
    )
    ws.conditional_formatting.add(
        f"S2:S{actual_last}", FormulaRule(formula=["ISNUMBER(SEARCH(\"再確認\",$S2))"], fill=fill(YELLOW))
    )
    ws.auto_filter.ref = f"A1:S{actual_last}"
    setup_page(ws)


def update_existing_sheets(wb: Workbook) -> None:
    ws = wb["方針_送信タイミング"]
    ws["D9"] = "発売予定"
    ws["E9"] = "2026年12月"
    ws["F9"] = "Steam公式ページ確認（2026-10-03）"
    ws["D10"] = "開発 / 販売"
    ws["E10"] = "Chaos Cat Games"
    ws["F10"] = "Steam公式ページと一致"
    set_hyperlink(ws["E4"], ws["E4"].value)
    ws.sheet_view.showGridLines = False

    ws = wb["プレスキット準備"]
    ws["B2"] = "一部確認済"
    ws["C2"] = "NICO DRAW / Chaos Cat Games / 2D協力パズルアクション / 最大4人オンライン / Windows / 2026年12月予定 / Steam"
    ws["E2"] = STEAM_URL
    set_hyperlink(ws["E2"], ws["E2"].value)
    ws["B7"] = "確認済"
    set_hyperlink(ws["E7"], ws["E7"].value)
    additions = [
        ["Steamページ", "確認済", STEAM_URL, "必須", "発売予定2026年12月、Online Co-op、Windows表記を確認"],
        ["対応言語表", "確認済", "Steam上では日本語・英語・韓国語・繁体字・簡体字・スペイン語・ブラジル葡語を含む103言語のUI対応表記", "必須", "送信時は対象媒体の言語だけを強調"],
        ["デモ公開状態", "要最終確認", "Steamストアで一般ユーザーにDemoボタンが見えること", "必須", "Web取得時はPlaytest導線が表示されたため、営業送信直前に未ログイン環境でも確認"],
        ["デモのオンライン協力", "要最終確認", "別Steamアカウント・別回線の2台以上で接続確認", "必須", "媒体へ『デモでも最大4人』と書く前のP0確認"],
        ["プレスキット公開URL", "未着手", "閲覧権限不要の公開URL", "必須", "Google Driveの場合はログアウト状態でダウンロード確認"],
        ["メディア問い合わせ先", "未着手", "返信監視できるChaos Cat Gamesのメール", "必須", "個人メールより専用アドレス推奨"],
    ]
    start = 14
    for r, vals in enumerate(additions, start=start):
        for c, value in enumerate(vals, start=1):
            ws.cell(r, c, value)
        for c in range(1, 6):
            ws.cell(r, c)._style = copy.copy(ws.cell(13, c)._style)
            ws.cell(r, c).alignment = Alignment(vertical="top", wrap_text=True)
        ws.row_dimensions[r].height = 56
    ws.auto_filter.ref = "A1:E19"
    ws.freeze_panes = "A2"
    ws.sheet_view.showGridLines = False

    ws = wb["プレスリリース骨子"]
    ws["B3"] = "Chaos Cat Gamesは、最大4人オンライン協力パズルアクション『NICO DRAW』の無料デモをSteamで公開しました。製品版は2026年12月発売予定です。"
    ws["B6"] = "全8ステージ、想定プレイ時間1〜2時間。デモでオンライン協力が利用できることを送信前に実機確認する。"
    ws["B7"] = "Steam Next Festの参加回・開催期間が公式に確定した場合だけ、正式名称と日付を明記。"
    ws["B8"] = f"Steam: {STEAM_URL} / Trailer: {TRAILER_URL} / Press Kit: [公開URL]"
    ws.sheet_view.showGridLines = False

    ws = wb["追加調査キュー"]
    updates = {
        2: ("主要枠充足", "13件。まず送信し、反応率を見て追加調査"),
        3: ("重点継続", "4件。Gamereactor中文版を追加。Gamebaseは編集情報提供窓口が未確認のためキュー維持"),
        4: ("重点継続", "6件。GameVUを追加。代表編集部窓口を引き続き確認"),
        5: ("主要枠充足", "30件。TheSixthAxis / GodisaGeek / COGconnected / Use a Potionを追加"),
        6: ("主要枠充足", "4件。Vandal / 3DJuegosを追加。まず反応率を測る"),
        7: ("主要枠充足", "4件。Overloadr / Jogando Casualmenteを追加"),
    }
    for row, (status, memo) in updates.items():
        ws.cell(row, 6, status)
        ws.cell(row, 7, memo)
    extra = [
        ["台湾", "遊戲基地 Gamebase", 1, "直近のSteamデモ掲載は強いが、編集部向け正式窓口を確認できた場合のみ本表へ昇格", "繁体字中国語", "窓口確認待ち", "客服メールは編集情報提供先として使わない"],
        ["スペイン語圏", "VidaoPantalla等", 2, "直近1年の小規模Steamデモ記事と編集部窓口の両方が必要", "スペイン語", "追加余地", "Vandal/3DJuegos/JUEGONEXT/INAGAMEの反応後に判断"],
        ["ブラジル", "追加小規模媒体", 2, "レビューコード受付だけでなくPC/Steamデモの直近実績を優先", "ブラジルポルトガル語", "追加余地", "4媒体へ送信後、開封/返信率で増減"],
    ]
    for r, vals in enumerate(extra, start=8):
        for c, value in enumerate(vals, start=1):
            ws.cell(r, c, value)
        for c in range(1, 8):
            ws.cell(r, c)._style = copy.copy(ws.cell(7, c)._style)
            ws.cell(r, c).alignment = Alignment(vertical="top", wrap_text=True)
    ws.auto_filter.ref = "A1:G10"
    ws.freeze_panes = "A2"
    ws.sheet_view.showGridLines = False

    ws = wb["調査知見"]
    rows = [
        ["完全版の判定基準", "公式窓口または公式Contact導線と、2025〜2026年の小規模インディー/Steamデモ掲載根拠の両方を基本条件にした。片方だけの媒体は第2陣か調査キュー。"],
        ["公開メールの扱い", "検索結果や第三者名簿から推測しない。公式ページに表示されない/保護される場合は公式Contact URLを記録し、送信当日に確認する。"],
        ["デモ表示の注意", "SteamページのWeb取得ではPlaytest導線が表示された。プレス送信前に、一般ユーザー環境でDemoボタンとオンライン協力が使えることを必ず確認する。"],
        ["多言語", "媒体の主言語で件名と本文を用意し、Steamの当該言語対応を明記する。本文の固有名詞・人数・発売予定はSteam表記と一致させる。"],
    ]
    for r, vals in enumerate(rows, start=11):
        ws.cell(r, 1, vals[0])
        ws.cell(r, 2, vals[1])
        for c in range(1, 3):
            ws.cell(r, c)._style = copy.copy(ws.cell(10, c)._style)
            ws.cell(r, c).alignment = Alignment(vertical="top", wrap_text=True)
        ws.row_dimensions[r].height = 44
    ws.auto_filter.ref = "A1:B14"
    ws.freeze_panes = "A2"
    ws.sheet_view.showGridLines = False

    for name in ["配信_PR経路"]:
        ws = wb[name]
        ws.freeze_panes = "A2"
        ws.auto_filter.ref = f"A1:H{ws.max_row}"
        ws.sheet_view.showGridLines = False


def create_dashboard(wb: Workbook, media_rows: list[list]) -> None:
    if "営業ダッシュボード" in wb.sheetnames:
        del wb["営業ダッシュボード"]
    ws = wb.create_sheet("営業ダッシュボード", 0)
    add_title(ws, "NICO DRAW メディア営業ダッシュボード — 完全版 v9", 8)
    ws["A2"] = f"更新日: {VERIFIED_ON}　確定情報はSteam公式ページと照合。媒体の窓口・掲載根拠は送信直前にも再確認。"
    ws.merge_cells("A2:H2")
    ws["A2"].fill = fill(PALE_BLUE)
    ws["A2"].font = Font(name="Yu Gothic", size=10, italic=True, color=NAVY)
    ws["A2"].alignment = Alignment(vertical="center")
    ws.row_dimensions[2].height = 24

    order_counts = Counter(r[17] for r in media_rows)
    lang_counts = Counter(r[3] for r in media_rows)
    web_count = len(media_rows) - order_counts.get("別枠", 0)
    metrics = [
        ("調査済み候補", len(media_rows), BLUE),
        ("Web編集媒体", web_count, GREEN),
        ("第1陣", order_counts.get("第1陣", 0), GREEN),
        ("第2陣", order_counts.get("第2陣", 0), YELLOW),
        ("保留", order_counts.get("保留", 0), GRAY),
        ("別枠", order_counts.get("別枠", 0), PALE_BLUE),
    ]
    for i, (label, value, color) in enumerate(metrics):
        col = 1 + i
        ws.cell(4, col, label)
        ws.cell(5, col, value)
        ws.cell(4, col).fill = fill(NAVY)
        ws.cell(4, col).font = Font(name="Yu Gothic", size=10, bold=True, color=WHITE)
        ws.cell(4, col).alignment = Alignment(horizontal="center")
        ws.cell(5, col).fill = fill(color)
        ws.cell(5, col).font = Font(name="Yu Gothic", size=18, bold=True, color=NAVY)
        ws.cell(5, col).alignment = Alignment(horizontal="center")
        ws.cell(4, col).border = base_border()
        ws.cell(5, col).border = base_border()

    ws["A7"] = "NICO DRAW 確定情報"
    ws["D7"] = "営業言語別件数"
    ws["G7"] = "P0 — 送信前ブロッカー"
    for cell in ["A7", "D7", "G7"]:
        ws[cell].fill = fill(NAVY)
        ws[cell].font = Font(name="Yu Gothic", size=11, bold=True, color=WHITE)
    ws.merge_cells("A7:C7")
    ws.merge_cells("D7:F7")
    ws.merge_cells("G7:H7")

    facts = [
        ("開発 / 販売", "Chaos Cat Games"),
        ("発売予定", "2026年12月"),
        ("ジャンル", "手描き2D・最大4人オンライン協力パズルアクション"),
        ("コアフック", "描いた身体と限られたINK配分で能力・攻略法が変わる"),
        ("キャラクター", "Human / Cat / Bird / Turtle / Slime の5種"),
        ("Steam", STEAM_URL),
        ("Trailer", TRAILER_URL),
    ]
    for r, (label, value) in enumerate(facts, start=8):
        ws.cell(r, 1, label)
        ws.merge_cells(start_row=r, start_column=2, end_row=r, end_column=3)
        ws.cell(r, 2, value)
        ws.cell(r, 1).fill = fill(BLUE)
        ws.cell(r, 1).font = Font(name="Yu Gothic", size=10, bold=True, color=NAVY)
        for c in range(1, 4):
            ws.cell(r, c).border = base_border()
            ws.cell(r, c).alignment = Alignment(vertical="top", wrap_text=True)
        set_hyperlink(ws.cell(r, 2), value)

    lang_order = ["日本語", "英語", "韓国語", "繁体字中国語", "スペイン語", "ブラジルポルトガル語", "簡体字中国語"]
    for r, lang in enumerate(lang_order, start=8):
        ws.cell(r, 4, lang)
        ws.cell(r, 5, lang_counts.get(lang, 0))
        ws.merge_cells(start_row=r, start_column=5, end_row=r, end_column=6)
        ws.cell(r, 4).fill = fill(BLUE)
        ws.cell(r, 4).font = Font(name="Yu Gothic", size=10, bold=True, color=NAVY)
        for c in range(4, 7):
            ws.cell(r, c).border = base_border()
            ws.cell(r, c).alignment = Alignment(horizontal="center", vertical="center")

    blockers = [
        ("Demo公開表示", "一般ユーザーのSteam画面でDemoボタン確認"),
        ("Demoマルチ", "別アカウント2台以上でオンライン接続確認"),
        ("Press Kit", "ログイン不要の公開URL作成"),
        ("返信先", "監視できるメディア問い合わせメール確定"),
        ("Next Fest", "参加回・正式名称・開催日を公式確認"),
        ("素材", "横長KV / ロゴ / 画像 / GIFを権利確認"),
        ("表記統一", "人数・発売予定・対応OSをSteamと一致"),
    ]
    for r, (label, value) in enumerate(blockers, start=8):
        ws.cell(r, 7, label)
        ws.cell(r, 8, value)
        ws.cell(r, 7).fill = fill(RED if r <= 11 else YELLOW)
        ws.cell(r, 7).font = Font(name="Yu Gothic", size=10, bold=True, color=NAVY)
        for c in range(7, 9):
            ws.cell(r, c).border = base_border()
            ws.cell(r, c).alignment = Alignment(vertical="top", wrap_text=True)

    ws["A17"] = "最優先で送る12媒体"
    ws.merge_cells("A17:H17")
    ws["A17"].fill = fill(NAVY)
    ws["A17"].font = Font(name="Yu Gothic", size=11, bold=True, color=WHITE)
    priorities = [
        (1, "GamingTrend", "直近に『Steamデモ公開→Next Fest』を単独ニュース化", "英語"),
        (2, "GameSpew", "小規模開発のNext Festデモ＋公式受付", "英語"),
        (3, "Co-Optimus", "協力ゲーム専門。4人オンラインが媒体テーマに直結", "英語"),
        (4, "Alpha Beta Gamer", "無料公開デモを公式Submissionで常時受付", "英語"),
        (5, "TheSixthAxis", "Steamデモ記事＋開発者向けプレス窓口", "英語"),
        (6, "COGconnected", "創作/色彩系Steamデモの単独掲載＋明確な窓口", "英語"),
        (7, "게임조선", "小規模Steamデモを直近掲載、編集部メール公開", "韓国語"),
        (8, "게임동아", "1人開発Steamデモ掲載＋公式情報提供窓口", "韓国語"),
        (9, "인디게임닷컴", "インディー専門＋開発者からの広報受付", "韓国語"),
        (10, "Vandal", "オンライン協力パズルのNext Festデモを詳細掲載", "スペイン語"),
        (11, "Jogando Casualmente", "1〜4人オンライン協力インディーの実プレイPreview", "PT-BR"),
        (12, "Gamereactor 中文版", "Steam協力パズルデモ掲載＋公式編集部窓口", "繁体字"),
    ]
    headers = ["順", "媒体", "選定理由", "言語", "順", "媒体", "選定理由", "言語"]
    for c, value in enumerate(headers, start=1):
        ws.cell(18, c, value)
    style_header(ws, 18, 1, 8)
    for idx, item in enumerate(priorities):
        block = 0 if idx < 6 else 4
        row = 19 + (idx % 6)
        for offset, value in enumerate(item):
            ws.cell(row, 1 + block + offset, value)
        for c in range(1 + block, 5 + block):
            ws.cell(row, c).border = base_border()
            ws.cell(row, c).alignment = Alignment(vertical="top", wrap_text=True)
    for r in range(19, 25):
        ws.row_dimensions[r].height = 42

    ws["A27"] = "運用ルール"
    ws.merge_cells("A27:H27")
    ws["A27"].fill = fill(NAVY)
    ws["A27"].font = Font(name="Yu Gothic", size=11, bold=True, color=WHITE)
    rules = [
        "第1陣は同日に全件送らず、言語別に5〜8件ずつ分けて件名/反応を検証する。",
        "フォローは有力媒体へ1回だけ。新しいGIF、更新、イベント開始など追加情報がある場合に限る。",
        "掲載された媒体は製品版営業の最優先へ昇格し、掲載記事URLを必ず記録する。",
        "送信前に媒体の公式窓口と担当分野を再確認し、公開されていないメールを推測しない。",
        "PR配信サービスとYouTube系はWeb編集媒体の件数・成果指標から分離する。",
    ]
    for r, value in enumerate(rules, start=28):
        ws.cell(r, 1, f"{r - 27}.")
        ws.merge_cells(start_row=r, start_column=2, end_row=r, end_column=8)
        ws.cell(r, 2, value)
        for c in range(1, 9):
            ws.cell(r, c).border = base_border()
            ws.cell(r, c).alignment = Alignment(vertical="top", wrap_text=True)
        ws.cell(r, 1).fill = fill(BLUE)
        ws.cell(r, 1).font = Font(name="Yu Gothic", size=10, bold=True, color=NAVY)

    widths = [8, 20, 42, 18, 12, 20, 42, 38]
    for i, width in enumerate(widths, start=1):
        ws.column_dimensions[get_column_letter(i)].width = width
    setup_page(ws)
    ws.freeze_panes = "A3"


def build_templates() -> list[list[str]]:
    steam = STEAM_URL
    trailer = TRAILER_URL
    shared = "[プレスキットURL] / [送信者名] / [返信先メール] / [担当者名]"
    rows: list[list[str]] = []

    rows.extend([
        ["日本語", "第1波：デモ", "【プレスリリース】描いた身体が攻略法になる。最大4人協力『NICO DRAW』無料デモ公開",
         f"[担当者名]様\n\n突然のご連絡失礼いたします。Chaos Cat Gamesの[送信者名]です。\n\n2026年12月発売予定の、最大4人オンライン協力パズルアクション『NICO DRAW』の無料デモをSteamで公開しました。プレイヤーが描いた胴体・手足・翼・甲羅などの形と、限られたINKの配分によって、走る・滑空する・壁に張り付くといった能力やステージの攻略法が変化します。\n\nデモは全8ステージ、想定1〜2時間です。仲間を持ち上げて投げる、つかまって一緒に移動する、盾になるなど、成功しても失敗しても笑える協力プレイをお楽しみいただけます。\n\nSteam: {steam}\n動画: {trailer}\nプレスキット: [プレスキットURL]\n\n貴媒体の読者に合いそうでしたら、ニュースまたはデモ紹介としてご検討いただけますと幸いです。追加素材・取材・確認事項にはすぐ対応いたします。\n\n[送信者名]\nChaos Cat Games\n[返信先メール]",
         shared + " / Next Fest表記は正式確定時のみ追加", "日本語媒体", "簡潔・事実先行", "Demoボタン、Demoマルチ、Press Kit公開権限"],
        ["日本語", "フォロー（1回のみ）", "Re: 『NICO DRAW』無料デモ — [新情報を一言]",
         f"[担当者名]様\n\n先日お送りした『NICO DRAW』について、[新しいGIF／アップデート／Steam Next Fest開始／反響]が追加されたため、1度だけご連絡いたします。\n\n[新情報を2文以内]\nSteam: {steam}\nプレスキット: [プレスキットURL]\n\nご関心がなければご返信は不要です。ご確認ありがとうございました。\n\n[送信者名] / Chaos Cat Games",
         "[新情報] / [プレスキットURL]", "第1陣の未掲載かつ適合度が高い媒体", "短く・圧をかけない", "初回送信から4〜7営業日、新情報あり"],
        ["日本語", "第2波：製品版", "【発売】描いた身体で協力する『NICO DRAW』、Steamで本日リリース",
         f"[担当者名]様\n\nChaos Cat Gamesは、最大4人オンライン協力パズルアクション『NICO DRAW』を[発売日]にSteamで発売します。価格は[価格]です。\n\n無料デモから[製品版の追加要素3点]を追加し、全[確定ステージ数]ステージを収録します。描いた身体とINK配分で能力が変わり、5種類のキャラクターを組み合わせて複数の攻略法を試せる点が特徴です。\n\nSteam: {steam}\n発売トレーラー: [発売トレーラーURL]\nプレスキット: [プレスキットURL]\nレビューキー: [提供方法]\n\n発売ニュース、レビュー、配信企画をご検討いただけますと幸いです。",
         "[発売日] / [価格] / [追加要素] / [ステージ数] / [キー]", "全媒体。デモ掲載媒体を最優先", "新情報を明確に", "Steamの発売日・価格・内容と完全一致"],
    ])

    rows.extend([
        ["English", "Wave 1: Demo", "[Press] Free Steam demo — draw your body to change your abilities in 4-player online co-op NICO DRAW",
         f"Hi [Name],\n\nI'm [Sender] from Chaos Cat Games, an indie developer in Japan.\n\nWe've released a free Steam demo for NICO DRAW, a hand-drawn online co-op puzzle action game for up to four players, planned for December 2026. Players draw their own body, arms, legs, wings, or shell, and both the shape and the way they spend a limited supply of INK change how the character runs, glides, sticks to walls, and solves each stage.\n\nThe demo contains eight stages and takes roughly 1–2 hours. Players can pick up and throw friends, grab onto each other, carry teammates, and combine five character types in multiple ways—so even failure tends to become part of the fun.\n\nSteam: {steam}\nTrailer: {trailer}\nPress kit: [Press kit URL]\n\nIf this sounds relevant to your readers, we'd be grateful if you considered it for news or demo coverage. I can quickly provide any additional assets, details, or an interview.\n\nBest,\n[Sender]\nChaos Cat Games\n[Reply email]",
         "[Name] / [Sender] / [Reply email] / [Press kit URL]", "English-language media", "Direct and factual", "Public demo, demo multiplayer, press-kit permissions"],
        ["English", "One follow-up only", "Re: NICO DRAW free demo — [one new development]",
         f"Hi [Name],\n\nOne brief follow-up on NICO DRAW because we now have [new GIF / demo update / Steam Next Fest start / concrete milestone].\n\n[Describe the genuinely new information in no more than two sentences.]\nSteam: {steam}\nPress kit: [Press kit URL]\n\nNo reply is needed if it isn't a fit. Thanks for taking a look.\n\n[Sender] / Chaos Cat Games",
         "[New information] / [Press kit URL]", "High-fit outlets that did not cover Wave 1", "Brief, no pressure", "4–7 business days after first email; new information required"],
        ["English", "Wave 2: Full release", "[Launch] Draw your body, change your abilities: NICO DRAW launches on Steam [date]",
         f"Hi [Name],\n\nChaos Cat Games will launch NICO DRAW, its online co-op puzzle action game for up to four players, on Steam on [release date] for [price].\n\nSince the free demo, the full version adds [three verified additions] and includes [confirmed stage count] stages. Players draw their own characters, allocate limited INK to different body parts, and combine five character types to discover multiple solutions together.\n\nSteam: {steam}\nLaunch trailer: [Launch trailer URL]\nPress kit: [Press kit URL]\nReview keys: [Key process]\n\nWe'd be happy to support launch news, reviews, interviews, or multiplayer coverage.\n\nBest,\n[Sender] / Chaos Cat Games",
         "[Date] / [Price] / [Additions] / [Stage count] / [Keys]", "All English media; demo coverage first", "New launch facts first", "Match Steam date, price, content and embargo"],
    ])

    rows.extend([
        ["한국어", "1차: 데모", "[보도자료] 직접 그린 몸이 능력이 된다 — 4인 온라인 협동 퍼즐 액션 NICO DRAW 무료 데모 공개",
         f"안녕하세요, [담당자명] 님.\n\n일본 인디 개발팀 Chaos Cat Games의 [보낸이]입니다.\n\n2026년 12월 출시 예정인 최대 4인 온라인 협동 퍼즐 액션 게임 NICO DRAW의 무료 Steam 데모를 공개했습니다. 플레이어가 몸통, 팔, 다리, 날개, 등껍질을 직접 그리고 한정된 INK를 어디에 배분하느냐에 따라 달리기, 활공, 벽에 붙기 등의 능력과 스테이지 공략법이 달라집니다.\n\n데모에는 8개 스테이지가 포함되어 있으며 예상 플레이 시간은 약 1~2시간입니다. 친구를 들어 던지거나, 서로 매달려 이동하거나, 방패가 되는 등 다섯 종의 캐릭터 능력을 조합해 여러 방식으로 퍼즐을 풀 수 있습니다.\n\nSteam: {steam}\n영상: {trailer}\n프레스 키트: [프레스 키트 URL]\n\n귀 매체 독자에게 맞는 작품이라면 뉴스 또는 데모 소개로 검토해 주시면 감사하겠습니다. 추가 자료, 인터뷰, 확인 사항에는 빠르게 대응하겠습니다.\n\n[보낸이]\nChaos Cat Games\n[회신 이메일]",
         "[담당자명] / [보낸이] / [회신 이메일] / [프레스 키트 URL]", "한국 매체", "간결하고 사실 중심", "공개 데모/멀티플레이/한국어 표기 확인"],
        ["한국어", "후속 1회", "Re: NICO DRAW 무료 데모 — [새로운 소식 한 줄]",
         f"안녕하세요, [담당자명] 님.\n\n앞서 전달드린 NICO DRAW에 [새 GIF / 데모 업데이트 / Steam Next Fest 시작 / 구체적 성과]가 추가되어 한 번만 후속 연락드립니다.\n\n[새로운 정보를 두 문장 이내로 작성]\nSteam: {steam}\n프레스 키트: [프레스 키트 URL]\n\n매체와 맞지 않는 경우 회신하지 않으셔도 됩니다. 확인해 주셔서 감사합니다.\n\n[보낸이] / Chaos Cat Games",
         "[새 정보] / [프레스 키트 URL]", "1차 미게재 고적합 매체", "짧고 부담 없이", "첫 메일 4~7영업일 후, 새 정보가 있을 때만"],
        ["한국어", "2차: 정식 출시", "[출시] 직접 그린 몸으로 협력하는 NICO DRAW, Steam 정식 출시",
         f"안녕하세요, [담당자명] 님.\n\nChaos Cat Games는 최대 4인 온라인 협동 퍼즐 액션 NICO DRAW를 [출시일] Steam에 [가격]으로 출시합니다.\n\n정식 버전에는 무료 데모 이후 [검증된 추가 요소 3개]가 더해졌으며 총 [확정 스테이지 수]개 스테이지를 제공합니다. 직접 그린 몸과 INK 배분에 따라 능력이 달라지고, 다섯 종의 캐릭터를 조합해 여러 공략법을 찾을 수 있습니다.\n\nSteam: {steam}\n출시 트레일러: [URL]\n프레스 키트: [URL]\n리뷰 키: [제공 방법]\n\n출시 뉴스, 리뷰 또는 멀티플레이 콘텐츠로 검토해 주시면 감사하겠습니다.",
         "[출시일] / [가격] / [추가 요소] / [스테이지 수] / [키]", "한국 매체 전체. 데모 기사 매체 우선", "새로운 출시 정보 우선", "Steam 표기 및 엠바고와 일치"],
    ])

    rows.extend([
        ["Español", "Oleada 1: Demo", "[Prensa] Demo gratis en Steam: dibuja tu cuerpo y cambia tus habilidades en el cooperativo online NICO DRAW",
         f"Hola [Nombre]:\n\nSoy [Remitente], de Chaos Cat Games, un estudio indie japonés.\n\nHemos publicado la demo gratuita de NICO DRAW en Steam. Es un juego de acción y puzles cooperativo online para hasta cuatro jugadores, previsto para diciembre de 2026. Cada jugador dibuja el cuerpo, los brazos, las piernas, las alas o el caparazón de su personaje; la forma y la distribución de una cantidad limitada de INK cambian su manera de correr, planear, adherirse a las paredes y resolver cada nivel.\n\nLa demo incluye ocho niveles y dura aproximadamente entre una y dos horas. Los jugadores pueden levantar y lanzar a sus amigos, agarrarse entre sí, transportarlos y combinar cinco tipos de personaje para encontrar distintas soluciones.\n\nSteam: {steam}\nTráiler: {trailer}\nKit de prensa: [URL del kit de prensa]\n\nSi encaja con vuestros lectores, agradeceríamos que la valorarais para una noticia o cobertura de la demo. Podemos facilitar rápidamente más materiales, información o una entrevista.\n\nUn saludo,\n[Remitente]\nChaos Cat Games\n[Correo de respuesta]",
         "[Nombre] / [Remitente] / [Correo] / [Kit]", "Medios en español", "Claro y factual", "Demo pública, multijugador de la demo, kit accesible"],
        ["Español", "Un solo seguimiento", "Re: Demo gratuita de NICO DRAW — [novedad concreta]",
         f"Hola [Nombre]:\n\nUn breve seguimiento sobre NICO DRAW porque acabamos de publicar [nuevo GIF / actualización de la demo / inicio de Steam Next Fest / hito concreto].\n\n[Explica la novedad en un máximo de dos frases.]\nSteam: {steam}\nKit de prensa: [URL]\n\nNo hace falta responder si no encaja con el medio. Gracias por vuestro tiempo.\n\n[Remitente] / Chaos Cat Games",
         "[Novedad] / [Kit]", "Medios prioritarios sin cobertura", "Breve y sin presión", "4–7 días laborables después; solo con novedad real"],
        ["Español", "Oleada 2: Lanzamiento", "[Lanzamiento] Dibuja tu cuerpo y coopera: NICO DRAW llega a Steam el [fecha]",
         f"Hola [Nombre]:\n\nChaos Cat Games lanzará NICO DRAW, su juego de acción y puzles cooperativo online para hasta cuatro jugadores, en Steam el [fecha] por [precio].\n\nDesde la demo gratuita, la versión final añade [tres novedades verificadas] e incluye [número confirmado] niveles. La forma dibujada y la distribución del INK cambian las habilidades, y los cinco tipos de personaje permiten encontrar varias soluciones en equipo.\n\nSteam: {steam}\nTráiler de lanzamiento: [URL]\nKit de prensa: [URL]\nClaves de prensa: [proceso]\n\nQuedamos disponibles para noticias de lanzamiento, análisis, entrevistas o contenido multijugador.",
         "[Fecha] / [Precio] / [Novedades] / [Niveles] / [Claves]", "Todos los medios en español; priorizar quienes cubrieron la demo", "Novedades verificadas primero", "Coincidir con Steam y embargo"],
    ])

    rows.extend([
        ["Português (Brasil)", "1ª onda: Demo", "[Imprensa] Demo grátis na Steam: desenhe o corpo e mude as habilidades no cooperativo online NICO DRAW",
         f"Olá, [Nome].\n\nSou [Remetente], da Chaos Cat Games, um estúdio indie do Japão.\n\nLançamos a demo gratuita de NICO DRAW na Steam. O jogo é uma ação cooperativa online com puzzles para até quatro pessoas e tem lançamento previsto para dezembro de 2026. Cada jogador desenha o corpo, braços, pernas, asas ou casco do personagem; o formato e a distribuição de uma quantidade limitada de INK mudam como ele corre, plana, gruda nas paredes e resolve cada fase.\n\nA demo inclui oito fases e dura aproximadamente de uma a duas horas. É possível levantar e arremessar amigos, agarrar-se a eles, carregar colegas e combinar cinco tipos de personagem para encontrar soluções diferentes.\n\nSteam: {steam}\nTrailer: {trailer}\nPress kit: [URL do press kit]\n\nSe o jogo combinar com o público do veículo, agradeceríamos a consideração para notícia ou preview da demo. Podemos enviar rapidamente materiais adicionais, informações ou entrevista.\n\nObrigado,\n[Remetente]\nChaos Cat Games\n[E-mail para resposta]",
         "[Nome] / [Remetente] / [E-mail] / [Press kit]", "Mídia brasileira", "Direto e factual", "Demo pública, multiplayer da demo, press kit acessível"],
        ["Português (Brasil)", "Um follow-up", "Re: Demo grátis de NICO DRAW — [novidade concreta]",
         f"Olá, [Nome].\n\nUm breve follow-up sobre NICO DRAW porque acabamos de publicar [novo GIF / atualização da demo / início do Steam Next Fest / marco concreto].\n\n[Descreva a novidade em no máximo duas frases.]\nSteam: {steam}\nPress kit: [URL]\n\nNão é necessário responder caso não seja adequado ao veículo. Obrigado pela atenção.\n\n[Remetente] / Chaos Cat Games",
         "[Novidade] / [Press kit]", "Veículos prioritários sem cobertura", "Breve e sem pressão", "4–7 dias úteis depois; somente com informação nova"],
        ["Português (Brasil)", "2ª onda: Lançamento", "[Lançamento] Desenhe o corpo e coopere: NICO DRAW chega à Steam em [data]",
         f"Olá, [Nome].\n\nA Chaos Cat Games lançará NICO DRAW, jogo de ação e puzzles cooperativo online para até quatro pessoas, na Steam em [data], por [preço].\n\nDesde a demo gratuita, a versão final recebeu [três novidades verificadas] e inclui [número confirmado] fases. O desenho do corpo e a distribuição de INK mudam as habilidades, e cinco tipos de personagem podem ser combinados para criar várias soluções em equipe.\n\nSteam: {steam}\nTrailer de lançamento: [URL]\nPress kit: [URL]\nChaves de imprensa: [processo]\n\nEstamos disponíveis para notícias de lançamento, análises, entrevistas ou conteúdo multiplayer.",
         "[Data] / [Preço] / [Novidades] / [Fases] / [Chaves]", "Toda mídia brasileira; priorizar quem cobriu a demo", "Fatos novos primeiro", "Coincidir com Steam e embargo"],
    ])

    rows.extend([
        ["繁體中文", "第一波：Demo", "【新聞稿】畫出來的身體會改變能力！4 人線上合作解謎《NICO DRAW》Steam 免費試玩版公開",
         f"[編輯／記者姓名]您好：\n\n我是日本獨立開發團隊 Chaos Cat Games 的[寄件人]。\n\n我們已在 Steam 公開《NICO DRAW》免費試玩版。本作是一款預計於 2026 年 12 月推出、最多支援 4 人線上合作的手繪風解謎動作遊戲。玩家可親手畫出角色的身體、手腳、翅膀或龜殼；形狀與有限 INK 的分配方式，會改變奔跑、滑翔、攀附牆面等能力以及關卡解法。\n\n試玩版包含 8 個關卡，預計遊玩時間約 1～2 小時。玩家可舉起並丟出朋友、互相攀附移動、搬運隊友，並組合 5 種角色能力，以不同方式完成關卡。\n\nSteam：{steam}\n影片：{trailer}\n媒體素材包：[媒體素材包 URL]\n\n若本作適合貴站讀者，懇請考慮新聞或試玩介紹。我們可隨時補充圖片、影片、訪談或其他資訊。\n\n[寄件人]\nChaos Cat Games\n[回覆信箱]",
         "[姓名] / [寄件人] / [信箱] / [素材包]", "台灣／繁中媒體", "精簡、資訊優先", "一般使用者可下載Demo、Demo連線、素材包權限"],
        ["繁體中文", "僅追蹤一次", "Re: 《NICO DRAW》免費試玩版 — [新增消息]",
         f"[姓名]您好：\n\n針對先前寄出的《NICO DRAW》，因新增了[新 GIF／試玩版更新／Steam新品節開始／具體成果]，因此簡短追蹤一次。\n\n[以兩句話內說明新資訊]\nSteam：{steam}\n媒體素材包：[URL]\n\n若不符合貴站方向，無須回覆。感謝撥冗確認。\n\n[寄件人] / Chaos Cat Games",
         "[新資訊] / [素材包]", "第一波未刊登且高度適合的媒體", "簡短、不施壓", "初次寄送後4～7個工作日，必須有新資訊"],
        ["繁體中文", "第二波：正式版", "【上市】親手畫出角色、一起解謎！《NICO DRAW》於 [日期] 登上 Steam",
         f"[姓名]您好：\n\nChaos Cat Games 將於[上市日期]在 Steam 推出最多 4 人線上合作解謎動作遊戲《NICO DRAW》，售價為[價格]。\n\n相較免費試玩版，正式版新增[已確認的三項內容]，共收錄[確認後的關卡數]個關卡。玩家畫出的身體與 INK 分配會改變能力，並可組合 5 種角色，以多種方式合作過關。\n\nSteam：{steam}\n上市影片：[URL]\n媒體素材包：[URL]\n評測序號：[提供方式]\n\n敬請考慮上市新聞、評測、訪談或多人連線企劃。",
         "[日期] / [價格] / [新增內容] / [關卡數] / [序號]", "所有繁中媒體，優先聯絡曾刊登Demo者", "新資訊優先", "與Steam及禁載時間一致"],
    ])

    rows.extend([
        ["简体中文", "第一波：Demo", "【新闻稿】画出的身体会改变能力！4人在线合作解谜《NICO DRAW》Steam免费试玩版公开",
         f"[编辑／记者姓名]您好：\n\n我是日本独立开发团队 Chaos Cat Games 的[发件人]。\n\n我们已在 Steam 发布《NICO DRAW》免费试玩版。本作是一款预计于 2026 年 12 月推出、最多支持 4 人在线合作的手绘解谜动作游戏。玩家可以亲手画出角色的身体、手脚、翅膀或龟壳；形状与有限 INK 的分配方式会改变奔跑、滑翔、攀附墙面等能力以及关卡解法。\n\n试玩版包含 8 个关卡，预计游玩时间约 1～2 小时。玩家可以举起并扔出朋友、互相攀附移动、搬运队友，并组合 5 种角色能力，以不同方式完成关卡。\n\nSteam：{steam}\n视频：{trailer}\n媒体素材包：[媒体素材包URL]\n\n如果本作适合贵站读者，恳请考虑新闻或试玩介绍。我们可以随时补充图片、视频、采访或其他信息。\n\n[发件人]\nChaos Cat Games\n[回复邮箱]",
         "[姓名] / [发件人] / [邮箱] / [素材包]", "简中媒体", "简洁、信息优先", "Demo公开、Demo联机、素材包权限"],
        ["简体中文", "仅跟进一次", "Re: 《NICO DRAW》免费试玩版 — [新增消息]",
         f"[姓名]您好：\n\n关于此前发送的《NICO DRAW》，由于新增了[新GIF／试玩版更新／Steam新品节开始／具体成果]，因此简短跟进一次。\n\n[用两句话以内说明新信息]\nSteam：{steam}\n媒体素材包：[URL]\n\n如不符合贵站方向，无需回复。感谢查看。\n\n[发件人] / Chaos Cat Games",
         "[新信息] / [素材包]", "第一波未刊登且高度匹配的媒体", "简短、不施压", "首次发送后4～7个工作日，必须有新信息"],
        ["简体中文", "第二波：正式版", "【发售】亲手画出角色、一起解谜！《NICO DRAW》将于[日期]登陆Steam",
         f"[姓名]您好：\n\nChaos Cat Games 将于[发售日期]在 Steam 推出最多 4 人在线合作解谜动作游戏《NICO DRAW》，售价为[价格]。\n\n与免费试玩版相比，正式版新增[已确认的三项内容]，共包含[确认后的关卡数]个关卡。玩家画出的身体和 INK 分配会改变能力，并可组合 5 种角色，以多种方式合作通关。\n\nSteam：{steam}\n发售预告片：[URL]\n媒体素材包：[URL]\n评测Key：[提供方式]\n\n敬请考虑发售新闻、评测、采访或多人联机内容。",
         "[日期] / [价格] / [新增内容] / [关卡数] / [Key]", "所有简中媒体；优先联系报道过Demo者", "新信息优先", "与Steam和禁发时间一致"],
    ])
    return rows


def create_template_sheet(wb: Workbook) -> None:
    if "送信テンプレート" in wb.sheetnames:
        del wb["送信テンプレート"]
    ws = wb.create_sheet("送信テンプレート", 3)
    headers = ["言語", "用途", "件名", "本文テンプレート", "必須差し替え", "送付対象", "トーン", "送信前確認"]
    ws.append(headers)
    rows = build_templates()
    for row in rows:
        ws.append(row)
    style_header(ws, 1, 1, len(headers))
    style_table(ws, 2, ws.max_row, 1, len(headers))
    for r in range(2, ws.max_row + 1):
        ws.row_dimensions[r].height = 235 if "Demo" in str(ws.cell(r, 2).value) or "デモ" in str(ws.cell(r, 2).value) else 145
        if r % 3 == 2:
            for c in range(1, 9):
                ws.cell(r, c).fill = fill(PALE_BLUE)
    widths = [18, 20, 58, 110, 42, 32, 22, 42]
    for c, width in enumerate(widths, start=1):
        ws.column_dimensions[get_column_letter(c)].width = width
    ws.auto_filter.ref = f"A1:H{ws.max_row}"
    setup_page(ws)


def create_schedule_sheet(wb: Workbook) -> None:
    if "送信スケジュール" in wb.sheetnames:
        del wb["送信スケジュール"]
    ws = wb.create_sheet("送信スケジュール", 4)
    headers = ["順序", "基準", "推奨タイミング", "対象", "送る内容", "主CTA", "対象件数", "成功判定", "次アクション"]
    ws.append(headers)
    rows = [
        [1, "P0", "送信開始前", "全地域", "Steam上のDemo公開、Demoオンライン協力、Press Kit公開URL、返信先を確認", "送信可否を確定", 0, "P0チェックがすべて完了", "未完了なら送信しない"],
        [2, "D-14〜D-10", "現地 火〜木 09:30〜11:30", "第1陣 Batch A（英語/韓国語/繁体字）", "デモ公開＋一文フック＋Steam＋短尺動画＋Press Kit", "Demoを遊ぶ / 記事検討", 12, "開封/返信/掲載を記録", "件名・冒頭の反応を確認"],
        [3, "D-9〜D-6", "現地 火〜木 09:30〜11:30", "第1陣 Batch B（日本語/スペイン語/PT-BR）", "Batch Aの学びを反映した各言語版", "Demoを遊ぶ / 記事検討", 12, "返信とクリックの質", "勝ち件名を残りへ展開"],
        [4, "D-5〜D-3", "現地 火〜木 09:30〜11:30", "第1陣残り", "媒体別に1文個別化したデモ案内", "Demoを遊ぶ", 11, "第1陣35件送信完了", "掲載URLを都度記録"],
        [5, "D-4〜D-1", "新情報がある場合のみ", "第1陣の未掲載かつ高適合媒体", "新GIF/更新/イベント開始などを付けた1回限りのフォロー", "新情報を見る", "最大12", "初回の連投を避ける", "以後は追わない"],
        [6, "イベント開始日", "各地域の午前", "第2陣のうちNext Fest/デモ特集媒体", "イベント開始という新しい時点情報", "期間中にDemoを遊ぶ", 8, "特集/まとめ記事入り", "中盤の追加送信はしない"],
        [7, "イベント中盤", "成果確認", "未送信の第2陣", "確かな反響/更新がある場合だけ送付", "記事/試遊", 8, "掲載効率を維持", "低適合先は見送る"],
        [8, "イベント終了後+2日", "集計", "内部", "掲載、返信、無反応、クリック、地域別の結果を記録", "製品版優先順位を作る", 0, "全掲載URL記録", "製品版リストを確定"],
        [9, "発売D-21〜D-14", "現地 火〜木 午前", "デモ掲載媒体＋第1陣高適合", "発売日、価格、デモからの追加点、レビューキー", "レビュー/発売記事", "掲載媒体＋上位", "レビューキー受領/記事予定", "キー・禁載管理"],
        [10, "発売D-7〜D-3", "現地 火〜木 午前", "第2陣＋未掲載有力媒体", "完成版の新情報と発売トレーラー", "発売記事/レビュー", "残り", "予定媒体数を把握", "発売当日素材を準備"],
        [11, "発売日", "各地域の午前", "掲載予定媒体", "発売開始の短い確認、公開済みURL", "記事公開", 0, "掲載記事URL回収", "公式SNS/Steamニュースで紹介"],
    ]
    for row in rows:
        ws.append(row)
    style_header(ws, 1, 1, len(headers))
    style_table(ws, 2, ws.max_row, 1, len(headers))
    for r in range(2, ws.max_row + 1):
        ws.row_dimensions[r].height = 58
        if ws.cell(r, 2).value == "P0":
            for c in range(1, 10):
                ws.cell(r, c).fill = fill(RED)

    start = ws.max_row + 3
    ws.cell(start, 1, "地域別の送信時間目安（現地時間）")
    ws.merge_cells(start_row=start, start_column=1, end_row=start, end_column=9)
    ws.cell(start, 1).fill = fill(NAVY)
    ws.cell(start, 1).font = Font(name="Yu Gothic", size=11, bold=True, color=WHITE)
    tz_headers = ["地域", "曜日", "現地時間", "日本時間の目安", "注意"]
    for c, value in enumerate(tz_headers, start=1):
        ws.cell(start + 1, c, value)
    style_header(ws, start + 1, 1, 5)
    timezone_rows = [
        ["日本", "火〜木", "10:00〜11:30 JST", "同時刻", "月曜朝・金曜午後を避ける"],
        ["韓国", "火〜木", "10:00〜11:30 KST", "同時刻", "日本と同じUTC+9"],
        ["台湾", "火〜木", "10:00〜11:30 CST", "11:00〜12:30 JST", "繁体字全文"],
        ["英国", "火〜木", "09:30〜11:00", "夏17:30〜19:00 / 冬18:30〜20:00", "送信時の夏時間を確認"],
        ["欧州（スペイン）", "火〜木", "09:30〜11:00", "夏16:30〜18:00 / 冬17:30〜19:00", "祝日を確認"],
        ["北米東部", "火〜木", "09:30〜11:00 ET", "夏22:30〜翌00:00 / 冬23:30〜翌00:00", "予約送信推奨"],
        ["ブラジル", "火〜木", "09:30〜11:00 BRT", "21:30〜23:00 JST", "ポルトガル語件名"],
    ]
    for row in timezone_rows:
        ws.append(row)
    style_table(ws, start + 2, ws.max_row, 1, 5)
    for r in range(start + 2, ws.max_row + 1):
        ws.row_dimensions[r].height = 34
    widths = [9, 16, 24, 34, 46, 28, 14, 34, 38]
    for c, width in enumerate(widths, start=1):
        ws.column_dimensions[get_column_letter(c)].width = width
    ws.auto_filter.ref = f"A1:I{1 + len(rows)}"
    setup_page(ws)


def create_checklist_sheet(wb: Workbook) -> None:
    if "送信前チェック" in wb.sheetnames:
        del wb["送信前チェック"]
    ws = wb.create_sheet("送信前チェック", 5)
    headers = ["優先", "カテゴリ", "チェック項目", "完了条件", "状態", "確認証跡/URL", "担当", "期限", "未完了時の扱い"]
    ws.append(headers)
    rows = [
        ["P0", "Steam", "一般ユーザーにDemoが公開表示", "ログアウト/別アカウントのSteamでDemoボタンが表示され、インストールできる", "要確認", STEAM_URL, "Chaos Cat Games", "送信前", "未確認なら『無料デモ公開』営業を停止"],
        ["P0", "マルチプレイ", "Demoでオンライン協力が動作", "別Steamアカウント・別PC/回線の2台以上でロビー作成、参加、ステージ開始、終了まで確認", "要確認", "[テスト日時・Build ID]", "Chaos Cat Games", "送信前", "未確認ならDemoの人数表記を送信本文から外す"],
        ["P0", "Press Kit", "公開URLを作成", "ログイン不要でKV、ロゴ、スクショ、GIF、説明文、連絡先を閲覧/取得できる", "未着手", "[Press Kit URL]", "Chaos Cat Games", "送信前", "未完了なら第1陣を延期"],
        ["P0", "連絡先", "メディア返信先を確定", "毎営業日に監視し、迷惑メールも確認できるメール", "未着手", "[返信先メール]", "Chaos Cat Games", "送信前", "未完了なら送信不可"],
        ["P0", "表記", "Steamとの数値一致", "最大4人、Online Co-op、Windows、2026年12月、開発/販売名が一致", "確認済", STEAM_URL, "Chaos Cat Games", "送信前", "差異があれば本文かSteamを修正"],
        ["P0", "イベント", "Steam Next Fest参加情報", "参加回、正式名称、開始/終了日時をSteamworks/公式告知で確認", "要確認", "[公式確認URL/画面]", "Chaos Cat Games", "送信前", "未確定ならNext Fest文言を削除"],
        ["P1", "素材", "キービジュアル", "横長・ロゴ有無の2種、記事サムネイルで読める", "要確認", "[ファイル/URL]", "Chaos Cat Games", "第1陣前", "暫定画像で送らない"],
        ["P1", "素材", "スクリーンショット", "描画画面、4人協力、投げる、滑空、壁張り付き、ギミックを含む8〜12枚", "要確認", "[URL]", "Chaos Cat Games", "第1陣前", "最低6枚を揃える"],
        ["P1", "素材", "短尺GIF/WebM", "3〜8秒で『描く→能力が変わる』『仲間を投げる/運ぶ』が無音でも伝わる", "要確認", "[URL]", "Chaos Cat Games", "第1陣前", "英語媒体の優先度を下げる"],
        ["P1", "素材", "ロゴ/透過PNG", "背景透過、十分な解像度、余白あり", "要確認", "[URL]", "Chaos Cat Games", "第1陣前", "Press Kitに不足と明記"],
        ["P1", "動画", "紹介動画", "公開設定、埋め込み可、年齢/地域制限なし", "確認済", TRAILER_URL, "Chaos Cat Games", "第1陣前", "公開設定を修正"],
        ["P1", "文面", "7言語テンプレートの固有値置換", "担当者名、Press Kit、返信先、イベント、日付を置換し角括弧が残っていない", "未着手", "送信テンプレートシート", "Chaos Cat Games", "各送信前", "角括弧が残るメールは送らない"],
        ["P1", "ローカライズ", "対象言語のSteam対応確認", "英/日/韓/繁中/簡中/西/ブラジル葡語のUI表記を実機確認", "要確認", STEAM_URL, "Chaos Cat Games", "第1陣前", "未確認言語の『対応済み』表現を削除"],
        ["P1", "権利", "画像・音楽・フォント・人物名", "媒体配布と記事利用が可能で、未公開/第三者素材を含まない", "要確認", "[権利確認メモ]", "Chaos Cat Games", "第1陣前", "問題素材を除外"],
        ["P1", "Demo内容", "非公開ステージ/開発用データを除外", "配布DemoとPress Kitに発売前秘匿データ、開発用設定、不要なログ/鍵が含まれない", "要確認", "[Build ID / 検査記録]", "Chaos Cat Games", "第1陣前", "Build差し替え後に再確認"],
        ["P2", "運用", "重複送信防止", "媒体名、宛先、送信日時をリストへ即時記録", "未着手", "メディア営業リスト", "Chaos Cat Games", "送信中", "記録してから次を送る"],
        ["P2", "運用", "掲載記事URL回収", "掲載日、URL、言語、記事タイプを当日記録", "未着手", "メディア営業リスト", "Chaos Cat Games", "掲載時", "製品版営業の優先度に反映"],
        ["P2", "製品版", "価格・発売日・レビューキー", "Steamと一致する確定値、禁載日時、キー配布方法を用意", "未着手", "[製品版情報]", "Chaos Cat Games", "発売D-21", "第2波送信を開始しない"],
    ]
    for row in rows:
        ws.append(row)
    style_header(ws, 1, 1, len(headers))
    style_table(ws, 2, ws.max_row, 1, len(headers))
    for r in range(2, ws.max_row + 1):
        ws.row_dimensions[r].height = 52
        priority = ws.cell(r, 1).value
        color = RED if priority == "P0" else YELLOW if priority == "P1" else PALE_BLUE
        ws.cell(r, 1).fill = fill(color)
        ws.cell(r, 1).font = Font(name="Yu Gothic", size=10, bold=True, color=NAVY)
        set_hyperlink(ws.cell(r, 6), ws.cell(r, 6).value)
    dv = DataValidation(type="list", formula1='"未着手,要確認,対応中,確認済,不要"', allow_blank=False)
    ws.add_data_validation(dv)
    dv.add(f"E2:E{ws.max_row}")
    ws.conditional_formatting.add(f"E2:E{ws.max_row}", FormulaRule(formula=['$E2="確認済"'], fill=fill(GREEN)))
    ws.conditional_formatting.add(f"E2:E{ws.max_row}", FormulaRule(formula=['OR($E2="未着手",$E2="要確認")'], fill=fill(YELLOW)))
    widths = [9, 18, 34, 62, 14, 44, 22, 16, 42]
    for c, width in enumerate(widths, start=1):
        ws.column_dimensions[get_column_letter(c)].width = width
    ws.auto_filter.ref = f"A1:I{ws.max_row}"
    setup_page(ws)


def rebuild_progress(wb: Workbook, media_rows: list[list]) -> None:
    ws = wb["進捗"]
    for row in ws.iter_rows():
        for c in row:
            c.value = None
    order_counts = Counter(r[17] for r in media_rows)
    lang_counts = Counter(r[3] for r in media_rows)
    data = [
        ["NICO DRAW メディア営業 v9 進捗", "2026-10-03時点"],
        ["指標", "現在"],
        ["調査済み営業候補（別枠含む）", len(media_rows)],
        ["Web編集媒体", len(media_rows) - order_counts.get("別枠", 0)],
        ["第1陣", order_counts.get("第1陣", 0)],
        ["第2陣", order_counts.get("第2陣", 0)],
        ["保留", order_counts.get("保留", 0)],
        ["別枠（PR配信/動画）", order_counts.get("別枠", 0)],
        [None, None],
        ["営業言語", "件数"],
        *[[lang, lang_counts.get(lang, 0)] for lang in ["日本語", "英語", "韓国語", "繁体字中国語", "簡体字中国語", "スペイン語", "ブラジルポルトガル語"]],
        [None, None],
        ["v9追加", "根拠"],
        ["TheSixthAxis", "公式の開発者向け窓口＋2026年9月Steamデモ単独記事"],
        ["GodisaGeek.com", "公式のPress Releases & Game Pitching専用窓口"],
        ["COGconnected", "公式PR/News Tips窓口＋創作系Steamデモ単独記事"],
        ["Use a Potion!", "小規模チームのレビュー媒体。製品版/第2陣向け"],
        ["Vandal", "3人オンライン協力パズルのNext Fest記事＋プレス専用メール"],
        ["3DJuegos", "公式プレス専用メール＋Next Festデモ選定"],
        ["Overloadr", "企画提案フォーム＋独自性のあるインディーを継続取材"],
        ["Jogando Casualmente", "1〜4人オンライン協力インディーの実プレイPreview＋公式メール"],
        ["Gamereactor 中文版", "Steam協力パズルデモ単独記事＋公式編集部窓口"],
        ["게임뷰 (GameVU)", "韓国インディーパズルの直近取材＋記事記者メール"],
        [None, None],
        ["完全版に追加した実務機能", "内容"],
        ["営業ダッシュボード", "件数、確定情報、P0ブロッカー、最優先12媒体"],
        ["送信テンプレート", "日本語/英語/韓国語/スペイン語/PT-BR/繁体字/簡体字 × デモ/フォロー/製品版"],
        ["送信スケジュール", "相対日程、バッチ、地域別送信時間"],
        ["送信前チェック", "Steam Demo表示、Demoマルチ、Press Kit、権利、重複防止など"],
        [None, None],
        ["残る要確認", "判断"],
        ["SteamのDemo表示", "Web取得ではPlaytest導線が見えたため、一般ユーザー環境で必ず最終確認"],
        ["Next Festの回/日付", "公式確定情報がない限りメール本文へ固定値を書かない"],
        ["Press Kit URL/返信先", "未確定のためテンプレートでは角括弧のプレースホルダー"],
    ]
    for r, row in enumerate(data, start=1):
        ws.cell(r, 1, row[0])
        ws.cell(r, 2, row[1])
    ws.column_dimensions["A"].width = 32
    ws.column_dimensions["B"].width = 90
    ws.merge_cells("A1:B1")
    ws["A1"] = "NICO DRAW メディア営業 v9 — 進捗と変更点"
    ws["A1"].fill = fill(NAVY)
    ws["A1"].font = Font(name="Yu Gothic", size=15, bold=True, color=WHITE)
    ws["A1"].alignment = Alignment(horizontal="left", vertical="center")
    ws.row_dimensions[1].height = 28
    section_rows = [2, 10, 19, 31, 37]
    for r in section_rows:
        if r <= ws.max_row:
            style_header(ws, r, 1, 2)
    for r in range(2, ws.max_row + 1):
        if r not in section_rows:
            for c in range(1, 3):
                ws.cell(r, c).border = base_border()
                ws.cell(r, c).font = Font(name="Yu Gothic", size=10, color=INK)
                ws.cell(r, c).alignment = Alignment(vertical="top", wrap_text=True)
            ws.row_dimensions[r].height = 34
    ws.sheet_view.showGridLines = False
    ws.freeze_panes = "A2"


def collect_media_rows(wb: Workbook) -> list[list]:
    ws = wb["メディア営業リスト"]
    rows = []
    for r in range(2, ws.max_row + 1):
        if ws.cell(r, 2).value:
            rows.append([ws.cell(r, c).value for c in range(1, 20)])
    return rows


def normalize_workbook(wb: Workbook) -> None:
    for ws in wb.worksheets:
        ws.sheet_properties.pageSetUpPr.fitToPage = True
        ws.page_margins.left = 0.25
        ws.page_margins.right = 0.25
        ws.page_margins.top = 0.5
        ws.page_margins.bottom = 0.5
        for row in ws.iter_rows():
            for c in row:
                if c.value is not None and c.font.name is None:
                    c.font = Font(name="Yu Gothic", size=c.font.sz or 10, bold=c.font.b, italic=c.font.i, color=c.font.color)
    if wb.calculation is None:
        wb.calculation = CalcProperties()
    wb.calculation.calcMode = "auto"
    wb.calculation.fullCalcOnLoad = True
    wb.calculation.forceFullCalc = True


def validate(wb: Workbook) -> None:
    expected = {
        "営業ダッシュボード",
        "方針_送信タイミング",
        "メディア営業リスト",
        "送信テンプレート",
        "送信スケジュール",
        "送信前チェック",
        "プレスキット準備",
        "プレスリリース骨子",
        "追加調査キュー",
        "調査知見",
        "進捗",
        "配信_PR経路",
    }
    missing = expected.difference(wb.sheetnames)
    if missing:
        raise ValueError(f"Missing sheets: {sorted(missing)}")
    rows = collect_media_rows(wb)
    if len(rows) != 62:
        raise ValueError(f"Expected 62 media rows, found {len(rows)}")
    numbers = [row[0] for row in rows]
    if numbers != list(range(1, 63)):
        raise ValueError("Media No. values are not sequential 1..62")
    names = [row[1] for row in rows]
    if len(names) != len(set(names)):
        duplicates = [name for name, count in Counter(names).items() if count > 1]
        raise ValueError(f"Duplicate media names: {duplicates}")
    if any(not row[8] for row in rows):
        raise ValueError("Every media row must have an evidence URL")
    if any(row[17] not in {"第1陣", "第2陣", "保留", "別枠"} for row in rows):
        raise ValueError("Unexpected send-order value")
    if wb["送信テンプレート"].max_row != 22:
        raise ValueError("Expected 21 localized message templates")


def build(source: Path, output: Path) -> None:
    wb = openpyxl.load_workbook(source)
    add_media_rows(wb)
    update_existing_sheets(wb)
    media_rows = collect_media_rows(wb)
    create_dashboard(wb, media_rows)
    create_template_sheet(wb)
    create_schedule_sheet(wb)
    create_checklist_sheet(wb)
    rebuild_progress(wb, media_rows)

    preferred_order = [
        "営業ダッシュボード",
        "方針_送信タイミング",
        "メディア営業リスト",
        "送信テンプレート",
        "送信スケジュール",
        "送信前チェック",
        "プレスキット準備",
        "プレスリリース骨子",
        "追加調査キュー",
        "調査知見",
        "進捗",
        "配信_PR経路",
    ]
    wb._sheets = [wb[name] for name in preferred_order]
    normalize_workbook(wb)
    validate(wb)
    output.parent.mkdir(parents=True, exist_ok=True)
    wb.save(output)


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--source", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True)
    args = parser.parse_args()
    build(args.source, args.output)


if __name__ == "__main__":
    main()
