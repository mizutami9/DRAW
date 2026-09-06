#!/usr/bin/env python3
"""Generate complete locale tables from the English PICO tables using Argos Translate.

Argos and its language models are build-time tools only. They are not shipped with the game.
Run from the repository root with ARGOS_PACKAGES_DIR pointing at a disposable cache.
"""

from __future__ import annotations

import argparse
import json
import re
import sys
import uuid
from pathlib import Path

import argostranslate.package
import argostranslate.translate


ROOT = Path(__file__).resolve().parents[1]
LOCALIZATION_DIR = ROOT / "Assets" / "Resources" / "Localization"
SOURCE_FILES = ("en.json", "stage_decorations_en.json")
MANAGER_SOURCE = ROOT / "Assets" / "Scripts" / "LocalizationManager.cs"

# Locale file name -> Argos target code. Regional variants intentionally share
# the closest available offline model and remain separate files for later QA.
TARGETS = {
    "fr": "fr", "it": "it", "de": "de", "es": "es", "es-419": "es",
    "pt-BR": "pt", "pt-PT": "pt", "ru": "ru", "zh-CN": "zh",
    "zh-TW": "zh", "ko": "ko", "ar": "ar", "id": "id", "uk": "uk",
    "nl": "nl", "el": "el", "sv": "sv", "th": "th", "cs": "cs",
    "da": "da", "tr": "tr", "no": "nb", "hu": "hu", "fi": "fi",
    "bg": "bg", "vi": "vi", "pl": "pl", "ro": "ro",
}

CRITICAL_KEYS = (
    "title_single", "title_multi", "title_option", "title_exit", "retry", "clear", "decide",
    "stage_select", "stage_clear_title", "stage_clear_body", "stage_clear_body_generic",
    "stage_clear_next", "stage_clear_back", "stage_clear_all_done",
)

