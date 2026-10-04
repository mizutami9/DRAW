import os
import subprocess
import re

MODEL_DIR = r"D:\04_tiktok_game\NICODROW\work\models\small.en"
os.makedirs(MODEL_DIR, exist_ok=True)

def get_direct_url(url):
    cmd = f'curl.exe -s -I -L "{url}"'
    res = subprocess.check_output(cmd, shell=True).decode('utf-8', errors='ignore')
    locations = re.findall(r'Location:\s*([^\r\n]+)', res, re.IGNORECASE)
    if locations:
        return locations[-1].strip()
    return url

files = [
    ("config.json", "https://huggingface.co/Systran/faster-whisper-small.en/raw/main/config.json"),
    ("tokenizer.json", "https://huggingface.co/Systran/faster-whisper-small.en/raw/main/tokenizer.json"),
    ("vocabulary.txt", "https://huggingface.co/Systran/faster-whisper-small.en/raw/main/vocabulary.txt"),
    ("model.bin", "https://huggingface.co/Systran/faster-whisper-small.en/resolve/main/model.bin"),
]

for name, orig_url in files:
    dest = os.path.join(MODEL_DIR, name)
    if os.path.exists(dest) and os.path.getsize(dest) > 1000 and name != "model.bin":
        print(f"Already exists: {name}")
        continue
    
    print(f"Resolving direct URL for {name}...")
    direct_url = get_direct_url(orig_url)
    print(f"Downloading {name}...")
    # --retry 20 で、直URLに対して -C - でレジューム
    cmd = f'curl.exe -L -C - --retry 20 --retry-delay 2 --retry-all-errors -o "{dest}" "{direct_url}"'
    subprocess.check_call(cmd, shell=True)

print("small.en download finished successfully!")
