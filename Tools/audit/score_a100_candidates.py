from __future__ import annotations

from collections import Counter
from datetime import date
from pathlib import Path
import json
import re
import sys

import openpyxl

from a100_config import detect_game, is_dm_done, multiplayer_title, normalize


sys.stdout.reconfigure(encoding="utf-8")
ROOT = Path(r"C:\workSpace\PICO")
CURRENT = ROOT / "NICO_DRAW_creator_outreach_FINAL_100PLUS.xlsx"
ORIGINAL = ROOT / "NICO_DRAW_creator_outreach_100PLUS_WORKING.xlsx"
DISCOVERY = ROOT / "Tools" / "audit" / "youtube_discovery.json"
ENRICHMENT = ROOT / "Tools" / "audit" / "a100_b_enrichment.json"
REFRESH = ROOT / "Tools" / "audit" / "creator_evidence_refresh.json"
OUTPUT = ROOT / "Tools" / "audit" / "a100_scored.json"
TODAY = date(2026, 10, 4)


def clean_url(value):
    value = str(value or "").strip()
    if value.startswith("https://www.youtube.com/watch") and "v=" not in value:
        return ""
    return value if value.startswith("http") else ""


def parse_number(value):
    if isinstance(value, (int, float)):
        return int(value)
    match = re.sub(r"[^0-9]", "", str(value or ""))
    return int(match) if match else None


def age(value):
    try:
        return (TODAY - date.fromisoformat(str(value)[:10])).days
    except ValueError:
        return 9999


def evidence_maps():
    metadata = {}
    source_urls = {}

    discovery = json.loads(DISCOVERY.read_text(encoding="utf-8"))
    for candidate in discovery.get("candidates", []):
        name = normalize(candidate.get("name"))
        items = []
        for proof in candidate.get("proofs", []):
            item = {
                "url": proof.get("url"), "title": proof.get("title"), "date": proof.get("date"),
                "game": proof.get("game"), "qualified": multiplayer_title(proof.get("title"), proof.get("game")),
                "source": "YouTube公式チャンネル/RSS",
            }
            metadata[item["url"]] = item
            items.append(item["url"])
        source_urls[name] = items

    refresh = json.loads(REFRESH.read_text(encoding="utf-8")).get("channels", {})
    for channel in refresh.values():
        for video in channel.get("videos") or []:
            url = f"https://www.twitch.tv/videos/{video.get('id')}"
            game = str((video.get("game") or {}).get("name") or "")
            title = video.get("title") or ""
            metadata[url] = {
                "url": url, "title": title, "date": str(video.get("createdAt") or "")[:10],
                "game": game, "qualified": True, "source": "Twitch個別VOD/API再確認",
            }

    original = openpyxl.load_workbook(ORIGINAL, data_only=True).worksheets[0]
    for values in original.iter_rows(min_row=2, values_only=True):
        name = normalize(values[2])
        urls = [url.strip() for url in str(values[21] or "").splitlines() if url.strip().startswith("http") and "x.com/" not in url]
        source_urls[name] = urls[:3]
        games = str(values[19] or "")
        for url in urls:
            if url not in metadata:
                metadata[url] = {
                    "url": url, "title": str(values[20] or ""), "date": str(values[22] or ""),
                    "game": games, "qualified": True, "source": "前回全件監査済み直接証拠",
                }

    enrichment = json.loads(ENRICHMENT.read_text(encoding="utf-8"))
    for candidate in enrichment.get("enriched", []):
        name = normalize(candidate.get("name"))
        urls = source_urls.setdefault(name, list(candidate.get("proofs") or []))
        for proof in candidate.get("additional_proofs", []):
            item = {
                "url": proof.get("url"), "title": proof.get("title"), "date": proof.get("date"),
                "game": proof.get("game"), "qualified": True, "source": "A格上げ追加調査",
            }
            metadata[item["url"]] = item
            if item["url"] not in urls:
                urls.append(item["url"])
    return source_urls, metadata


