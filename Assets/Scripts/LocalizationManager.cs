using System;
using System.Collections.Generic;
using UnityEngine;

namespace DrawBody.Prototype
{
    public sealed class LocalizationManager : MonoBehaviour
    {
        public enum Language
        {
            Japanese,
            English
        }

        [Serializable]
        private sealed class LocalizationFile
        {
            public LocalizationEntry[] entries;
        }

        [Serializable]
        private sealed class LocalizationEntry
        {
            public string key;
            public string value;
        }

        private static readonly Dictionary<string, string> ExternalJapanese = new Dictionary<string, string>();
        private static readonly Dictionary<string, string> ExternalEnglish = new Dictionary<string, string>();
        private static bool loadedExternalTables;

        private static readonly Dictionary<string, string> Japanese = new Dictionary<string, string>
        {
            { "lang_ja", "日本語" },
            { "lang_en", "EN" },
            { "status_play", "A/D または ←/→: 移動   Space: ジャンプ   Tab: 描き直し   R: リトライ   左クリック: 腕振り" },
            { "status_draw", "体を描き直し中。Enter または 決定: この場所で再生成   C または クリア: 選択パーツを消す" },
            { "status_clear", "クリア！ Rでリトライ" },
            { "draw_title", "からだ描き直しプロトタイプ" },
            { "draw_help", "パーツを選んで、マウスドラッグで線を描く。インク上限は1000。" },
            { "preview", "プレビュー" },
            { "clear", "クリア" },
            { "decide", "決定" },
            { "ink", "インク" },
            { "remaining", "残り" },
            { "current", "選択中" },
            { "part", "パーツ" },
            { "msg_torso_first", "まず胴体を描いてください。他パーツは胴体の近くから描き始める必要があります。" },
            { "msg_torso_base", "胴体が土台です。他パーツは胴体の近くから描いてください。" },
            { "msg_start_near", "{0}: 胴体の近くから線を描き始めてください。" },
            { "msg_draw_torso_first", "接続する前に、まず胴体を描いてください。" },
            { "msg_connected", "{0} は接続されています。" },
            { "msg_not_connected", "{0} が胴体に接続されていません。" },
            { "msg_torso_needed", "決定する前に胴体の線が必要です。" },
            { "msg_part_must_start", "{0} は胴体の近くから始める必要があります。" },
            { "head", "頭" },
            { "torso", "胴体" },
            { "left_arm", "左腕" },
            { "right_arm", "右腕" },
            { "left_leg", "左足" },
            { "right_leg", "右足" },
            { "jump_normal", "通常ジャンプ" },
            { "jump_double", "ジャンプ2倍" },
            { "jump_triple", "ジャンプ3倍" },
            { "arm_normal", "通常リーチ" },
            { "arm_long", "長い腕" },
            { "arm_fast", "高速腕振り" },
            { "torso_normal", "普通" },
            { "torso_switch", "重スイッチ可" },
            { "torso_heavy", "重い体" },
            { "ability_summary", "足 {0:0.0}: {1}   腕 {2:0.0}: {3}   胴体 {4:0.0}: {5}" },
            { "label_high_platform", "1 高い足場" },
            { "label_heavy_switch", "2 重量スイッチ" },
            { "label_far_lever", "3 遠距離レバー" },
            { "label_narrow_hole", "4 狭い穴" },
            { "label_ball_hit", "5 ボール打ち" }
        };

        private static readonly Dictionary<string, string> English = new Dictionary<string, string>
        {
            { "lang_ja", "日本語" },
            { "lang_en", "EN" },
            { "status_play", "A/D or Arrows: Move   Space: Jump   Tab: Redraw   R: Retry   Left Click: Swing" },
            { "status_draw", "Redraw body. Enter or Decide: Rebuild here   C or Clear: Erase selected part" },
            { "status_clear", "Clear! Press R to retry." },
            { "draw_title", "Redraw Body Prototype" },
            { "draw_help", "Choose a part, then drag with mouse. Total ink limit is 1000." },
            { "preview", "Preview" },
            { "clear", "Clear" },
            { "decide", "Decide" },
            { "ink", "Ink" },
            { "remaining", "Remaining" },
            { "current", "Current" },
            { "part", "Part" },
            { "msg_torso_first", "Draw torso first. Other parts must start near the torso." },
            { "msg_torso_base", "Torso is the base. Draw other parts from near it." },
            { "msg_start_near", "{0}: start the line near the torso." },
            { "msg_draw_torso_first", "Draw torso first before connecting other parts." },
            { "msg_connected", "{0} is connected." },
            { "msg_not_connected", "{0} is not connected to the torso." },
            { "msg_torso_needed", "Torso needs a line before deciding." },
            { "msg_part_must_start", "{0} must start near the torso." },
            { "msg_start_at_marker", "{0}: start from the marker." },
            { "msg_part_required", "{0} needs at least one line." },
            { "head", "Head" },
            { "torso", "Torso" },
            { "left_arm", "Left Arm" },
            { "right_arm", "Right Arm" },
            { "left_leg", "Left Leg" },
            { "right_leg", "Right Leg" },
            { "left_front_leg", "Left Front" },
            { "right_front_leg", "Right Front" },
            { "left_back_leg", "Left Back" },
            { "right_back_leg", "Right Back" },
            { "tail", "Tail" },
            { "left_wing", "Left Wing" },
            { "right_wing", "Right Wing" },
            { "tail_feather", "Tail Feather" },
            { "slime_body", "Slime" },
            { "jump_normal", "Normal Jump" },
            { "jump_double", "Jump x2" },
            { "jump_triple", "Jump x3" },
            { "arm_normal", "Normal Reach" },
            { "arm_long", "Long Reach" },
            { "arm_fast", "Fast Swing" },
            { "torso_normal", "Normal" },
            { "torso_switch", "Heavy Switch" },
            { "torso_heavy", "Heavy" },
            { "ability_summary", "Leg {0:0.0}: {1}   Arm {2:0.0}: {3}   Torso {4:0.0}: {5}" },
            { "label_high_platform", "1 High Platform" },
            { "label_heavy_switch", "2 Heavy Switch" },
            { "label_far_lever", "3 Far Lever" },
            { "label_narrow_hole", "4 Narrow Hole" },
            { "label_ball_hit", "5 Ball Hit" }
        };

