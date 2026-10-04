import json
import re
import sys

import requests

sys.stdout.reconfigure(encoding="utf-8")
url = "https://www.youtube.com/@yedyashaVODs/about"
text = requests.get(url, headers={"User-Agent": "Mozilla/5.0", "Accept-Language": "en-US,en;q=0.9"}, cookies={"CONSENT": "YES+1"}, timeout=30).text
patterns = ["channelExternalLinkViewModel", "x.com", "twitter.com", "aboutChannelViewModel", "ytInitialData"]
out = {"length": len(text)}
for pattern in patterns:
    positions = [m.start() for m in re.finditer(re.escape(pattern), text, re.I)][:5]
    out[pattern] = [text[max(0, position - 250): position + 500] for position in positions]
print(json.dumps(out, ensure_ascii=False, indent=2))
