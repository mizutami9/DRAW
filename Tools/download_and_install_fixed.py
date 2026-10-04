import subprocess
import json
import os
import re
import sys

PY_EXE = r"C:\Users\mizut\AppData\Local\Programs\Python\Python311\python.exe"
DOWNLOAD_DIR = r"D:\04_tiktok_game\NICODROW\work\wheels_fixed"
os.makedirs(DOWNLOAD_DIR, exist_ok=True)

packages = [
    "typing_extensions",
    "idna",
    "certifi",
    "charset-normalizer",
    "urllib3",
    "requests",
    "h11",
    "sniffio",
    "anyio",
    "httpcore",
    "httpx",
    "colorama",
    "tqdm",
    "click",
    "filelock",
    "fsspec",
    "packaging",
    "pyyaml",
    "numpy",
    "flatbuffers",
    "protobuf",
    "tokenizers",
    "huggingface_hub",
    "av",
    "onnxruntime",
    "ctranslate2",
    "faster_whisper",
]

def get_wheel_info(pkg_name):
    url_name = pkg_name.replace('_', '-')
    simple_url = f"https://pypi.org/simple/{url_name}/"
    cmd = f'curl.exe -s -L --retry 5 --retry-delay 2 --retry-all-errors "{simple_url}"'
    html = subprocess.check_output(cmd, shell=True).decode('utf-8', errors='ignore')
    
    urls = re.findall(r'href=[\'"]([^\'"]+\.whl[^\'"]*)[\'"]', html)
    clean_urls = []
    for u in urls:
        base_u = u.split('#')[0]
        if not base_u.startswith('http'):
            base_u = f"https://pypi.org/simple/{url_name}/" + base_u
        clean_urls.append((os.path.basename(base_u.split('?')[0]), base_u))
    
    # 逆順（新しい順）にチェック
    # 1. cp311-win_amd64
    for fn, u in reversed(clean_urls):
        if "cp311" in fn and "win_amd64" in fn:
            return fn, u
    # 2. abi3-win_amd64
    for fn, u in reversed(clean_urls):
        if "abi3" in fn and "win_amd64" in fn:
            return fn, u
    # 3. win_amd64 (not cp312, not cp310, etc.)
    for fn, u in reversed(clean_urls):
        if "win_amd64" in fn and not any(f"cp{v}" in fn for v in [36,37,38,39,310,312,313]):
            return fn, u
    # 4. py3-none-any
    for fn, u in reversed(clean_urls):
        if "py3-none-any" in fn:
            return fn, u
    # 5. none-any
    for fn, u in reversed(clean_urls):
        if "none-any" in fn:
            return fn, u
    return None, None

for pkg in packages:
    print(f"Resolving {pkg}...")
    fn, url = get_wheel_info(pkg)
    if not fn:
        print(f"FAILED to resolve {pkg}")
        continue
    dest = os.path.join(DOWNLOAD_DIR, fn)
    print(f"Selected: {fn}")
    if not os.path.exists(dest) or os.path.getsize(dest) == 0:
        print(f"Downloading {fn} via curl...")
        cmd = f'curl.exe -L -C - --retry 10 --retry-delay 2 --retry-all-errors -o "{dest}" "{url}"'
        subprocess.check_call(cmd, shell=True)
    else:
        print(f"Already exists: {fn}")

print("\nAll wheels downloaded! Now installing...")
subprocess.check_call([PY_EXE, "-m", "pip", "install", "--no-index", f"--find-links={DOWNLOAD_DIR}", "faster-whisper"])
print("\nInstallation SUCCESSFUL!")