# Short UI labels are highly ambiguous without screen context (for example,
# "clear" may mean erase or complete). Keep these reviewed translations stable.
CRITICAL_OVERRIDES = {
    "fr": ("Solo", "Multijoueur", "Options", "Quitter", "Réessayer", "Effacer", "Valider", "Choix du niveau", "NIVEAU TERMINÉ !", "Niveau {0} terminé !", "Terminé !", "Suivant : {0}", "Choix du niveau", "Tous les niveaux sont terminés !"),
    "it": ("Giocatore singolo", "Multigiocatore", "Opzioni", "Esci", "Riprova", "Cancella", "Conferma", "Selezione livello", "LIVELLO COMPLETATO!", "Livello {0} completato!", "Completato!", "Prossimo: {0}", "Selezione livello", "Tutti i livelli completati!"),
    "de": ("Einzelspieler", "Mehrspieler", "Optionen", "Beenden", "Erneut versuchen", "Löschen", "Bestätigen", "Levelauswahl", "LEVEL GESCHAFFT!", "Level {0} geschafft!", "Geschafft!", "Weiter: {0}", "Levelauswahl", "Alle Level geschafft!"),
    "es": ("Un jugador", "Multijugador", "Opciones", "Salir", "Reintentar", "Borrar", "Confirmar", "Selección de nivel", "¡NIVEL SUPERADO!", "¡Nivel {0} superado!", "¡Superado!", "Siguiente: {0}", "Selección de nivel", "¡Todos los niveles superados!"),
    "es-419": ("Un jugador", "Multijugador", "Opciones", "Salir", "Reintentar", "Borrar", "Confirmar", "Selección de nivel", "¡NIVEL SUPERADO!", "¡Nivel {0} superado!", "¡Superado!", "Siguiente: {0}", "Selección de nivel", "¡Todos los niveles superados!"),
    "pt-BR": ("Um jogador", "Multijogador", "Opções", "Sair", "Tentar novamente", "Apagar", "Confirmar", "Seleção de fase", "FASE CONCLUÍDA!", "Fase {0} concluída!", "Concluída!", "Próxima: {0}", "Seleção de fase", "Todas as fases concluídas!"),
    "pt-PT": ("Um jogador", "Multijogador", "Opções", "Sair", "Tentar novamente", "Apagar", "Confirmar", "Seleção de nível", "NÍVEL CONCLUÍDO!", "Nível {0} concluído!", "Concluído!", "Seguinte: {0}", "Seleção de nível", "Todos os níveis concluídos!"),
    "ru": ("Один игрок", "Мультиплеер", "Настройки", "Выход", "Повторить", "Очистить", "Подтвердить", "Выбор этапа", "ЭТАП ПРОЙДЕН!", "Этап {0} пройден!", "Пройдено!", "Далее: {0}", "Выбор этапа", "Все этапы пройдены!"),
    "zh-CN": ("单人游戏", "多人游戏", "设置", "退出", "重试", "清除", "确认", "关卡选择", "关卡完成！", "关卡 {0} 完成！", "完成！", "下一关：{0}", "关卡选择", "所有关卡已完成！"),
    "zh-TW": ("單人遊戲", "多人遊戲", "設定", "離開", "重試", "清除", "確認", "關卡選擇", "關卡完成！", "關卡 {0} 完成！", "完成！", "下一關：{0}", "關卡選擇", "所有關卡已完成！"),
    "ko": ("싱글 플레이", "멀티플레이", "설정", "나가기", "다시 시도", "지우기", "확인", "스테이지 선택", "스테이지 클리어!", "스테이지 {0} 클리어!", "클리어!", "다음: {0}", "스테이지 선택", "모든 스테이지 클리어!"),
    "ar": ("لاعب واحد", "متعدد اللاعبين", "الإعدادات", "خروج", "إعادة المحاولة", "مسح", "تأكيد", "اختيار المرحلة", "اكتملت المرحلة!", "اكتملت المرحلة {0}!", "اكتملت!", "التالي: {0}", "اختيار المرحلة", "اكتملت جميع المراحل!"),
    "id": ("Pemain tunggal", "Multipemain", "Pengaturan", "Keluar", "Coba lagi", "Hapus", "Konfirmasi", "Pilih tahap", "TAHAP SELESAI!", "Tahap {0} selesai!", "Selesai!", "Berikutnya: {0}", "Pilih tahap", "Semua tahap selesai!"),
    "uk": ("Один гравець", "Багатокористувацька гра", "Налаштування", "Вихід", "Спробувати ще раз", "Очистити", "Підтвердити", "Вибір етапу", "ЕТАП ПРОЙДЕНО!", "Етап {0} пройдено!", "Пройдено!", "Далі: {0}", "Вибір етапу", "Усі етапи пройдено!"),
    "nl": ("Eén speler", "Multiplayer", "Opties", "Afsluiten", "Opnieuw proberen", "Wissen", "Bevestigen", "Level kiezen", "LEVEL VOLTOOID!", "Level {0} voltooid!", "Voltooid!", "Volgende: {0}", "Level kiezen", "Alle levels voltooid!"),
    "el": ("Ένας παίκτης", "Πολλοί παίκτες", "Ρυθμίσεις", "Έξοδος", "Ξανά", "Διαγραφή", "Επιβεβαίωση", "Επιλογή πίστας", "Η ΠΙΣΤΑ ΟΛΟΚΛΗΡΩΘΗΚΕ!", "Η πίστα {0} ολοκληρώθηκε!", "Ολοκληρώθηκε!", "Επόμενη: {0}", "Επιλογή πίστας", "Όλες οι πίστες ολοκληρώθηκαν!"),
    "sv": ("En spelare", "Flera spelare", "Alternativ", "Avsluta", "Försök igen", "Radera", "Bekräfta", "Välj bana", "BANAN KLAR!", "Bana {0} klar!", "Klar!", "Nästa: {0}", "Välj bana", "Alla banor klara!"),
    "th": ("ผู้เล่นคนเดียว", "หลายผู้เล่น", "ตั้งค่า", "ออก", "ลองอีกครั้ง", "ลบ", "ยืนยัน", "เลือกด่าน", "ผ่านด่านแล้ว!", "ผ่านด่าน {0} แล้ว!", "ผ่านแล้ว!", "ถัดไป: {0}", "เลือกด่าน", "ผ่านครบทุกด่านแล้ว!"),
    "cs": ("Jeden hráč", "Více hráčů", "Nastavení", "Ukončit", "Zkusit znovu", "Smazat", "Potvrdit", "Výběr úrovně", "ÚROVEŇ DOKONČENA!", "Úroveň {0} dokončena!", "Dokončeno!", "Další: {0}", "Výběr úrovně", "Všechny úrovně dokončeny!"),
    "da": ("En spiller", "Flere spillere", "Indstillinger", "Afslut", "Prøv igen", "Slet", "Bekræft", "Vælg bane", "BANEN GENNEMFØRT!", "Bane {0} gennemført!", "Gennemført!", "Næste: {0}", "Vælg bane", "Alle baner gennemført!"),
    "tr": ("Tek oyuncu", "Çok oyunculu", "Ayarlar", "Çıkış", "Tekrar dene", "Sil", "Onayla", "Bölüm seçimi", "BÖLÜM TAMAMLANDI!", "Bölüm {0} tamamlandı!", "Tamamlandı!", "Sonraki: {0}", "Bölüm seçimi", "Tüm bölümler tamamlandı!"),
    "no": ("Én spiller", "Flerspiller", "Innstillinger", "Avslutt", "Prøv igjen", "Slett", "Bekreft", "Velg bane", "BANEN FULLFØRT!", "Bane {0} fullført!", "Fullført!", "Neste: {0}", "Velg bane", "Alle baner fullført!"),
    "hu": ("Egy játékos", "Többjátékos", "Beállítások", "Kilépés", "Újra", "Törlés", "Megerősítés", "Pályaválasztás", "PÁLYA TELJESÍTVE!", "A(z) {0}. pálya teljesítve!", "Teljesítve!", "Következő: {0}", "Pályaválasztás", "Minden pálya teljesítve!"),
    "fi": ("Yksinpeli", "Moninpeli", "Asetukset", "Lopeta", "Yritä uudelleen", "Pyyhi", "Vahvista", "Kentän valinta", "KENTTÄ LÄPÄISTY!", "Kenttä {0} läpäisty!", "Läpäisty!", "Seuraava: {0}", "Kentän valinta", "Kaikki kentät läpäisty!"),
    "bg": ("Един играч", "Мултиплейър", "Настройки", "Изход", "Опитай отново", "Изтрий", "Потвърди", "Избор на ниво", "НИВОТО Е ЗАВЪРШЕНО!", "Ниво {0} е завършено!", "Завършено!", "Следващо: {0}", "Избор на ниво", "Всички нива са завършени!"),
    "vi": ("Một người chơi", "Nhiều người chơi", "Cài đặt", "Thoát", "Thử lại", "Xóa", "Xác nhận", "Chọn màn", "HOÀN THÀNH MÀN!", "Hoàn thành màn {0}!", "Hoàn thành!", "Tiếp theo: {0}", "Chọn màn", "Đã hoàn thành tất cả các màn!"),
    "pl": ("Jeden gracz", "Wielu graczy", "Ustawienia", "Wyjdź", "Spróbuj ponownie", "Usuń", "Potwierdź", "Wybór poziomu", "POZIOM UKOŃCZONY!", "Poziom {0} ukończony!", "Ukończono!", "Dalej: {0}", "Wybór poziomu", "Wszystkie poziomy ukończone!"),
    "ro": ("Un jucător", "Multiplayer", "Setări", "Ieșire", "Reîncearcă", "Șterge", "Confirmă", "Selectare nivel", "NIVEL TERMINAT!", "Nivelul {0} a fost terminat!", "Terminat!", "Următorul: {0}", "Selectare nivel", "Toate nivelurile au fost terminate!"),
}

