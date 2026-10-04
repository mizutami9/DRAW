from __future__ import annotations

import re


# The user has already contacted these creators. They must never be present in the
# new A/B sales queue, even if a spelling variant or tracking query is encountered.
DM_DONE_NAMES = [
    "キュレ", "にらの民", "あんぜんだいいち", "平方ワールド", "カタリベゲームズ",
    "ショウセイshousei-agent", "サ柊", "ヨフカシ@ゲーム実況", "shooon＠ゲーム実況",
    "べんべん", "かねぶろ", "毎日がエブリデイライフ", "さっぽこぽんちゃい",
    "とまとなすびチャンネル", "TMG", "KAFE", "夜な夜なドロン", "ぴきねこ",
    "ゆるゆるなまな板さん家", "しょぴ",
    "怀特尼斯whiteness", "折笔日记", "代糖L", "扶-城", "紫幻青", "無畏寒東",
    "王大大Gamer", "Emberwindy", "蓝若羽", "水天葵", "哇系小黑哥", "远哥视频",
    "爱打游戏的老给", "龙眼皮Lyd", "清照儿", "Zero零八", "梧箐箐", "老白ya-游戏",
    "玲珑酱酱子", "沐锶MuSr", "云端不是云玩家", "乱打乱蒙",
    "Lominoth", "Kino & Lotus", "Elie", "Zoey The Demon Fox", "OniHeart",
    "Marisol Fable", "DiceyDev", "Nova Lux", "Line Klein", "Kazura", "Oshen Allure",
    "Cloverpop", "SlingerGames", "Fairy Mysterious Adventures", "JeansGaming", "Kyumera",
    "MizukoKanagawa", "Uxie", "Vivi / M_Vixn",
]

DM_DONE_URL_TOKENS = [
    "niranotami", "anzen_daiichi_4", "heihouworld", "nii_san_213", "shousei_agent",
    "shu_of_service", "yohu_kashi", "k14440", "u38z5wskh_y", "quarbro",
    "%e6%af%8f%e6%97%a5%e3%81%8c%e3%82%a8%e3%83%96%e3%83%aa%e3%83%87%e3%82%a4%e3%83%a9%e3%82%a4%e3%83%95",
    "sappoko_ponchai", "tomatonasubich", "tmg-4manzi", "kafecafe", "yonayonadoron",
    "pikineco", "manaitasanchi", "sho-p",
    "space.bilibili.com/92682962", "space.bilibili.com/903630", "bv14hgy6aecl",
    "space.bilibili.com/1371735722", "space.bilibili.com/170805608",
    "space.bilibili.com/3494371580184942", "space.bilibili.com/85400122",
    "space.bilibili.com/1455953", "space.bilibili.com/228803159",
    "space.bilibili.com/18306130", "space.bilibili.com/150799",
    "space.bilibili.com/15152799", "space.bilibili.com/73652352",
    "space.bilibili.com/39846961", "space.bilibili.com/737453",
    "space.bilibili.com/3707040528599288", "space.bilibili.com/1629955",
    "space.bilibili.com/32048607", "space.bilibili.com/14060509",
    "space.bilibili.com/2027700528", "space.bilibili.com/3546780199291598",
    "space.bilibili.com/18982459",
    "lominoth", "kinaurandlobus", "itseliep", "zoeythedemonfox", "oniheartvt",
    "marisolfable", "diceydev", "novaluxvt", "lineklein100", "kazuraarts",
    "oshenallure", "cloverpopvt", "slinger92", "fairymystgames", "jeansgamingyt",
    "kyumerakyo", "mizukokanagawa", "uxieuuu", "m_vixxxnnn",
]


GAME_ALIASES = {
    "MECCHA CHAMELEON": ["meccha chameleon", "メッチャカメレオン", "めっちゃカメレオン"],
    "PICO PARK 2": ["pico park 2", "pico park", "ピコパーク", "피코 파크"],
    "PEAK": ["peak"],
    "R.E.P.O.": ["r.e.p.o.", "r.e.p.o", "repo"],
    "Chained Together": ["chained together", "체인드 투게더"],
    "Lethal Company": ["lethal company", "リサルカンパニー", "리썰 컴퍼니"],
    "Content Warning": ["content warning"],
    "Human: Fall Flat": ["human fall flat", "human: fall flat", "ヒューマンフォールフラット"],
    "Party Animals": ["party animals"],
    "Heave Ho": ["heave ho"],
    "Bread & Fred": ["bread & fred", "bread and fred"],
    "We Were Here": ["we were here"],
    "Escape Simulator": ["escape simulator"],
    "Pummel Party": ["pummel party"],
    "Gang Beasts": ["gang beasts"],
    "Moving Out": ["moving out"],
    "Overcooked": ["overcooked", "オーバークック"],
    "PlateUp!": ["plateup", "plate up"],
    "Unrailed!": ["unrailed"],
    "Keep Talking": ["keep talking and nobody explodes"],
    "RV There Yet?": ["rv there yet"],
    "Waterpark Simulator": ["waterpark simulator"],
    "Ready or Not": ["ready or not"],
    "HELLDIVERS 2": ["helldivers 2"],
}

INTRINSIC_MULTIPLAYER = {
    "MECCHA CHAMELEON", "PICO PARK 2", "Party Animals", "We Were Here",
    "Keep Talking", "Pummel Party", "Gang Beasts", "Heave Ho", "Unrailed!",
}

MULTIPLAYER_PATTERNS = [
    r"\bcollab(?:oration)?\b", r"\bwith\b", r"\bw/", r"\bfeat\.?\b", r"\bft\.?\b",
    r"\bfriends?\b", r"\btogether\b", r"\bmultiplayer\b", r"\bco-?op\b",
    r"\bteam(?:work)?\b", r"\bduo\b", r"\bsquad\b",
    r"コラボ", r"一緒", r"みんな", r"友達", r"友人", r"リア友", r"参加型",
    r"[234４３２]人", r"合流", r"共演",
    r"합방", r"같이", r"친구", r"[234]인",
    r"多人", r"一起", r"很多人", r"觀眾", r"陪我", r"朋友", r"聯動", r"联动",
    r"เพื่อน", r"ด้วยกัน", r"ทีม", r"\bw/",
    r"\bcon\b", r"\bcom\b", r"amigos?", r"juntos?", r"voc[eê]s", r"equipo", r"equipe", r"colab",
    r"\bmit\b", r"zusammen", r"avec", r"amis?", r"équipe",
]


def normalize(value: object) -> str:
    return re.sub(r"[^a-z0-9\u3040-\u30ff\u3400-\u9fff\uac00-\ud7af]+", "", str(value or "").lower())


def is_dm_done(*values: object) -> bool:
    combined = " ".join(str(value or "").lower() for value in values)
    candidate_name = normalize(values[0] if values else "")
    for name in DM_DONE_NAMES:
        done_name = normalize(name)
        if candidate_name == done_name:
            return True
        if len(done_name) >= 8 and (done_name in candidate_name or candidate_name in done_name):
            return True
    return any(token.lower() in combined for token in DM_DONE_URL_TOKENS)


def detect_game(title: object) -> str | None:
    lowered = str(title or "").lower()
    for game, aliases in GAME_ALIASES.items():
        if any(alias in lowered for alias in aliases):
            return game
    return None


def multiplayer_title(title: object, game: str | None = None) -> bool:
    lowered = str(title or "").lower()
    return bool(game in INTRINSIC_MULTIPLAYER or any(re.search(pattern, lowered, re.I) for pattern in MULTIPLAYER_PATTERNS))
