#!/usr/bin/env python3
"""Complete every shipped localization table from the effective English table.

Existing reviewed translations are retained. Missing, blank, or accidentally
English values are translated locally with NLLB-200. The script also creates
the Malay table required by the Steam language declaration. No game text is
sent to an external service.
"""

from __future__ import annotations

import argparse
import json
import re
import sys
import uuid
from pathlib import Path

import ctranslate2
import sentencepiece as spm


ROOT = Path(__file__).resolve().parents[1]
LOCALIZATION_DIR = ROOT / "Assets" / "Resources" / "Localization"
MANAGER_SOURCE = ROOT / "Assets" / "Scripts" / "LocalizationManager.cs"
LANGUAGES_PATH = LOCALIZATION_DIR / "languages.json"
MODEL_DIR = Path(r"D:\04_tiktok_game\NICODROW\models\nllb-200-distilled-600M-ct2-int8")

TARGETS = {
    "fr": "fra_Latn", "it": "ita_Latn", "de": "deu_Latn",
    "es": "spa_Latn", "es-419": "spa_Latn", "pt-BR": "por_Latn",
    "pt-PT": "por_Latn", "ru": "rus_Cyrl", "zh-CN": "zho_Hans",
    "zh-TW": "zho_Hant", "ko": "kor_Hang", "ar": "arb_Arab",
    "id": "ind_Latn", "uk": "ukr_Cyrl", "nl": "nld_Latn",
    "el": "ell_Grek", "sv": "swe_Latn", "th": "tha_Thai",
    "cs": "ces_Latn", "da": "dan_Latn", "tr": "tur_Latn",
    "no": "nob_Latn", "hu": "hun_Latn", "fi": "fin_Latn",
    "bg": "bul_Cyrl", "vi": "vie_Latn", "pl": "pol_Latn",
    "ro": "ron_Latn", "ms": "zsm_Latn",
}

# These lines are intentionally spoken in English in every locale. Keeping an
# explicit entry still guarantees ID parity and prevents accidental fallback.
INTENTIONAL_ENGLISH_KEYS = {
    "boss_health",
    "escort_friend_word_go",
    "escort_friend_word_jump",
    "escort_friend_word_move",
    "escort_friend_word_run",
    "escort_friend_word_wait",
    "multi_local",
    "option_bgm",
    "tower_ally_speech_get_ready",
    "tower_ally_speech_ready",
    "tower_ally_speech_run",
    "tower_ally_speech_together",
    "tower_ally_speech_wait",
    "tower_defense_rank_up",
    "umbrella_friend_speech_dry_and_clever",
    "umbrella_friend_speech_fools_get_wet",
    "umbrella_friend_speech_keep_close",
    "umbrella_friend_speech_nice_rain",
    "umbrella_friend_speech_puddle_weather",
}

MANUAL_OVERRIDES = {
    "id": {
        "stage_object_enemy_flyer_zigzag": "Musuh Terbang Zigzag",
        "stage_object_enemy_flyer_orbit": "Musuh Terbang Mengorbit",
    },
}

MANUAL_OVERRIDES = {
    "id": {
        "stage_object_enemy_flyer_zigzag": "Musuh Terbang Zigzag",
        "stage_object_enemy_flyer_orbit": "Musuh Terbang Mengorbit",
    },
}

PROTECTED_RE = re.compile(
    r"(\{[^{}]+\}|</?[^>]+>|\b(?:NICO DRAW|INK|WASD|SPACE|ENTER|ESC|TAB|SHIFT|ALT|CTRL|F[1-9]?|P[1-4])\b)",
    re.IGNORECASE,
)
FORMAT_RE = re.compile(r"\{[^{}]+\}")
HAS_WORD_RE = re.compile(r"[A-Za-z]{2,}")


def decode_csharp(value: str) -> str:
    return json.loads('"' + value + '"')


def load_csharp_table(name: str, next_marker: str) -> list[dict[str, str]]:
    source = MANAGER_SOURCE.read_text(encoding="utf-8")
    start = source.index(f"Dictionary<string, string> {name}")
    end = source.index(next_marker, start)
    pattern = re.compile(r'\{\s*"((?:\\.|[^"\\])*)"\s*,\s*"((?:\\.|[^"\\])*)"\s*\},')
    return [
        {"key": decode_csharp(m.group(1)), "value": decode_csharp(m.group(2))}
        for m in pattern.finditer(source[start:end])
    ]


