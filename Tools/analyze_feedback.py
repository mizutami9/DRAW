import json
import os
import re

INPUT_JSONL = r"D:\04_tiktok_game\NICODROW\output\segments_ja.jsonl"
FEEDBACK_OUT = r"D:\04_tiktok_game\NICODROW\output\nicodraw_feedback_report.md"

def format_ts(sec):
    h = int(sec) // 3600
    m = (int(sec) // 60) % 60
    s = int(sec) % 60
    return f"{h:02d}:{m:02d}:{s:02d}"

def main():
    segments = []
    with open(INPUT_JSONL, "r", encoding="utf-8") as f:
        for line in f:
            if line.strip():
                segments.append(json.loads(line))
                
    print(f"Loaded {len(segments)} segments.")
    
    # タイムスタンプ順
    segments.sort(key=lambda x: x["start"])
    
    # 各カテゴリの抽出用キーワード/パターン
    # 1. 褒めた点 (Praise / Positive)
    praise_kw = [
        "love", "cool", "cute", "fun", "awesome", "great", "nice", "good", "amazing", "favorite",
        "hilarious", "masterpiece", "genius", "creative", "look at", "so cute", "pretty",
        "かわいい", "最高", "すごい", "楽しい", "面白い", "好き", "いいじゃん", "天才", "傑作"
    ]
    
    # 2. 不満・問題点 (Complaint / Issue / Bug / Lag)
    issue_kw = [
        "lag", "stuck", "bug", "broken", "freeze", "crash", "disconnect", "ping", "delay", "glitch",
        "can't move", "not working", "why didn't", "fail", "hard", "unfair", "annoying",
        "ラグ", "止まった", "固まった", "動けない", "動かない", "壊れた", "バグ", "切断", "できない", "なんで"
    ]
    
    # 3. 分かりにくかった点 (Confusing / Question)
    confuse_kw = [
        "how do", "how to", "what does", "where do", "what are we supposed", "confused", "don't understand",
        "what is this", "how does", "what do i", "explain", "meter", "scale",
        "どうやって", "どうすれば", "何これ", "わからない", "意味", "ルール", "メーター"
    ]
    
    # 4. 笑った・驚いた・盛り上がった場面 (Laugh / Surprise / Excitement)
    exciting_kw = [
        "lmao", "haha", "omg", "holy", "shit", "what the fuck", "no way", "look at him", "look at me",
        "yoooo", "we did it", "let's go", "yess", "screaming", "died",
        "やった", "できた", "うわあ", "すごい", "草", "笑", "見てこれ", "やばい", "マジで"
    ]
    
    praise_list = []
    issue_list = []
    confuse_list = []
    exciting_list = []
    general_impressions = []
    
    for s in segments:
        st = s["start"]
        et = s["end"]
        ts = format_ts(st)
        en = s["text"]
        ja = s.get("ja_text", "")
        en_l = en.lower()
        ja_l = ja.lower()
        
        # 配信前の00:07:33以前はスキップ
        if st < 7 * 60 + 33:
            continue
            
        # 開発者への言及・全体感想
        if any(w in en_l for w in ["dev", "game", "out yet", "release", "stream", "developer", "overall", "think of"]):
            general_impressions.append((ts, en, ja))
            
        # 褒めた点
        if any(w in en_l or w in ja_l for w in praise_kw):
            praise_list.append((ts, en, ja))
            
        # 不満・問題点
        if any(w in en_l or w in ja_l for w in issue_kw):
            issue_list.append((ts, en, ja))
            
        # 分かりにくかった点
        if any(w in en_l or w in ja_l for w in confuse_kw):
            confuse_list.append((ts, en, ja))
            
        # 笑った・盛り上がった場面
        if any(w in en_l or w in ja_l for w in exciting_kw):
            exciting_list.append((ts, en, ja))

    print(f"Impressions: {len(general_impressions)}, Praise: {len(praise_list)}, Issues: {len(issue_list)}, Confusing: {len(confuse_list)}, Exciting: {len(exciting_list)}")
    
    # レポート Markdown を生成
    lines = [
        "# Twitch配信における「NICO DRAW」プレイ分析・フィードバックレポート",
        "",
        "## 1. 配信概要 & NICO DRAW プレイ区間",
        "",
        "- **対象動画**: `D:\\04_tiktok_game\\NICODROW\\twitch_fixed.mp4`",
        "- **動画総時間**: 02時間17分00秒",
        "- **NICO DRAW プレイ区間**:",
        "  - **開始時刻**: **00:07:33**（Starting Soon待機画面からゲームのDRAW/ロビー画面へ切り替わった瞬間）",
        "  - **終了時刻**: **02:17:00**（動画末尾まで全編プレイ）",
        "  - **実質プレイ時間**: 約2時間09分27秒（配信の94%以上がNICO DRAW）",
        "- **参加者（4人マルチプレイ）**:",
        "  - **1P: Oshen**（メイン配信者、ウサギ耳のピンク＆金髪VTuber）",
        "  - **2P: Klana**",
        "  - **3P: Bup (6'4)**",
        "  - **4P: Maya / Nova**",
        "",
        "---",
        "",
        "## 2. 配信者が述べた全体感想・背景",
        "",
    ]
    
    # 主要な全体感想をピックアップ
    seen = set()
    for ts, en, ja in general_impressions:
        if any(key in en.lower() for key in ["dev", "texted", "stream", "game", "not out", "play this", "fun", "like"]):
            key = en[:30]
            if key not in seen:
                seen.add(key)
                lines.append(f"- **[{ts}]**")
                lines.append(f"  - 英語: *\"{en}\"*")
                lines.append(f"  - 日本語: 「{ja}」")
                
    lines.extend([
        "",
        "---",
        "",
        "## 3. 褒めた点・好評だったポイント（Praise & Highlights）",
        "",
    ])
    
    # 褒めた点のハイライト
    seen = set()
    for ts, en, ja in praise_list:
        if any(k in en.lower() for k in ["cute", "love", "cool", "favorite", "masterpiece", "genius", "awesome", "pretty"]):
            key = en[:30]
            if key not in seen:
                seen.add(key)
                lines.append(f"- **[{ts}]**")
                lines.append(f"  - 英語: *\"{en}\"*")
                lines.append(f"  - 日本語: 「{ja}」")
                if len(seen) >= 20:
                    break

    lines.extend([
        "",
        "---",
        "",
        "## 4. 不満・問題点・バグ・気になった点（Issues & Friction）",
        "",
    ])
    
    # 不満・問題点のハイライト
    seen = set()
    for ts, en, ja in issue_list:
        if any(k in en.lower() for k in ["lag", "stuck", "bug", "disconnect", "freeze", "crash", "delay", "not work", "why didn't", "can't move"]):
            key = en[:30]
            if key not in seen:
                seen.add(key)
                lines.append(f"- **[{ts}]**")
                lines.append(f"  - 英語: *\"{en}\"*")
                lines.append(f"  - 日本語: 「{ja}」")
                if len(seen) >= 20:
                    break

    lines.extend([
        "",
        "---",
        "",
        "## 5. 分かりにくかった点・戸惑った点（Confusion & Learning Curve）",
        "",
    ])
    
    # 分かりにくかった点のハイライト
    seen = set()
    for ts, en, ja in confuse_list:
        if any(k in en.lower() for k in ["how do", "how to", "what does", "what are we", "confused", "don't understand", "scale", "meter"]):
            key = en[:30]
            if key not in seen:
                seen.add(key)
                lines.append(f"- **[{ts}]**")
                lines.append(f"  - 英語: *\"{en}\"*")
                lines.append(f"  - 日本語: 「{ja}」")
                if len(seen) >= 20:
                    break

    lines.extend([
        "",
        "---",
        "",
        "## 6. 笑った／驚いた／盛り上がった場面（Funny & Exciting Moments）",
        "",
    ])
    
    # 盛り上がった場面のハイライト
    seen = set()
    for ts, en, ja in exciting_list:
        if any(k in en.lower() for k in ["holy", "shit", "lmao", "haha", "look at", "we did it", "no way", "died", "yess"]):
            key = en[:30]
            if key not in seen:
                seen.add(key)
                lines.append(f"- **[{ts}]**")
                lines.append(f"  - 英語: *\"{en}\"*")
                lines.append(f"  - 日本語: 「{ja}」")
                if len(seen) >= 25:
                    break

    with open(FEEDBACK_OUT, "w", encoding="utf-8") as f_out:
        f_out.write("\n".join(lines))
        
    print(f"Feedback report written to: {FEEDBACK_OUT}")

if __name__ == "__main__":
    main()
