import requests, re, sys
H = {'User-Agent': 'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 Chrome/124 Safari/537.36', 'Accept-Language': 'en-US,en;q=0.9'}
t = requests.get(sys.argv[1], headers=H, cookies={'CONSENT': 'YES+1'}, timeout=30).text
print(len(t))
for m in list(re.finditer(r'.{80}subscribers.{40}', t))[:3]:
    print('SUB>', m.group(0))
print(re.findall(r'channel_id=(UC[\w-]{22})', t)[:2], re.findall(r'"externalId":"(UC[\w-]{22})', t)[:2])
ex = re.findall(r'(?:x|twitter)\.com%2F[A-Za-z0-9_]+', t)[:5]
print(ex)
