import os
import subprocess

MODEL_DIR = r"D:\04_tiktok_game\NICODROW\work\models\medium.en"
os.makedirs(MODEL_DIR, exist_ok=True)

files = [
    ("config.json", "https://huggingface.co/Systran/faster-whisper-medium.en/raw/main/config.json"),
    ("tokenizer.json", "https://huggingface.co/Systran/faster-whisper-medium.en/raw/main/tokenizer.json"),
    ("vocabulary.txt", "https://huggingface.co/Systran/faster-whisper-medium.en/raw/main/vocabulary.txt"),
    ("model.bin", "https://huggingface.co/Systran/faster-whisper-medium.en/resolve/main/model.bin"),
]

for name, url in files:
    dest = os.path.join(MODEL_DIR, name)
    print(f"Downloading {name}...")
    cmd = f'curl.exe -L -C - --retry 10 --retry-delay 2 --retry-all-errors -o "{dest}" "{url}"'
    subprocess.check_call(cmd, shell=True)

print("Model download complete!")
