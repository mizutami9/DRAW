import json
import urllib.request

res = urllib.request.urlopen("http://127.0.0.1:50021/speakers")
speakers = json.loads(res.read())
for s in speakers:
    styles = [f"{st['name']}(id:{st['id']})" for st in s["styles"]]
    print(f"- {s['name']}: {', '.join(styles)}")
