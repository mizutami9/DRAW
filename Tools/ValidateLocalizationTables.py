#!/usr/bin/env python3
"""Strict structural validation for every declared NICO DRAW language."""

from __future__ import annotations

import json
import re
import sys
from pathlib import Path

from CompleteLocalizationTables import (
    INTENTIONAL_ENGLISH_KEYS,
    LOCALIZATION_DIR,
    MANAGER_SOURCE,
    PROTECTED_RE,
    decode_csharp,
    load_csharp_table,
    load_effective_english,
)


FORMAT_RE = re.compile(r"\{[^{}]+\}")
WORD_RE = re.compile(r"[A-Za-z]{2,}")


def load_table(paths: list[str]) -> tuple[dict[str, str], list[str]]:
    table: dict[str, str] = {}
    duplicates: list[str] = []
    for resource_path in paths:
        path = LOCALIZATION_DIR.parent / f"{resource_path}.json"
        payload = json.loads(path.read_text(encoding="utf-8"))
        for item in payload["entries"]:
            if item["key"] in table:
                duplicates.append(item["key"])
            table[item["key"]] = item.get("value", "")
    return table, duplicates


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
    external, _ = load_table(["Localization/ja", "Localization/stage_decorations_ja"])
    table.update(external)
    return table


def main() -> int:
    source = {item["key"]: item["value"] for item in load_effective_english()}
    japanese = load_effective_japanese()
    definitions = json.loads(
        (LOCALIZATION_DIR / "languages.json").read_text(encoding="utf-8")
    )["entries"]
    errors: list[str] = []
    print(f"languages={len(definitions)} reference_ids={len(source)}")
    if len(definitions) != 31:
        errors.append(f"expected 31 languages, found {len(definitions)}")
    if len(source) != 1200:
        errors.append(f"expected 1200 reference IDs, found {len(source)}")

    for definition in definitions:
        code = definition["code"]
        if code == "en":
            table, duplicates = dict(source), []
        elif code == "ja":
            table, duplicates = dict(japanese), []
        else:
            table, duplicates = load_table(definition["resourcePaths"])
        missing = sorted(source.keys() - table.keys())
        extra = sorted(table.keys() - source.keys())
        blank = sorted(key for key in source if not table.get(key, "").strip())
        bad_formats = sorted(
            key for key in source
            if key in table
            and sorted(FORMAT_RE.findall(source[key])) != sorted(FORMAT_RE.findall(table[key]))
        )
        replacement = sorted(key for key, value in table.items() if "\ufffd" in value)
        unexpected_english: list[str] = []
        if code not in {"ja", "en"}:
            for key, english in source.items():
                if key in INTENTIONAL_ENGLISH_KEYS or table.get(key) != english:
                    continue
                remainder = PROTECTED_RE.sub("", english)
                # A one-word identical value can be a valid loanword or acronym
                # in the target language (menu, laser, BGM, etc.). Multi-word
                # English phrases are treated as untranslated release blockers.
                if len(WORD_RE.findall(remainder)) >= 2:
                    unexpected_english.append(key)
        print(
            f"{code:6} ids={len(table):4} missing={len(missing):2} extra={len(extra):2} "
            f"blank={len(blank):2} format={len(bad_formats):2} "
            f"replacement={len(replacement):2} english={len(unexpected_english):2}"
        )
        for label, values in (
            ("missing", missing), ("extra", extra), ("blank", blank),
            ("format", bad_formats), ("replacement", replacement),
            ("unexpected English", unexpected_english),
        ):
            if values:
                errors.append(f"{code} {label}: {', '.join(values)}")
        # Japanese is intentionally split into the base and decoration tables;
        # retain its known overrides. Every generated locale must be unique.
        if duplicates and code not in {"ja", "en"}:
            errors.append(f"{code} duplicate IDs: {', '.join(sorted(set(duplicates)))}")

    if errors:
        print("\nVALIDATION FAILED", file=sys.stderr)
        for error in errors:
            print("- " + error, file=sys.stderr)
        return 1
    print("VALIDATION PASSED")
    return 0


if __name__ == "__main__":
    sys.exit(main())
