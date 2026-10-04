from __future__ import annotations

from collections import Counter
from concurrent.futures import ThreadPoolExecutor, as_completed
from pathlib import Path
from urllib.parse import quote
import json
import sys

import openpyxl
import requests


sys.stdout.reconfigure(encoding="utf-8")
FILE = Path(r"C:\workSpace\PICO\NICO_DRAW_creator_outreach_FINAL_A100.xlsx")
OUTPUT = Path(r"C:\workSpace\PICO\Tools\audit\a100_proof_url_status.json")
UA = {"User-Agent": "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 Chrome/124 Safari/537.36"}


def check(url):
    try:
        if "youtube.com" in url:
            endpoint = "https://www.youtube.com/oembed?format=json&url=" + quote(url, safe="")
            response = requests.get(endpoint, headers=UA, timeout=20)
            return {"url": url, "service": "YouTube", "status": response.status_code, "alive": response.status_code == 200}
        response = requests.get(url, headers=UA, timeout=20, allow_redirects=True)
        # Twitch VOD routes return the channel shell even when age/login gates apply.
        # A 404 is treated as dead; 200/3xx/login-gated 4xx remain reviewable.
        return {"url": url, "service": "Twitch", "status": response.status_code, "alive": response.status_code != 404}
    except Exception as exc:
        return {"url": url, "service": "unknown", "status": None, "alive": None, "error": f"{type(exc).__name__}: {exc}"}


def main():
    book = openpyxl.load_workbook(FILE, data_only=True)
    sheet = book.worksheets[0]
    urls = sorted({str(row[index]) for row in sheet.iter_rows(min_row=2, values_only=True) for index in (20, 21, 22) if str(row[index] or "").startswith("http")})
    results = []
    with ThreadPoolExecutor(max_workers=16) as pool:
        futures = [pool.submit(check, url) for url in urls]
        for future in as_completed(futures):
            results.append(future.result())
    results.sort(key=lambda item: item["url"])
    OUTPUT.write_text(json.dumps({"checked": len(urls), "results": results}, ensure_ascii=False, indent=2), encoding="utf-8")
    print(json.dumps({
        "checked": len(urls), "alive": sum(item["alive"] is True for item in results),
        "dead": [item for item in results if item["alive"] is False],
        "indeterminate": [item for item in results if item["alive"] is None],
        "statuses": Counter(f"{item['service']}:{item['status']}" for item in results),
        "output": str(OUTPUT),
    }, ensure_ascii=False, indent=2))


if __name__ == "__main__":
    main()