        public static event Action LanguageChanged;
        public static Language CurrentLanguage { get; private set; } = Language.Japanese;

        private void Awake()
        {
            LoadExternalTables();
            string saved = PlayerPrefs.GetString("language", Language.Japanese.ToString());
            if (Enum.TryParse(saved, out Language language))
            {
                SetLanguage(language);
            }
        }

        public static void SetLanguage(Language language)
        {
            LoadExternalTables();
            CurrentLanguage = language;
            PlayerPrefs.SetString("language", language.ToString());
            LanguageChanged?.Invoke();
        }

        public static string T(string key)
        {
            LoadExternalTables();
            Dictionary<string, string> external = CurrentLanguage == Language.Japanese ? ExternalJapanese : ExternalEnglish;
            if (external.TryGetValue(key, out string externalValue))
            {
                return externalValue;
            }

            if (CurrentLanguage == Language.Japanese && TryGetJapaneseOverride(key, out string japanese))
            {
                return japanese;
            }

            Dictionary<string, string> table = CurrentLanguage == Language.Japanese ? Japanese : English;
            if (table.TryGetValue(key, out string value))
            {
                return value;
            }

            return English.TryGetValue(key, out string fallback) ? fallback : key;
        }

        private static void LoadExternalTables()
        {
            if (loadedExternalTables)
            {
                return;
            }

            loadedExternalTables = true;
            LoadExternalTable("Localization/ja", ExternalJapanese);
            LoadExternalTable("Localization/en", ExternalEnglish);
        }

        private static void LoadExternalTable(string resourcePath, Dictionary<string, string> target)
        {
            TextAsset asset = Resources.Load<TextAsset>(resourcePath);
            if (asset == null)
            {
                return;
            }

            LocalizationFile file = JsonUtility.FromJson<LocalizationFile>(asset.text);
            if (file?.entries == null)
            {
                return;
            }

            for (int i = 0; i < file.entries.Length; i++)
            {
                LocalizationEntry entry = file.entries[i];
                if (entry == null || string.IsNullOrEmpty(entry.key))
                {
                    continue;
                }

                target[entry.key] = entry.value ?? string.Empty;
            }
        }

        private static bool TryGetJapaneseOverride(string key, out string value)
        {
            switch (key)
            {
                case "head":
                    value = "\u982d";
                    return true;
                case "torso":
                    value = "\u80f4\u4f53";
                    return true;
                case "left_arm":
                    value = "\u5de6\u8155";
                    return true;
                case "right_arm":
                    value = "\u53f3\u8155";
                    return true;
                case "left_leg":
                    value = "\u5de6\u8db3";
                    return true;
                case "right_leg":
                    value = "\u53f3\u8db3";
                    return true;
                case "left_front_leg":
                    value = "\u5de6\u524d\u8db3";
                    return true;
                case "right_front_leg":
                    value = "\u53f3\u524d\u8db3";
                    return true;
                case "left_back_leg":
                    value = "\u5de6\u5f8c\u8db3";
                    return true;
                case "right_back_leg":
                    value = "\u53f3\u5f8c\u8db3";
                    return true;
                case "tail":
                    value = "\u5c3b\u5c3e";
                    return true;
                case "left_wing":
                    value = "\u5de6\u7ffc";
                    return true;
                case "right_wing":
                    value = "\u53f3\u7ffc";
                    return true;
                case "tail_feather":
                    value = "\u5c3e\u7fbd";
                    return true;
                case "slime_body":
                    value = "\u30b9\u30e9\u30a4\u30e0";
                    return true;
                default:
                    value = null;
                    return false;
            }
        }

        public static string Format(string key, params object[] args)
        {
            return string.Format(T(key), args);
        }

        public static string GetPartLabel(DrawManager.BodyPart part)
        {
            return T(GetPartKey(part));
        }

        private static string GetPartKey(DrawManager.BodyPart part)
        {
            switch (part)
            {
                case DrawManager.BodyPart.Head:
                    return "head";
                case DrawManager.BodyPart.Torso:
                    return "torso";
                case DrawManager.BodyPart.LeftArm:
                    return "left_arm";
                case DrawManager.BodyPart.RightArm:
                    return "right_arm";
                case DrawManager.BodyPart.LeftLeg:
                    return "left_leg";
                case DrawManager.BodyPart.RightLeg:
                    return "right_leg";
                case DrawManager.BodyPart.LeftFrontLeg:
                    return "left_front_leg";
                case DrawManager.BodyPart.RightFrontLeg:
                    return "right_front_leg";
                case DrawManager.BodyPart.LeftBackLeg:
                    return "left_back_leg";
                case DrawManager.BodyPart.RightBackLeg:
                    return "right_back_leg";
                case DrawManager.BodyPart.Tail:
                    return "tail";
                case DrawManager.BodyPart.LeftWing:
                    return "left_wing";
                case DrawManager.BodyPart.RightWing:
                    return "right_wing";
                case DrawManager.BodyPart.TailFeather:
                    return "tail_feather";
                case DrawManager.BodyPart.SlimeBody:
                    return "slime_body";
                default:
                    return part.ToString();
            }
        }
    }
}
