import os
import sys

# Ensure UTF-8 output
sys.stdout.reconfigure(encoding='utf-8')

for folder in [r'D:\04_tiktok_game\NICODROW\work', r'D:\04_tiktok_game\NICODROW\output']:
    print(f"=== {folder} ===")
    if os.path.exists(folder):
        for root, dirs, files in os.walk(folder):
            depth = root.replace(folder, '').count(os.sep)
            if depth > 2:
                continue
            print(f"[{root}]")
            for f in files[:20]:
                fp = os.path.join(root, f)
                try:
                    sz = os.path.getsize(fp)
                    print(f"  {f} ({sz:,} bytes)")
                except Exception as e:
                    print(f"  {f} (error: {e})")
            if len(files) > 20:
                print(f"  ... and {len(files) - 20} more files")
    else:
        print("Not found")