ROUND_OVERRIDE_KEYS = ("monitor", "round_clear", "timeout", "plug_holes", "reflect_laser")
ROUND_OVERRIDES = {
    "fr": ("MANCHE {0}/3    RESTE {1:0.0} s", "MANCHE {0} TERMINÉE !", "TEMPS ÉCOULÉ ! REPRISE DE CETTE MANCHE", "BOUCHEZ LES TROUS !", "AJUSTEZ LES POSITIONS ET LES ANGLES POUR RÉFLÉCHIR LE LASER !"),
    "it": ("ROUND {0}/3    {1:0.0} s RIMANENTI", "ROUND {0} COMPLETATO!", "TEMPO SCADUTO! SI RIPARTE DA QUESTO ROUND", "CHIUDI I BUCHI!", "REGOLA POSIZIONI E ANGOLI PER RIFLETTERE IL LASER!"),
    "de": ("RUNDE {0}/3    NOCH {1:0.0} s", "RUNDE {0} GESCHAFFT!", "ZEIT ABGELAUFEN! DIESE RUNDE WIRD NEU GESTARTET", "VERSCHLIESST DIE LÖCHER!", "PASST POSITIONEN UND WINKEL AN, UM DEN LASER ZU REFLEKTIEREN!"),
    "es": ("RONDA {0}/3    QUEDAN {1:0.0} s", "¡RONDA {0} SUPERADA!", "¡TIEMPO AGOTADO! SE REINICIA ESTA RONDA", "¡TAPEN LOS AGUJEROS!", "¡AJUSTEN POSICIONES Y ÁNGULOS PARA REFLEJAR EL LÁSER!"),
    "es-419": ("RONDA {0}/3    QUEDAN {1:0.0} s", "¡RONDA {0} SUPERADA!", "¡SE ACABÓ EL TIEMPO! SE REINICIA ESTA RONDA", "¡TAPEN LOS AGUJEROS!", "¡AJUSTEN POSICIONES Y ÁNGULOS PARA REFLEJAR EL LÁSER!"),
    "pt-BR": ("RODADA {0}/3    RESTAM {1:0.0} s", "RODADA {0} CONCLUÍDA!", "TEMPO ESGOTADO! REINICIANDO ESTA RODADA", "TAMPEM OS BURACOS!", "AJUSTEM AS POSIÇÕES E OS ÂNGULOS PARA REFLETIR O LASER!"),
    "pt-PT": ("RONDA {0}/3    RESTAM {1:0.0} s", "RONDA {0} CONCLUÍDA!", "TEMPO ESGOTADO! A REINICIAR ESTA RONDA", "TAPEM OS BURACOS!", "AJUSTEM AS POSIÇÕES E OS ÂNGULOS PARA REFLETIR O LASER!"),
    "ru": ("РАУНД {0}/3    ОСТАЛОСЬ {1:0.0} с", "РАУНД {0} ПРОЙДЕН!", "ВРЕМЯ ВЫШЛО! ЭТОТ РАУНД НАЧИНАЕТСЯ ЗАНОВО", "ЗАКРОЙТЕ ОТВЕРСТИЯ!", "МЕНЯЙТЕ ПОЗИЦИИ И УГЛЫ, ЧТОБЫ ОТРАЖАТЬ ЛАЗЕР!"),
    "zh-CN": ("第 {0}/3 轮    剩余 {1:0.0} 秒", "第 {0} 轮完成！", "时间到！重新开始本轮", "堵住所有破洞！", "调整位置和角度来反射激光！"),
    "zh-TW": ("第 {0}/3 回合    剩餘 {1:0.0} 秒", "第 {0} 回合完成！", "時間到！重新開始本回合", "堵住所有破洞！", "調整位置和角度來反射雷射！"),
    "ko": ("라운드 {0}/3    남은 시간 {1:0.0}초", "라운드 {0} 클리어!", "시간 초과! 이 라운드를 다시 시작합니다", "구멍을 막아라!", "위치와 각도를 조절해 레이저를 반사하라!"),
    "ar": ("الجولة {0}/3    متبقٍ {1:0.0} ث", "اكتملت الجولة {0}!", "انتهى الوقت! ستُعاد هذه الجولة", "سدّوا الثقوب!", "اضبطوا المواقع والزوايا لعكس الليزر!"),
    "id": ("RONDE {0}/3    SISA {1:0.0} dtk", "RONDE {0} SELESAI!", "WAKTU HABIS! MENGULANG RONDE INI", "TUTUP LUBANGNYA!", "ATUR POSISI DAN SUDUT UNTUK MEMANTULKAN LASER!"),
    "uk": ("РАУНД {0}/3    ЗАЛИШИЛОСЯ {1:0.0} с", "РАУНД {0} ПРОЙДЕНО!", "ЧАС ВИЙШОВ! ЦЕЙ РАУНД ПОЧИНАЄТЬСЯ ЗНОВУ", "ЗАКРИЙТЕ ОТВОРИ!", "ЗМІНЮЙТЕ ПОЗИЦІЇ ТА КУТИ, ЩОБ ВІДБИВАТИ ЛАЗЕР!"),
    "nl": ("RONDE {0}/3    NOG {1:0.0} s", "RONDE {0} VOLTOOID!", "TIJD VOORBIJ! DEZE RONDE START OPNIEUW", "DICHT DE GATEN!", "PAS POSITIES EN HOEKEN AAN OM DE LASER TE WEERKAATSEN!"),
    "el": ("ΓΥΡΟΣ {0}/3    ΑΠΟΜΕΝΟΥΝ {1:0.0} δ", "Ο ΓΥΡΟΣ {0} ΟΛΟΚΛΗΡΩΘΗΚΕ!", "ΤΕΛΟΣ ΧΡΟΝΟΥ! Ο ΓΥΡΟΣ ΞΕΚΙΝΑ ΞΑΝΑ", "ΚΛΕΙΣΤΕ ΤΙΣ ΤΡΥΠΕΣ!", "ΡΥΘΜΙΣΤΕ ΘΕΣΕΙΣ ΚΑΙ ΓΩΝΙΕΣ ΓΙΑ ΝΑ ΑΝΑΚΛΑΣΕΤΕ ΤΟ ΛΕΪΖΕΡ!"),
    "sv": ("RUNDA {0}/3    {1:0.0} s KVAR", "RUNDA {0} KLAR!", "TIDEN ÄR UTE! DEN HÄR RUNDAN STARTAR OM", "TÄPP I HÅLEN!", "JUSTERA POSITIONER OCH VINKLAR FÖR ATT REFLEKTERA LASERN!"),
    "th": ("รอบ {0}/3    เหลือ {1:0.0} วินาที", "ผ่านรอบ {0} แล้ว!", "หมดเวลา! เริ่มรอบนี้ใหม่", "อุดรูให้หมด!", "ปรับตำแหน่งและมุมเพื่อสะท้อนเลเซอร์!"),
    "cs": ("KOLO {0}/3    ZBÝVÁ {1:0.0} s", "KOLO {0} DOKONČENO!", "ČAS VYPRŠEL! TOTO KOLO ZAČÍNÁ ZNOVU", "UCPĚTE OTVORY!", "UPRAVTE POLOHY A ÚHLY, ABYSTE ODRAZILI LASER!"),
    "da": ("RUNDE {0}/3    {1:0.0} s TILBAGE", "RUNDE {0} GENNEMFØRT!", "TIDEN ER GÅET! DENNE RUNDE STARTER IGEN", "LUK HULLERNE!", "JUSTER POSITIONER OG VINKLER FOR AT REFLEKTERE LASEREN!"),
    "tr": ("TUR {0}/3    {1:0.0} sn KALDI", "TUR {0} TAMAMLANDI!", "SÜRE DOLDU! BU TUR YENİDEN BAŞLIYOR", "DELİKLERİ KAPATIN!", "LAZERİ YANSITMAK İÇİN KONUMLARI VE AÇILARI AYARLAYIN!"),
    "no": ("RUNDE {0}/3    {1:0.0} s IGJEN", "RUNDE {0} FULLFØRT!", "TIDEN ER UTE! DENNE RUNDEN STARTER PÅ NYTT", "TETT HULLENE!", "JUSTER POSISJONER OG VINKLER FOR Å REFLEKTERE LASEREN!"),
    "hu": ("KÖR {0}/3    {1:0.0} mp MARADT", "A(Z) {0}. KÖR TELJESÍTVE!", "LEJÁRT AZ IDŐ! EZ A KÖR ÚJRAINDUL", "TÖMJÉTEK BE A LYUKAKAT!", "ÁLLÍTSÁTOK BE A HELYZETET ÉS A SZÖGET A LÉZER VISSZAVERÉSÉHEZ!"),
    "fi": ("KIERROS {0}/3    {1:0.0} s JÄLJELLÄ", "KIERROS {0} LÄPÄISTY!", "AIKA LOPPUI! TÄMÄ KIERROS ALKAA UUDELLEEN", "TUKKIKAA REIÄT!", "SÄÄTÄKÄÄ SIJAINTEJA JA KULMIA HEIJASTAAKSENNE LASERIN!"),
    "bg": ("РУНД {0}/3    ОСТАВАТ {1:0.0} сек", "РУНД {0} ЗАВЪРШЕН!", "ВРЕМЕТО ИЗТЕЧЕ! ТОЗИ РУНД ЗАПОЧВА ОТНОВО", "ЗАПУШЕТЕ ДУПКИТЕ!", "НАГЛАСЕТЕ ПОЗИЦИИТЕ И ЪГЛИТЕ, ЗА ДА ОТРАЗИТЕ ЛАЗЕРА!"),
    "vi": ("VÒNG {0}/3    CÒN {1:0.0} giây", "HOÀN THÀNH VÒNG {0}!", "HẾT GIỜ! BẮT ĐẦU LẠI VÒNG NÀY", "BỊT CÁC LỖ LẠI!", "ĐIỀU CHỈNH VỊ TRÍ VÀ GÓC ĐỂ PHẢN XẠ TIA LASER!"),
    "pl": ("RUNDA {0}/3    ZOSTAŁO {1:0.0} s", "RUNDA {0} UKOŃCZONA!", "KONIEC CZASU! TA RUNDA ZACZYNA SIĘ OD NOWA", "ZATKAJCIE OTWORY!", "USTAWCIE POZYCJE I KĄTY, ABY ODBIĆ LASER!"),
    "ro": ("RUNDA {0}/3    AU RĂMAS {1:0.0} s", "RUNDA {0} TERMINATĂ!", "TIMPUL A EXPIRAT! RUNDA REÎNCEPE", "ASTUPAȚI GĂURILE!", "REGLAȚI POZIȚIILE ȘI UNGHIURILE PENTRU A REFLECTA LASERUL!"),
}

