import subprocess
import json
import os
import sys

PY_EXE = r"C:\Users\mizut\AppData\Local\Programs\Python\Python311\python.exe"
DOWNLOAD_DIR = r"D:\04_tiktok_game\NICODROW\work\wheels"
os.makedirs(DOWNLOAD_DIR, exist_ok=True)

packages = [
    "numpy",
    "pyyaml",
    "tqdm",
    "colorama",
    "typing_extensions",
    "idna",
    "certifi",
    "h11",
    "httpcore",
    "anyio",
    "httpx",
    "click",
    "filelock",
    "fsspec",
    "packaging",
    "protobuf",
    "flatbuffers",
    "tokenizers",
    "huggingface_hub",
    "av",
    "onnxruntime",
    "ctranslate2",
    "faster_whisper",
]

import re

def get_wheel_url(pkg_name):
    simple_url = f"https://pypi.org/simple/{pkg_name}/"
    cmd = f'curl.exe -s -L --retry 5 --retry-delay 2 --retry-all-errors "{simple_url}"'
    html = subprocess.check_output(cmd, shell=True).decode('utf-8', errors='ignore')
    
    # Extract all .whl urls
    urls = re.findall(r'href=[\'"]([^\'"]+\.whl[^\'"]*)[\'"]', html)
    
    # Clean URLs (remove fragments like #sha256=...)
    clean_urls = []
    for u in urls:
        base_u = u.split('#')[0]
        if not base_u.startswith('http'):
            base_u = "https://pypi.org/simple/" + pkg_name + "/" + base_u
        clean_urls.append((os.path.basename(base_u.split('?')[0]), base_u))
    
    # Priority matching for cp311-win_amd64
    for fn, u in clean_urls:
        if "cp311" in fn and "win_amd64" in fn:
            return {"filename": fn, "url": u}
    for fn, u in clean_urls:
        if "abi3" in fn and "win_amd64" in fn:
            return {"filename": fn, "url": u}
    for fn, u in clean_urls:
        if "py3-none-any" in fn:
            return {"filename": fn, "url": u}
    for fn, u in clean_urls:
        if "py2.py3-none-any" in fn or "none-any" in fn:
            return {"filename": fn, "url": u}
    return None

for pkg in packages:
    print(f"Resolving {pkg}...")
    info = get_wheel_url(pkg)
    if not info:
        print(f"Could not find wheel for {pkg}")
        continue
    fn = info["filename"]
    url = info["url"]
    dest = os.path.join(DOWNLOAD_DIR, fn)
    if not os.path.exists(dest) or os.path.getsize(dest) == 0:
        print(f"Downloading {fn} via curl...")
        cmd = f'curl.exe -L -C - --retry 10 --retry-delay 2 --retry-all-errors -o "{dest}" "{url}"'
        subprocess.check_call(cmd, shell=True)
    else:
        print(f"Already downloaded {fn}")

print("All wheels downloaded! Now installing...")
subprocess.check_call([PY_EXE, "-m", "pip", "install", "--no-index", f"--find-links={DOWNLOAD_DIR}"] + packages)
print("Installation complete!")
