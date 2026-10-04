import subprocess
import os
import re

PY_EXE = r"C:\Users\mizut\AppData\Local\Programs\Python\Python311\python.exe"
DOWNLOAD_DIR = r"D:\04_tiktok_game\NICODROW\work\wheels_fixed"

# 1. 壊れたファイル削除
for f in os.listdir(DOWNLOAD_DIR):
    if "huggingface" in f:
        os.remove(os.path.join(DOWNLOAD_DIR, f))

# 2. huggingface-hub 0.x (e.g. 0.28.1 or 0.29.x) を取得
cmd = 'curl.exe -s -L "https://pypi.org/simple/huggingface-hub/"'
html = subprocess.check_output(cmd, shell=True).decode('utf-8', errors='ignore')
matches = re.findall(r'href=[\'"]([^\'"]+huggingface_hub-0\.[^\'"]+\.whl[^\'"]*)[\'"]', html)
print(f"Found {len(matches)} matching wheels")
chosen = matches[-1].split('#')[0]
if not chosen.startswith('http'):
    chosen = "https://pypi.org/simple/huggingface-hub/" + chosen
fn = os.path.basename(chosen.split('?')[0])
dest = os.path.join(DOWNLOAD_DIR, fn)
print(f"Downloading {fn} from {chosen}...")
subprocess.check_call(f'curl.exe -L -C - --retry 10 --retry-all-errors -o "{dest}" "{chosen}"', shell=True)

# 3. pip install 実行
print("Installing faster-whisper...")
subprocess.check_call([PY_EXE, "-m", "pip", "install", "--no-index", f"--find-links={DOWNLOAD_DIR}", "faster-whisper"])
print("SUCCESS!")