QUALITY_OVERRIDES = {
    "zh-CN": {
        "ready_room_restriction_redraw_allowed": "可以在起始房间内重新绘制。",
    },
    "zh-TW": {
        "ready_room_restriction_redraw_allowed": "可以在起始房間內重新繪製。",
    },
    "sv": {
        "ability_summary": "Ben {0:0.0}: {1}   Arm {2:0.0}: {3}   Kropp {4:0.0}: {5}",
    },
    "tr": {
        "msg_personal_ink_over": "Onaylanamıyor: kişisel INK {0:0.#}/{1:0}. {2:0} azaltın.",
        "msg_team_ink_over": "Onaylanamıyor: takım INK {0:0.#}/{1:0}. {2:0} azaltın.",
        "multi_lobby_status_default": "Oda: Birlikte Çiz\nID: ABC123\n2 / 4\n\nOynanabilir lobi alanı\nBeklerken kutular, toplar ve zıplama rampalarıyla oynayın.",
        "multi_default_room_name": "Birlikte Çiz",
        "stage_editor_link_target": "Bağlantı hedefi",
        "challenge_time_remaining": "Kalan süre: {0:0.0}",
        "stage_editor_status_placed_point": "{0}, ({1:0.0}, {2:0.0}) konumuna yerleştirildi.",
    },
}