def load_effective_english() -> list[dict[str, str]]:
    ordered: list[dict[str, str]] = []
    positions: dict[str, int] = {}
    sources = (
        load_csharp_table("English", "Dictionary<string, string> GeneratedJapanese"),
        load_csharp_table("GeneratedEnglish", "public static event Action LanguageChanged"),
    )
    for entries in sources:
        for item in entries:
            if item["key"] in positions:
                ordered[positions[item["key"]]] = item
            else:
                positions[item["key"]] = len(ordered)
                ordered.append(item)
    for name in ("en.json", "stage_decorations_en.json"):
        for item in json.loads((LOCALIZATION_DIR / name).read_text(encoding="utf-8"))["entries"]:
            normalized = {"key": item["key"], "value": item.get("value", "")}
            if item["key"] in positions:
                ordered[positions[item["key"]]] = normalized
            else:
                positions[item["key"]] = len(ordered)
                ordered.append(normalized)
    return ordered


def load_effective_japanese() -> dict[str, str]:
    table: dict[str, str] = {}
    for entries in (
        load_csharp_table("Japanese", "Dictionary<string, string> English"),
        load_csharp_table("GeneratedJapanese", "Dictionary<string, string> GeneratedEnglish"),
    ):
        for item in entries:
            table[item["key"]] = item["value"]
    source = MANAGER_SOURCE.read_text(encoding="utf-8")
    start = source.index("private static bool TryGetJapaneseOverride")
    end = source.index("public static string Format", start)
    pattern = re.compile(
        r'case\s+"((?:\\.|[^"\\])*)"\s*:\s*value\s*=\s*"((?:\\.|[^"\\])*)"'
    )
    for match in pattern.finditer(source[start:end]):
        table[decode_csharp(match.group(1))] = decode_csharp(match.group(2))
    for name in ("ja.json", "stage_decorations_ja.json"):
        for item in json.loads((LOCALIZATION_DIR / name).read_text(encoding="utf-8"))["entries"]:
            table[item["key"]] = item.get("value", "")
    return table


def load_existing(locale: str) -> dict[str, str]:
    path = LOCALIZATION_DIR / f"{locale}.json"
    if not path.exists():
        return {}
    return {
        item["key"]: item.get("value", "")
        for item in json.loads(path.read_text(encoding="utf-8"))["entries"]
    }


class LocalNllbTranslator:
    def __init__(self) -> None:
        if not MODEL_DIR.exists():
            raise RuntimeError(f"Local NLLB model is missing: {MODEL_DIR}")
        self.sentencepiece = spm.SentencePieceProcessor()
        self.sentencepiece.load(str(MODEL_DIR / "sentencepiece.bpe.model"))
        self.translator = ctranslate2.Translator(
            str(MODEL_DIR), device="cpu", compute_type="int8"
        )

    def translate_segments(self, segments: list[str], target: str, source: str) -> list[str]:
        output: list[str] = []
        for start in range(0, len(segments), 32):
            batch = segments[start : start + 32]
            tokens = [
                [source] + self.sentencepiece.encode_as_pieces(text) + ["</s>"]
                for text in batch
            ]
            results = self.translator.translate_batch(
                tokens,
                target_prefix=[[target]] * len(tokens),
                beam_size=2,
                max_batch_size=32,
            )
            for result in results:
                hypothesis = result.hypotheses[0]
                if hypothesis and hypothesis[0] == target:
                    hypothesis = hypothesis[1:]
                if hypothesis and hypothesis[-1] == "</s>":
                    hypothesis = hypothesis[:-1]
                decoded = self.sentencepiece.decode_pieces(hypothesis).strip()
                # The converted NLLB model can rarely emit an internal subtitle
                # color marker for very short nouns. It is not part of the
                # translation and would be interpreted as a format placeholder.
                decoded = re.sub(
                    r"\{\\+CH[0-9A-Fa-f]+\}", "", decoded, flags=re.IGNORECASE
                ).strip()
                output.append(decoded)
            print(f"  translated segments {min(start + 32, len(segments))}/{len(segments)}", flush=True)
        return output

    def translate_values(
        self, values: list[str], target: str, source: str = "eng_Latn"
    ) -> list[str]:
        layouts: list[list[tuple[str, int | None, str, str, bool]]] = []
        translatable: list[str] = []
        for value in values:
            layout: list[tuple[str, int | None, str, str, bool]] = []
            for part in PROTECTED_RE.split(value):
                if not part or PROTECTED_RE.fullmatch(part):
                    layout.append((part, None, "", "", False))
                    continue
                leading = part[: len(part) - len(part.lstrip())]
                trailing = part[len(part.rstrip()) :]
                core = part.strip()
                if any(character.isalpha() for character in core):
                    letters = "".join(character for character in core if character.isalpha())
                    restore_upper = bool(letters) and letters == letters.upper()
                    # NLLB often treats all-caps, one-word UI labels as product
                    # tokens. Sentence casing makes their meaning translatable;
                    # the original display casing is restored afterwards.
                    model_input = core.lower().capitalize() if restore_upper else core
                    index = len(translatable)
                    translatable.append(model_input)
                    layout.append(("", index, leading, trailing, restore_upper))
                else:
                    layout.append((part, None, "", "", False))
            layouts.append(layout)
        translated = self.translate_segments(translatable, target, source)
        return [
            "".join(
                literal if index is None else leading
                + (translated[index].upper() if restore_upper else translated[index])
                + trailing
                for literal, index, leading, trailing, restore_upper in layout
            )
            for layout in layouts
        ]


