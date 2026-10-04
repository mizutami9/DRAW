import os
import sys

sys.stdout.reconfigure(encoding='utf-8')

output_dir = r'D:\04_tiktok_game\NICODROW\output'
for fname in os.listdir(output_dir):
    fpath = os.path.join(output_dir, fname)
    sz = os.path.getsize(fpath)
    print(f"=== {fname} ({sz:,} bytes) ===")
    if fname.endswith(('.txt', '.srt', '.md')):
        with open(fpath, 'r', encoding='utf-8', errors='ignore') as f:
            lines = [f.readline() for _ in range(15)]
            print("".join(lines))
    print("-" * 50)