PROTECTED = re.compile(
    r"(\{[^{}]+\}|</?[^>]+>|\\n|\b(?:NICO DRAW|DRAW|INK|WASD|SPACE|ENTER|ESC|TAB|F[1-9]?|P[1-4])\b)",
    re.IGNORECASE,
)
HAS_WORD = re.compile(r"[A-Za-z]")


def decode_csharp_string(value: str) -> str:
    # Localization initializers use the JSON-compatible C# escape subset.
    return json.loads('"' + value + '"')


def load_csharp_table(name: str, next_marker: str) -> list[dict[str, str]]:
    source = MANAGER_SOURCE.read_text(encoding="utf-8")
    marker = f"Dictionary<string, string> {name}"
    start = source.index(marker)
    end = source.index(next_marker, start)
    pattern = re.compile(
        r'\{\s*"((?:\\.|[^"\\])*)"\s*,\s*"((?:\\.|[^"\\])*)"\s*\},'
    )
    return [
        {"key": decode_csharp_string(match.group(1)), "value": decode_csharp_string(match.group(2))}
        for match in pattern.finditer(source[start:end])
    ]


def load_source_entries() -> list[dict[str, str]]:
    entries: list[dict[str, str]] = []
    positions: dict[str, int] = {}

    # Match LocalizationManager's lookup priority: external, generated, built-in.
    # Build from lowest to highest priority so later assignments win.
    sources = (
        load_csharp_table("English", "Dictionary<string, string> GeneratedJapanese"),
        load_csharp_table("GeneratedEnglish", "public static event Action LanguageChanged"),
    )
    for source_entries in sources:
        for item in source_entries:
            key = item["key"]
            if key in positions:
                entries[positions[key]] = item
            else:
                positions[key] = len(entries)
                entries.append(item)

    for file_name in SOURCE_FILES:
        payload = json.loads((LOCALIZATION_DIR / file_name).read_text(encoding="utf-8"))
        for entry in payload["entries"]:
            key = entry["key"]
            item = {"key": key, "value": entry.get("value", "")}
            if key in positions:
                # Resource paths are loaded in order at runtime; later tables
                # intentionally override an earlier value for the same key.
                entries[positions[key]] = item
            else:
                positions[key] = len(entries)
                entries.append(item)
    return entries


