import subprocess
import re
import sys

sys.stdout.reconfigure(encoding='utf-8')

targets = [
    r"C:\Users\mizut",
    r"C:\workSpace",
    r"C:\Program Files",
    r"C:\Program Files (x86)",
    r"C:\ProgramData",
    r"C:\Windows"
]

for t in targets:
    try:
        cmd = f'robocopy "{t}" "{t}" /L /S /NJH /BYTES /XJ /R:0 /W:0'
        res = subprocess.run(cmd, shell=True, capture_output=True, text=True, errors='ignore')
        m = re.findall(r'Bytes\s*:\s*(\d+)', res.stdout)
        if m:
            bytes_val = int(m[-1])
            print(f"{t:30s}: {bytes_val / 1024 / 1024 / 1024:6.2f} GB")
        else:
            print(f"{t:30s}: Failed to parse robocopy")
    except Exception as e:
        print(f"{t:30s}: Error {e}")