def main():
    source_urls, metadata = evidence_maps()
    book = openpyxl.load_workbook(CURRENT, data_only=True)
    sheet = book.worksheets[0]
    scored = []
    for values in sheet.iter_rows(min_row=2, values_only=True):
        name = str(values[2])
        name_key = normalize(name)
        displayed_count = sum(bool(values[idx]) for idx in (20, 21, 22))
        urls = source_urls.get(name_key, [])
        # Restored Twitch candidate is not in either source map.
        if name_key == normalize("EsBrilloG"):
            urls = ["https://www.twitch.tv/videos/2888685239"]
        proof_items = []
        seen = set()
        for url in urls:
            if not clean_url(url) or url in seen:
                continue
            seen.add(url)
            item = metadata.get(url, {"url": url, "title": "", "date": values[23], "game": values[19], "qualified": True, "source": "既存監査"})
            if item.get("qualified"):
                proof_items.append(item)

        x_verified = str(values[6] or "").startswith("http") and "確認済" in str(values[7] or "")
        official_contact = x_verified or "http" in str(values[13] or "") or "http" in str(values[15] or "")
        size_values = [parse_number(values[10]), parse_number(values[12])]
        size_values = [item for item in size_values if item is not None]
        max_size = max(size_values) if size_values else None
        recent = age(values[23]) <= 31
        very_recent = age(values[23]) <= 14
        dm_done = is_dm_done(name, values[6], values[9], values[11], values[13], values[15])

        reasons = []
        qualifies = False
        if dm_done:
            reasons.append("DM済み除外")
        elif not official_contact:
            reasons.append("本人公式の営業経路不足")
        elif not recent:
            reasons.append("活動日が31日超")
        elif max_size is not None and max_size > 50_000:
            reasons.append("規模50,000超")
        elif len(proof_items) >= 2:
            qualifies = True
            reasons.append(f"直接証拠{len(proof_items)}件・公式営業経路・直近活動")
        elif len(proof_items) == 1 and x_verified and very_recent:
            # One exceptionally recent, concrete multiplayer video is enough when the
            # title itself proves the 2–4 player/collab context and X is cross-verified.
            proof = proof_items[0]
            # The channel itself must be active within 14 days. A concrete co-op video
            # from the current quarter (95 days) is accepted as an established format.
            proof_recent = age(proof.get("date")) <= 95
            game = proof.get("game") or detect_game(proof.get("title"))
            title_strong = multiplayer_title(proof.get("title"), game)
            if proof_recent and title_strong:
                qualifies = True
                reasons.append("直近14日活動・95日以内の明確な協力動画・本人確認済みX")
            else:
                reasons.append("単一証拠の強度不足")
        else:
            reasons.append("A基準の証拠本数/直近性不足")

        scored.append({
            "no": values[0], "current_priority": values[1], "name": name, "language": values[4],
            "region": values[5], "x_url": values[6], "youtube_url": values[9], "twitch_url": values[11],
            "last_active": str(values[23]), "activity_age": age(values[23]), "max_size": max_size,
            "x_verified": x_verified, "official_contact": official_contact, "dm_done": dm_done,
            "displayed_proof_count": displayed_count, "qualified_proof_count": len(proof_items),
            "proofs": proof_items[:3], "a_qualified": qualifies, "reason": " / ".join(reasons),
        })

    OUTPUT.write_text(json.dumps({"scored": scored}, ensure_ascii=False, indent=2), encoding="utf-8")
    qualified = [item for item in scored if item["a_qualified"]]
    print(json.dumps({
        "total": len(scored), "a_qualified": len(qualified),
        "current_a_retained": sum(item["a_qualified"] and item["current_priority"] == "A" for item in scored),
        "b_upgraded": sum(item["a_qualified"] and item["current_priority"] == "B" for item in scored),
        "dm_done_matches": [item["name"] for item in scored if item["dm_done"]],
        "languages": Counter(item["language"] for item in qualified), "output": str(OUTPUT),
    }, ensure_ascii=False, indent=2))
    print("\nUPGRADES")
    for item in qualified:
        if item["current_priority"] == "B":
            print(f"{item['no']:>3} {item['name']} | proof={item['qualified_proof_count']} age={item['activity_age']} | {item['reason']}")


if __name__ == "__main__":
    main()