def ensure_models(target_codes: set[str]) -> None:
    argostranslate.package.update_package_index()
    available = argostranslate.package.get_available_packages()
    installed = {
        language.code
        for language in argostranslate.translate.get_installed_languages()
    }
    for target in sorted(target_codes):
        if target in installed:
            continue
        model = next(
            (item for item in available if item.from_code == "en" and item.to_code == target),
            None,
        )
        if model is None:
            raise RuntimeError(f"No Argos model for en -> {target}")
        print(f"Downloading model en -> {target}", flush=True)
        argostranslate.package.install_from_path(model.download())
        installed.add(target)


def get_translation(target_code: str):
    languages = argostranslate.translate.get_installed_languages()
    source = next(language for language in languages if language.code == "en")
    target = next(language for language in languages if language.code == target_code)
    return source.get_translation(target)


def translate_segment(segment: str, translator, cache: dict[str, str]) -> str:
    if not HAS_WORD.search(segment):
        return segment
    leading = segment[: len(segment) - len(segment.lstrip())]
    trailing = segment[len(segment.rstrip()) :]
    core = segment.strip()
    if not core:
        return segment
    if core not in cache:
        translated = translator.translate(core).strip()
        cache[core] = translated or core
    return leading + cache[core] + trailing


def translate_value(value: str, translator, cache: dict[str, str]) -> str:
    # Translate around format placeholders and gameplay tokens. This guarantees
    # string.Format signatures remain byte-for-byte intact.
    parts = PROTECTED.split(value)
    return "".join(
        part if PROTECTED.fullmatch(part or "") else translate_segment(part, translator, cache)
        for part in parts
    )


