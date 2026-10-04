import sys

sys.stdout.reconfigure(encoding='utf-8')

with open(r'D:\04_tiktok_game\NICODROW\output\nicodraw_feedback_report.md', 'r', encoding='utf-8', errors='ignore') as f:
    text = f.read()

print(text[4000:10000])