def needs_translation(source: str, existing: str | None, locale: str, key: str) -> bool:
    if key in INTENTIONAL_ENGLISH_KEYS:
        return False
    if existing is None or not existing.strip():
        return HAS_WORD_RE.search(source) is not None
    return existing == source and HAS_WORD_RE.search(source) is not None


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
        encoding="utf-8", newline="\n",
    )


def ensure_malay_definition() -> None:
    payload = json.loads(LANGUAGES_PATH.read_text(encoding="utf-8"))
    if any(entry["code"] == "ms" for entry in payload["entries"]):
        return
    payload["entries"].append({
        "code": "ms", "nativeName": "Bahasa Melayu", "fallbackCode": "en",
        "resourcePaths": ["Localization/ms"], "fontResourcePath": "",
        "systemFontNames": ["Segoe UI", "Arial", "Noto Sans"],
        "cultureCode": "ms-MY", "uiTextScale": 0.68,
        "listSeparator": " / ", "rightToLeft": False,
    })
    LANGUAGES_PATH.write_text(
        json.dumps(payload, ensure_ascii=False, indent=2) + "\n",
        encoding="utf-8", newline="\n",
    )


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--targets", nargs="*", choices=TARGETS.keys())
    args = parser.parse_args()
    selected = args.targets or list(TARGETS)
    source_entries = load_effective_english()
    source_map = {item["key"]: item["value"] for item in source_entries}
    japanese_map = load_effective_japanese()
    if len(source_map) != 1200:
        raise RuntimeError(f"Expected 1200 effective English IDs, found {len(source_map)}")

    translator = LocalNllbTranslator()
    for locale in selected:
        existing = load_existing(locale)
        keys = [
            item["key"] for item in source_entries
            if needs_translation(item["value"], existing.get(item["key"]), locale, item["key"])
        ]
        print(f"{locale}: completing {len(keys)} of {len(source_entries)} entries", flush=True)
        translated = translator.translate_values([source_map[key] for key in keys], TARGETS[locale])
        replacements = dict(zip(keys, translated))
        unchanged_phrases = []
        for key in keys:
            if replacements.get(key) != source_map[key] or key in INTENTIONAL_ENGLISH_KEYS:
                continue
            remainder = PROTECTED_RE.sub("", source_map[key])
            if len(HAS_WORD_RE.findall(remainder)) >= 2 and japanese_map.get(key):
                unchanged_phrases.append(key)
        if unchanged_phrases:
            print(
                f"  retrying {len(unchanged_phrases)} untranslated phrases from Japanese",
                flush=True,
            )
            retried = translator.translate_values(
                [japanese_map[key] for key in unchanged_phrases],
                TARGETS[locale],
                "jpn_Jpan",
            )
            replacements.update(zip(unchanged_phrases, retried))
        completed: list[dict[str, str]] = []
        for item in source_entries:
            key = item["key"]
            if key in INTENTIONAL_ENGLISH_KEYS:
                value = item["value"]
            elif key in replacements:
                value = replacements[key]
            else:
                value = existing.get(key, item["value"])
            value = MANUAL_OVERRIDES.get(locale, {}).get(key, value)
            value = MANUAL_OVERRIDES.get(locale, {}).get(key, value)
            if sorted(FORMAT_RE.findall(value)) != sorted(FORMAT_RE.findall(item["value"])):
                raise RuntimeError(f"{locale}:{key} changed format placeholders: {value!r}")
            completed.append({"key": key, "value": value})
        path = LOCALIZATION_DIR / f"{locale}.json"
        path.write_text(
            json.dumps({"entries": completed}, ensure_ascii=False, indent=2) + "\n",
            encoding="utf-8", newline="\n",
        )
        write_meta(path)

    ensure_malay_definition()
    return 0


if __name__ == "__main__":
    sys.exit(main())