def get_manual_overrides(locale: str) -> dict[str, str]:
    overrides = dict(zip(CRITICAL_KEYS, CRITICAL_OVERRIDES[locale]))
    round_values = dict(zip(ROUND_OVERRIDE_KEYS, ROUND_OVERRIDES[locale]))
    overrides.update({
        "laser_relay_monitor": round_values["monitor"],
        "aquarium_seal_monitor": round_values["monitor"],
        "laser_relay_round_clear": round_values["round_clear"],
        "aquarium_seal_round_clear": round_values["round_clear"],
        "laser_relay_timeout": round_values["timeout"],
        "aquarium_seal_timeout": round_values["timeout"],
        "aquarium_seal_box_hint": round_values["plug_holes"],
        "laser_relay_hint": round_values["reflect_laser"],
    })
    overrides.update(QUALITY_OVERRIDES.get(locale, {}))
    return overrides


def write_meta(path: Path) -> None:
    meta = path.with_suffix(path.suffix + ".meta")
    if meta.exists():
        return
    guid = uuid.uuid5(uuid.NAMESPACE_URL, "nico-draw-localization/" + path.name).hex
    meta.write_text(
        "fileFormatVersion: 2\n"
        f"guid: {guid}\n"
        "TextScriptImporter:\n"
        "  externalObjects: {}\n"
        "  userData: \n"
        "  assetBundleName: \n"
        "  assetBundleVariant: \n",
        encoding="utf-8",
        newline="\n",
    )


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--targets", nargs="*", choices=TARGETS.keys())
    args = parser.parse_args()
    selected = args.targets or list(TARGETS.keys())
    source_entries = load_source_entries()
    ensure_models({TARGETS[locale] for locale in selected})

    for locale in selected:
        target_code = TARGETS[locale]
        print(f"Translating {locale} ({len(source_entries)} entries)", flush=True)
        translator = get_translation(target_code)
        cache: dict[str, str] = {}
        translated = [
            {"key": item["key"], "value": translate_value(item["value"], translator, cache)}
            for item in source_entries
        ]
        if locale == "zh-TW":
            try:
                from opencc import OpenCC
            except ImportError as error:
                raise RuntimeError(
                    "zh-TW generation requires opencc-python-reimplemented"
                ) from error
            converter = OpenCC("s2tw")
            for item in translated:
                item["value"] = converter.convert(item["value"])
        overrides = get_manual_overrides(locale)
        for item in translated:
            if item["key"] in overrides:
                item["value"] = overrides[item["key"]]
        output = LOCALIZATION_DIR / f"{locale}.json"
        output.write_text(
            json.dumps({"entries": translated}, ensure_ascii=False, indent=2) + "\n",
            encoding="utf-8",
            newline="\n",
        )
        write_meta(output)
    return 0


if __name__ == "__main__":
    sys.exit(main())
