using System;
using System.Collections.Generic;
using System.Globalization;
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

        [Serializable]
        public sealed class LanguageDefinition
        {
            public string code;
            public string nativeName;
            public string fallbackCode;
            public string[] resourcePaths;
            public string fontResourcePath;
            public string[] systemFontNames;
            public string cultureCode;
            public float uiTextScale = 1f;
            public string listSeparator = " / ";
            public bool rightToLeft;
        }

        [Serializable]
        private sealed class LanguageDefinitionFile
        {
            public LanguageDefinition[] entries;
        }

        private const string DefaultLanguageCode = "ja";
        private const string FallbackLanguageCode = "en";
        private static readonly List<LanguageDefinition> languageDefinitions = new List<LanguageDefinition>();
        private static readonly Dictionary<string, LanguageDefinition> languageDefinitionsByCode = new Dictionary<string, LanguageDefinition>(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, Dictionary<string, string>> externalTables = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, Font> dynamicFonts = new Dictionary<string, Font>(StringComparer.OrdinalIgnoreCase);
        private static bool loadedLanguageDefinitions;
        private static bool loadedExternalTables;

        private static readonly Dictionary<string, string> Japanese = new Dictionary<string, string>
        {
            { "option_player_name", "プレイヤー名" },
            { "option_player_name_placeholder", "名前を入力" },
            { "option_player_name_required", "プレイヤー名を入力してください" },
            { "option_player_name_guide", "ここに名前を入力" },
            { "option_register", "登録" },
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
            { "ability_bird_glide", "羽 {0:0.0} INK / 滑空速度 {1:0.00}" },
            { "ability_human_status_combined", "人間の能力\n腕 {0:0.0} INK　腕力 ×{1:0.00}\n足 {2:0.0} INK　ジャンプ ×{3:0.00}" },
            { "ability_cat_status", "猫の能力\n後ろ足 {0:0.0} INK　速さ ×{1:0.00}\n前足 {2:0.0} INK　爪レンジ ×{3:0.00}" },
            { "ability_bird_status", "鳥の能力\n羽 {0:0.0} / 700 INK\n滑空速度 {1:0.00}" },
            { "ability_turtle_status", "カメの能力\nSPACE長押し：甲羅で無敵\nF長押し：向いている側へ90°回転" },
            { "ability_slime_status", "スライムの能力\n体 {0:0.0} / 700 INK\n上トゲの長さ {1:0.0}" },
            { "ability_card_human", "人間" },
            { "ability_card_cat", "猫" },
            { "ability_card_bird", "鳥" },
            { "ability_card_turtle", "カメ" },
            { "ability_card_slime", "スライム" },
            { "ability_description_human", "足にインクを使うほど、ジャンプ力アップ\n手にインクを使うほど、投擲力アップ" },
            { "ability_description_cat", "後ろ足にインクを使うほど、走る速さアップ\n前足にインクを使うほど、ひっかき範囲アップ" },
            { "ability_description_bird", "羽にインクを使うほど、滑空力アップ" },
            { "ability_description_turtle", "甲羅に隠れている間は無敵" },
            { "ability_description_slime", "Fで真上へ攻撃トゲを出す\n少ないINKほど長い／壁ジャンプ可能" },
            { "ability_control_human", "F：投擲" },
            { "ability_control_cat", "F：ひっかき／つかまり" },
            { "ability_control_bird", "SPACE長押し：滑空　F：つかむ" },
            { "ability_control_turtle", "SPACE長押し：甲羅　F長押し：90°回転" },
            { "ability_control_slime", "F：上向きトゲ攻撃" },
            { "ability_effect_human_combined", "腕力 ×{0:0.00}　ジャンプ ×{1:0.00}" },
            { "ability_effect_cat", "走る速さ ×{0:0.00}　爪レンジ ×{1:0.00}" },
            { "ability_effect_bird", "ふんわり度 {0:0}%" },
            { "ability_effect_turtle", "SPACE 甲羅で無敵　／　F 90°回転" },
            { "ability_effect_slime", "上トゲの長さ ×{0:0.00}" },
            { "ability_ink_human_combined", "腕 {0:0.0}/280　足 {1:0.0}/160 INK" },
            { "ability_ink_cat", "後ろ足 {0:0.0}/240　前足 {1:0.0}/240 INK" },
            { "ability_ink_bird", "羽 {0:0.0} / 700 INK" },
            { "ability_ink_turtle", "ボタンを押している間だけ発動" },
            { "ability_turtle_badge", "甲羅 READY" },
            { "ability_turtle_hint", "SPACEとFは押している間だけ発動！" },
            { "ability_ink_slime", "体 {0:0.0} / 700 INK" },
            { "ability_rank", "RANK {0}" },
            { "ability_gauge_low", "よわい" },
            { "ability_gauge_high", "つよい" },
            { "ability_human_jump_gauge", "ジャンプ力" },
            { "ability_human_arm_gauge", "投擲力" },
            { "ability_cat_back_leg_gauge", "走る速さ" },
            { "ability_cat_front_leg_gauge", "ひっかき" },
            { "ability_bird_gauge", "滑空力" },
            { "ability_turtle_gauge", "防御力" },
            { "ability_slime_gauge", "機動力" },
            { "ability_slime_ink_gauge", "体のINK量" },
            { "ability_slime_gauge_low", "軽い：粘着↑" },
            { "ability_slime_gauge_high", "重い：速さ・ジャンプ↑" },
            { "ability_slime_badge", "INK量 {0:0}%" },
            { "ability_slime_hint", "F：真上へトゲ攻撃！ 少ないINKほど長い／壁ジャンプ可能" },
            { "ability_growth_hint", "描くほど能力アップ！" },
            { "redraw_unavailable_stage", "このステージでは、開始後に書き直しできません。" },
            { "label_high_platform", "1 高い足場" },
            { "label_heavy_switch", "2 重量スイッチ" },
            { "label_far_lever", "3 遠距離レバー" },
            { "label_narrow_hole", "4 狭い穴" },
            { "label_ball_hit", "5 ボール打ち" }
        };

        private static readonly Dictionary<string, string> English = new Dictionary<string, string>
        {
            { "option_player_name", "PLAYER NAME" },
            { "option_player_name_placeholder", "Enter your name" },
            { "option_player_name_required", "Please enter a player name." },
            { "option_player_name_guide", "ENTER YOUR NAME HERE" },
            { "option_register", "REGISTER" },
            { "lang_ja", "日本語" },
            { "lang_en", "EN" },
            { "status_play", "A/D or Arrows: Move   Space: Jump   Tab: Redraw   R: Retry   Left Click: Swing" },
            { "status_draw", "Redraw body. Enter or Decide: Rebuild here   C or Clear: Erase selected part" },
            { "status_clear", "Clear! Press R to retry." },
            { "draw_title", "Redraw Body Prototype" },
            { "draw_help", "Choose a part and draw. Personal max: 500. Team budget: players x 350 (checked when deciding)." },
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
            { "ink_personal_status", "Personal  {0:0.#} / {1:0}" },
            { "ink_team_status", "Team  {0:0.#} / {1:0}  [{2}]" },
            { "msg_personal_ink_over", "Cannot decide: personal ink is {0:0.#}/{1:0}. Reduce it by {2:0}." },
            { "msg_team_ink_over", "Cannot decide: team ink is {0:0.#}/{1:0}. Reduce it by {2:0}." },
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
            { "ability_bird_glide", "Wings {0:0.0} INK / Glide speed {1:0.00}" },
            { "ability_human_status_combined", "HUMAN ABILITY\nArms {0:0.0} INK  Power ×{1:0.00}\nLegs {2:0.0} INK  Jump ×{3:0.00}" },
            { "ability_cat_status", "CAT ABILITY\nBack legs {0:0.0} INK  Speed ×{1:0.00}\nFront legs {2:0.0} INK  Claw range ×{3:0.00}" },
            { "ability_bird_status", "BIRD ABILITY\nWings {0:0.0} / 700 INK\nGlide speed {1:0.00}" },
            { "ability_turtle_status", "TURTLE ABILITY\nHold SPACE: invincible shell\nHold F: turn 90° toward facing direction" },
            { "ability_slime_status", "SLIME ABILITY\nBody {0:0.0} / 700 INK\nUpward spike length {1:0.0}" },
            { "ability_card_human", "HUMAN" },
            { "ability_card_cat", "CAT" },
            { "ability_card_bird", "BIRD" },
            { "ability_card_turtle", "TURTLE" },
            { "ability_card_slime", "SLIME" },
            { "ability_description_human", "More leg INK increases jump power\nMore arm INK increases throw power" },
            { "ability_description_cat", "More back-leg INK increases run speed\nMore front-leg INK increases scratch range" },
            { "ability_description_bird", "More wing INK increases glide power" },
            { "ability_description_turtle", "Invincible while hidden in the shell" },
            { "ability_description_slime", "Press F for an upward attack spike\nLess INK makes it longer / Wall jump enabled" },
            { "ability_control_human", "F: THROW" },
            { "ability_control_cat", "F: SCRATCH / GRAB" },
            { "ability_control_bird", "HOLD SPACE: GLIDE  F: GRAB" },
            { "ability_control_turtle", "HOLD SPACE: SHELL  HOLD F: TURN 90°" },
            { "ability_control_slime", "F: UPWARD SPIKE ATTACK" },
            { "ability_effect_human_combined", "Power ×{0:0.00}  Jump ×{1:0.00}" },
            { "ability_effect_cat", "Run ×{0:0.00}  Claw range ×{1:0.00}" },
            { "ability_effect_bird", "Float power {0:0}%" },
            { "ability_effect_turtle", "SPACE: SHELL  /  F: 90° TURN" },
            { "ability_effect_slime", "Upward spike length ×{0:0.00}" },
            { "ability_ink_human_combined", "Arms {0:0.0}/280  Legs {1:0.0}/160 INK" },
            { "ability_ink_cat", "Back {0:0.0}/240  Front {1:0.0}/240 INK" },
            { "ability_ink_bird", "Wings {0:0.0} / 700 INK" },
            { "ability_ink_turtle", "Active only while the button is held" },
            { "ability_turtle_badge", "SHELL READY" },
            { "ability_turtle_hint", "SPACE and F work while held!" },
            { "ability_ink_slime", "Body {0:0.0} / 700 INK" },
            { "ability_rank", "RANK {0}" },
            { "ability_gauge_low", "LOW" },
            { "ability_gauge_high", "HIGH" },
            { "ability_human_jump_gauge", "JUMP" },
            { "ability_human_arm_gauge", "THROW" },
            { "ability_cat_back_leg_gauge", "RUN" },
            { "ability_cat_front_leg_gauge", "SCRATCH" },
            { "ability_bird_gauge", "GLIDE" },
            { "ability_turtle_gauge", "DEFENSE" },
            { "ability_slime_gauge", "MOBILITY" },
            { "ability_slime_ink_gauge", "BODY INK" },
            { "ability_slime_gauge_low", "LIGHT: GRIP UP" },
            { "ability_slime_gauge_high", "HEAVY: SPEED + JUMP UP" },
            { "ability_slime_badge", "INK SIZE {0:0}%" },
            { "ability_slime_hint", "F: Spike up! Less INK = longer / Wall jump enabled" },
            { "ability_growth_hint", "Draw more to power up!" },
            { "redraw_unavailable_stage", "You cannot redraw after this stage has started." },
            { "label_high_platform", "1 High Platform" },
            { "label_heavy_switch", "2 Heavy Switch" },
            { "label_far_lever", "3 Far Lever" },
            { "label_narrow_hole", "4 Narrow Hole" },
            { "label_ball_hit", "5 Ball Hit" }
        };

        private static readonly Dictionary<string, string> GeneratedJapanese = new Dictionary<string, string>
        {
            { "multi_room_setup", "\u30eb\u30fc\u30e0\u8a2d\u5b9a" },
            { "multi_participants", "\u53c2\u52a0\u8005" },
            { "multi_room_create_title", "\u30eb\u30fc\u30e0\u4f5c\u6210" },
            { "multi_room_join_title", "\u30eb\u30fc\u30e0\u5165\u5ba4" },
            { "multi_room_id", "\u30eb\u30fc\u30e0ID" },
            { "multi_local_test_no_invite", "\u30ed\u30fc\u30ab\u30eb\u30c6\u30b9\u30c8\u4e2d\uff08\u62db\u5f85\u4e0d\u53ef\uff09" },
            { "multi_you_badge", "YOU" },
            { "slime_missile_title", "\u0039\u002d\u0031\u0020\u30b9\u30e9\u30a4\u30e0\u30fb\u30df\u30b5\u30a4\u30eb\u30b1\u30a4\u30d6" },
            { "slime_missile_floor_warning", "\u5e8a\u304c\u6d88\u3048\u308b\uff01\u0020\u58c1\u306b\u3064\u304b\u307e\u308c" },
            { "slime_missile_hint", "\u58c1\u3092\u98db\u3073\u79fb\u308a\u3001\u8cab\u901a\u30df\u30b5\u30a4\u30eb\u3092\u304b\u308f\u305b\uff01" },
            { "tower_defense_title", "\u0038\u002d\u0033\u0020\u0020\u5473\u65b9\u3092\u5b88\u308c\uff01" },
            { "tower_defense_phase", "\u30d5\u30a7\u30fc\u30ba\u0020\u007b\u0030\u007d\u0020\u002f\u0020\u007b\u0031\u007d" },
            { "tower_defense_time_remaining", "残り時間 {0:0.0}" },
            { "tower_defense_protect", "\u5de6\u53f3\u304b\u3089\u6765\u308b\u6575\u304b\u3089\u5473\u65b9\u3092\u5b88\u308d\u3046" },
            { "tower_defense_airstrike", "\u7a7a\u7206\u307e\u3067\u0020\u007b\u0030\u007d" },
            { "tower_defense_retry", "\u5473\u65b9\u304c\u3084\u3089\u308c\u305f\u2026\u0020\u0020\u007b\u0030\u007d\u79d2\u5f8c\u306b\u30ea\u30c8\u30e9\u30a4" },
            { "tower_defense_clear", "DEFENSE CLEAR!" },
            { "value_coin_amount", "COIN {0} / {1}" },
            { "value_coin_time", "TIME {0}" },
            { "value_coin_hint", "\u7bb1\u3092\u58ca\u3057\u3066\u30b3\u30a4\u30f3\u3092\u96c6\u3081\u308d\uff01" },
            { "value_coin_time_up", "TIME UP... 5\u79d2\u5f8c\u306b\u30ea\u30c8\u30e9\u30a4" },
            { "value_coin_clear", "100 COIN CLEAR!" },
            { "human_circuit_powered", "\u901a\u96fb\uff01" },
            { "title_single", "SINGLE" },
            { "title_multi", "MULTI" },
            { "title_draw", "DRAW" },
            { "title_option", "オプション" },
            { "option_back", "戻る  ESC" },
            { "option_back_esc", "戻る  ESC" },
            { "ui_back_esc", "戻る  ESC" },
            { "title_exit", "EXIT" },
            { "title_debug", "DEBUG" },
            { "trailer_debug_title", "トレーラー撮影デバッグ" },
            { "trailer_debug_scenario_01", "① 4人協力・空中リレー" },
            { "trailer_debug_scenario_01_help", "人が鳥を投げ、鳥→猫→スライムへつなぐ演出" },
            { "trailer_debug_start", "演出を開始" },
            { "trailer_debug_back", "戻る  ESC" },
            { "trailer_debug_edit_shapes", "キャラ造形を編集" },
            { "steam_header_debug_button", "Steamヘッダー" },
            { "steam_header_capture", "920×430 PNGを書き出す" },
            { "steam_header_exit", "タイトルへ戻る" },
            { "steam_header_ready", "構図を確認してPNGを書き出してください" },
            { "steam_header_saved", "保存しました: {0}" },
            { "player_redrawing_status", "書き直し中…" },
            { "character_change_ready_room_only", "キャラ変更は待機小部屋の中でのみ行えます。" },
            { "trailer_debug_capture_help", "R  最初から     H  表示を隠す     ESC  タイトル" },
            { "trailer_tas_title", "トレーラー TAS レコーダー" },
            { "trailer_tas_help", "待機中はキャラをドラッグ配置｜人→鳥→猫→スライムの順に重ね録り｜F9: 録画  F10: 再生  P: 停止  O: 1F" },
            { "trailer_tas_record", "F9 録画/停止" },
            { "trailer_tas_play", "F10 全員再生" },
            { "trailer_tas_reset", "F8 最初へ戻す" },
            { "trailer_tas_clear", "選択キャラを消去" },
            { "trailer_tas_pause", "P 一時停止" },
            { "trailer_tas_step", "O 1フレーム" },
            { "trailer_tas_status_idle", "待機中" },
            { "trailer_tas_status_recording", "録画中" },
            { "trailer_tas_status_playback", "全トラック再生中" },
            { "trailer_tas_paused", "一時停止" },
            { "trailer_tas_status_format", "{0}｜選択: {1}｜{2}フレーム｜速度 {3:0.##}x" },
            { "trailer_tas_preset_format", "プリセット {0}" },
            { "trailer_tas_save_preset", "造形保存" },
            { "trailer_tas_placement_help", "待機中はキャラをドラッグして開始位置を変更できます" },
            { "menu_continue", "続ける" },
            { "menu_to_title", "タイトルへ" },
            { "language_settings", "言語設定" },
            { "multi_play", "マルチプレイ" },
            { "multi_random_button", "ランダムマッチ" },
            { "multi_room_button", "プライベート" },
            { "multi_random_match", "ランダムマッチ" },
            { "multi_searching_players", "プレイヤーを探しています" },
            { "multi_searching_slot", "募集中" },
            { "multi_random_status_default", "プレイヤーを探しています...\n\n●□□□\n1 / 4 PLAYERS\n\nP1  あなた    READY?\n✏  募集中\n✏  募集中\n✏  募集中" },
            { "multi_room_title", "ルーム" },
            { "multi_create_room", "ルームを作る" },
            { "multi_join_room", "ルームに入る" },
            { "multi_create_room_body", "最大人数\n\n<color=#1F63D8><b>4</b></color>\n\n公開\n\n<color=#0E7A2A><b>公開</b></color>" },
            { "multi_max_players", "最大人数" },
            { "multi_visibility", "公開設定" },
            { "multi_visibility_short", "公開" },
            { "multi_public", "公開" },
            { "multi_private", "非公開" },
            { "multi_toggle_visibility", "公開切替" },
            { "multi_prev", "<" },
            { "multi_next", ">" },
            { "multi_create", "作成" },
            { "multi_join_room_help", "ホストの画面に出ているルームコードを入力\n\nEOS設定済みなら、離れた友達とも参加できます。\nDirect TCPに戻した場合は IP:7777 を入力します。" },
            { "multi_lobby_id_placeholder", "ルームコードを入力" },
            { "multi_join", "参加" },
            { "multi_refresh", "更新" },
            { "multi_room_lobby", "ROOM LOBBY" },
            { "multi_lobby_status_default", "ID: -\nPlayers 0 / 4" },
            { "multi_copy_id", "IDコピー" },
            { "multi_start_stage_1_1", "1-1開始" },
            { "multi_stage_select", "ステージ選択" },
            { "multi_all_ready_required", "全員READYになるとステージ選択できます。" },
            { "multi_leave_confirm", "本当に退出しますか？" },
            { "multi_leave_yes", "退出する" },
            { "multi_leave_no", "キャンセル" },
            { "multi_host_selecting_stage", "ホストがステージを選択中..." },
            { "multi_leave", "退出" },
            { "multi_matching", "Matching..." },
            { "multi_host_only_start", "ホストだけが開始できます。" },
            { "multi_no_lobby_id", "コピーできるルームコードがありません。" },
            { "multi_copied_lobby_id", "Lobby IDをコピーしました。" },
            { "multi_copied_room_code", "ルームコードをコピーしました。" },
            { "multi_copied_connection_id", "接続IDをコピーしました。" },
            { "multi_no_online_lobby_id", "オンライン未接続です。ルームコードはありません。" },
            { "multi_offline_lobby_id", "オンライン未接続" },
            { "multi_room_code_label", "Code" },
            { "multi_connecting", "接続中..." },
            { "multi_room", "Room" },
            { "multi_players", "Players" },
            { "multi_local", "Local" },
            { "multi_ready", "READY" },
            { "multi_wait", "WAIT" },
            { "multi_host", "HOST" },
            { "multi_default_room_name", "みんなで落書き" },
            { "multi_friend_room_name", "友達ルーム" },
            { "online_player_you", "あなた" },
            { "player_controlled_marker", "YOU" },
            { "online_player_host", "ホスト" },
            { "online_player_number", "Player {0}" },
            { "online_fake_random_ready", "疑似ランダムマッチの準備ができました。" },
            { "online_fake_initialized", "疑似オンラインを初期化しました。" },
            { "online_content_mismatch", "ゲームデータまたはバージョンが一致しないため、オンラインプレイを開始できません。Steamでファイルの整合性を確認してください。" },
            { "online_host_disconnected", "ホストが切断したため、ルームを終了しました。" },
            { "online_logging_in", "ログイン中..." },
            { "online_local_test_player", "ローカルテストプレイヤーとしてオンラインです。" },
            { "online_fake_stage_start", "疑似ステージを開始します。" },
            { "online_private_room_created", "非公開ルームを作成しました。" },
            { "online_public_room_created", "公開ルームを作成しました。" },
            { "online_joined_fake_room", "疑似ルームに参加しました。" },
            { "online_direct_initialized", "Direct TCPを初期化しました。" },
            { "online_ready", "オンライン準備完了。ルーム作成または参加できます。" },
            { "online_room_created", "ルームを作成しました。友達にIDを共有してください。" },
            { "online_failed_to_host", "ホストに失敗しました: {0}" },
            { "online_room_id_format", "ルームIDは host-ip:port 形式で入力してください。" },
            { "online_joining_room", "ルームに参加中..." },
            { "online_failed_to_join", "参加に失敗しました: {0}" },
            { "online_left_lobby", "ロビーから退出しました。" },
            { "online_ready_changed", "READY状態を変更しました。" },
            { "online_accept_failed", "接続の受け入れに失敗しました。" },
            { "online_player_joined", "{0} が参加しました。" },
            { "online_player_left_notice", "{0} \u304c\u9000\u51fa\u3057\u307e\u3057\u305f\u3002" },
            { "online_lobby_updated", "ロビーを更新しました。" },
            { "online_starting_stage", "ステージ {0} を開始します。" },
            { "online_eos_initialized", "EOSを初期化しました。ログイン前にEOS Pluginを設定してください。" },
            { "online_eos_login", "EOSログイン中..." },
            { "online_eos_connect_not_ready", "EOS Connectが準備できていません。" },
            { "online_eos_login_failed", "EOSログインに失敗しました: {0}" },
            { "online_eos_create_lobby_failed", "EOSロビー作成に失敗しました: {0}" },
            { "online_eos_room_created", "EOSルームを作成しました。ルームコードを共有してください。" },
            { "online_eos_enter_lobby_id", "EOS Lobby IDを入力してください。" },
            { "online_eos_enter_room_code", "ルームコードを入力してください。" },
            { "online_eos_room_code_failed", "ルームコードの登録に失敗しました: {0}" },
            { "online_eos_room_code_search_failed", "ルームコード検索に失敗しました: {0}" },
            { "online_eos_room_code_not_found", "ルームコード {0} の部屋が見つかりません。" },
            { "online_eos_room_code_collision", "ルームコードが重複しました。もう一度作成してください。" },
            { "online_stage_select_opened", "ホストがステージ選択を開きました。" },
            { "online_stage_select_closed", "ホストがステージ選択を閉じました。" },
            { "online_eos_join_lobby_failed", "EOSロビー参加に失敗しました: {0}" },
            { "online_eos_joined_room", "EOSルームに参加しました。" },
            { "online_eos_left_lobby", "EOSロビーから退出しました。" },
            { "online_ready_on", "READYにしました。" },
            { "online_ready_off", "READYを解除しました。" },
            { "online_eos_device_create_failed", "EOS Device ID作成に失敗しました: {0}" },
            { "online_eos_creating_device_id", "EOS Device IDを作成中..." },
            { "online_eos_device_login_failed", "EOS Device IDログインに失敗しました: {0}" },
            { "online_eos_online_as", "EOSオンライン: {0}" },
            { "online_lobby_members_updated", "ロビー参加者を更新しました。" },
            { "online_eos_not_logged_in", "EOSにログインしていません。EOS Pluginを設定してログインしてください。" },
            { "online_eos_disabled", "EOSは無効です。" },
            { "stage_label", "Stage {0}" },
            { "stage_world_label", "WORLD {0}" },
            { "stage_species_available", "\u4f7f\u7528\u30ad\u30e3\u30e9" },
            { "stage_species_available_compact", "\u4f7f\u7528\uff1a{0}" },
            { "stage_select_debug_created", "\u4f5c\u6210\u6e08" },
            { "stage_select_debug_not_created", "\u672a\u4f5c\u6210" },
            { "draw_species_locked", "\u3053\u306e\u30b9\u30c6\u30fc\u30b8\u3067\u306f{0}\u306f\u4f7f\u3048\u307e\u305b\u3093" },
            { "draw_species_already_used", "{0}\u306f\u4ed6\u306e\u30d7\u30ec\u30a4\u30e4\u30fc\u304c\u4f7f\u7528\u4e2d\u3067\u3059\u3002\u7a7a\u3044\u3066\u3044\u308b\u30ad\u30e3\u30e9\u3092\u9078\u3093\u3067\u304f\u3060\u3055\u3044" },
            { "draw_species_swap_title", "\u30ad\u30e3\u30e9\u4ea4\u63db" },
            { "draw_species_swap_hint", "{0}\u306f\u4f7f\u7528\u4e2d\u3067\u3059\u3002\u300c\u5b8c\u6210\u300d\u3067\u4ea4\u63db\u3092\u7533\u8acb\u3067\u304d\u307e\u3059" },
            { "draw_species_swap_request", "{0}\u3055\u3093\u304c\u3001\u3042\u306a\u305f\u306e{1}\u3068{2}\u306e\u4ea4\u63db\u3092\u5e0c\u671b\u3057\u3066\u3044\u307e\u3059\u3002" },
            { "draw_species_swap_accept", "\u4ea4\u63db\u3059\u308b" },
            { "draw_species_swap_reject", "\u65ad\u308b" },
            { "draw_species_swap_pending", "\u4ea4\u63db\u306e\u8fd4\u4e8b\u3092\u5f85\u3063\u3066\u3044\u307e\u3059\u2026" },
            { "draw_species_swap_accepted", "\u30ad\u30e3\u30e9\u3092\u4ea4\u63db\u3057\u307e\u3057\u305f！" },
            { "draw_species_swap_rejected", "\u4ea4\u63db\u306f\u65ad\u3089\u308c\u307e\u3057\u305f" },
            { "draw_species_swap_unavailable", "\u4ea4\u63db\u76f8\u624b\u3092\u78ba\u8a8d\u3067\u304d\u307e\u305b\u3093\u3067\u3057\u305f" },
            { "stage_object_grain_emitter", "\u7c92\u306e\u4f9b\u7d66\u53e3" },
            { "stage_object_grain_scale", "100g\u8a08\u91cf\u53f0" },
            { "stage_object_grain_gate", "\u8a08\u91cf\u30b2\u30fc\u30c8" },
            { "stage_object_escort_friend_button", "\u5473\u65b9\u7528\u30dc\u30bf\u30f3" },
            { "stage_object_escort_player_one_way_floor", "\u30d7\u30ec\u30a4\u30e4\u30fc\u5c02\u7528\u3059\u308a\u629c\u3051\u5e8a" },
            { "species_human", "\u4eba" },
            { "species_cat", "\u732b" },
            { "species_bird", "\u9ce5" },
            { "species_turtle", "\u30ab\u30e1" },
            { "species_slime", "\u30b9\u30e9\u30a4\u30e0" },
            { "stage_editor_objects_tab", "Objects" },
            { "stage_editor_links_tab", "Links" },
            { "stage_editor_status_debug_start_help", "\u30ad\u30e3\u30e9\u3092\u30c9\u30e9\u30c3\u30b0\u3059\u308b\u3068\u3001\u305d\u306e\u5834\u6240\u304b\u3089\u30c6\u30b9\u30c8\u3092\u958b\u59cb\u3067\u304d\u307e\u3059\u3002\u901a\u5e38\u306e\u30b9\u30bf\u30fc\u30c8\u5730\u70b9\u306f\u5909\u66f4\u3055\u308c\u307e\u305b\u3093\u3002" },
            { "stage_editor_status_debug_start_drag", "\u30c6\u30b9\u30c8\u958b\u59cb\u4f4d\u7f6e\u3092\u79fb\u52d5\u4e2d\u3067\u3059\u3002" },
            { "stage_editor_status_debug_start", "\u30c6\u30b9\u30c8\u958b\u59cb\u4f4d\u7f6e: X {0:0.00} / Y {1:0.00}" },
            { "stage_editor_status_validation_failed", "\u4fdd\u5b58\u3067\u304d\u307e\u305b\u3093: \u30a8\u30e9\u30fc{0}\u4ef6\u3002{1}" },
            { "stage_editor_status_saved_with_warnings", "\u4fdd\u5b58\u3057\u307e\u3057\u305f: {0}\uff08\u8b66\u544a{1}\u4ef6\uff09" },
            { "multi_room_status_default", "ルーム作成\nルーム名  [........]\n人数  2 / 3 / 4\n公開 / 非公開\n\nルーム参加\nルームID  [......]" },
            { "stage_editor_help_runtime", "ドラッグで配置 / 選択モードの空白ドラッグ: 範囲選択 / 矢印: 選択物を移動（Alt併用で微調整）\nホイール: 拡大縮小  Shift+ホイール: 回転  Alt併用: 微調整\nX+ホイール: 横  Alt+ホイール: 縦  移動床はM+ホイール: 移動方向" },
            { "stage_editor_selected_multiple", "{0}個のオブジェクトを選択中" },
            { "stage_editor_status_range_selecting", "枠内に完全に収まるオブジェクトを選択します。" },
            { "stage_editor_status_range_selected", "{0}個のオブジェクトを範囲選択しました。選択物をドラッグするとまとめて移動できます。" },
            { "stage_editor_status_range_empty", "範囲内へ完全に収まるオブジェクトはありません。" },
            { "stage_editor_status_range_moved", "選択した{0}個のオブジェクトをまとめて移動しました。" },
            { "stage_editor_category", "カテゴリ" },
            { "stage_editor_search", "検索" },
            { "stage_editor_type", "種別" },
            { "stage_editor_search_placeholder", "検索ワード" },
            { "stage_editor_object_list", "オブジェクト一覧" },
            { "stage_editor_link_list", "リンク一覧" },
            { "stage_editor_width_minus", "横-" },
            { "stage_editor_width_plus", "横+" },
            { "stage_editor_height_minus", "縦-" },
            { "stage_editor_height_plus", "縦+" },
            { "stage_editor_redo", "進む" },
            { "stage_editor_copy", "コピー" },
            { "stage_editor_status_copied", "オブジェクトを右隣へ揃えてコピーしました。" },
            { "stage_editor_copy_right", "→ 右" },
            { "stage_editor_copy_down", "↓ 下" },
            { "stage_editor_copy_left", "← 左" },
            { "stage_editor_copy_up", "↑ 上" },
            { "stage_editor_status_copy_direction", "コピー方向: {0}" },
            { "stage_editor_status_copied_direction", "オブジェクトを{0}へ隙間なくコピーしました。" },
            { "stage_editor_status_nudge", "位置: X {0:0.00} / Y {1:0.00}（矢印キー）" },
            { "stage_editor_action_strength", "飛び出す強さ" },
            { "stage_editor_move_distance", "移動距離" },
            { "stage_editor_move_speed", "移動速度" },
            { "stage_editor_bomb_fuse_seconds", "爆発まで（秒）" },
            { "stage_editor_crumble_delay", "崩れるまで（秒）" },
            { "stage_editor_bomb_wall_hits", "爆発の必要回数" },
            { "stage_editor_conveyor_speed", "ベルト速度" },
            { "stage_editor_drop_interval", "投下間隔（秒）" },
            { "stage_editor_beam_interval", "発射間隔（秒）" },
            { "stage_editor_box_size", "箱の大きさ" },
            { "stage_editor_bomb_size", "爆弾の大きさ" },
            { "stage_editor_spike_size", "トゲの大きさ" },
            { "stage_editor_status_move_distance", "移動距離: {0:0.0}" },
            { "stage_editor_status_move_speed", "移動速度: {0:0.0}" },
            { "stage_editor_status_bomb_fuse_seconds", "爆発まで: {0:0.0}秒" },
            { "stage_editor_status_crumble_delay", "崩れるまで: {0:0.0}秒" },
            { "stage_editor_status_bomb_wall_hits", "壁の耐久: 爆発 {0:0}回" },
            { "stage_editor_status_conveyor_speed", "ベルト速度: {0:0.0}" },
            { "stage_editor_status_drop_interval", "投下間隔: {0:0.0}秒" },
            { "stage_editor_status_box_size", "落ちてくる箱の大きさ: {0:0.0}" },
            { "stage_editor_status_spike_size", "落ちてくるトゲの大きさ: {0:0.0}" },
            { "stage_editor_status_bomb_size", "製造する爆弾の大きさ: {0:0.0}" },
            { "stage_editor_conveyor_left", "向き：← 左" },
            { "stage_editor_conveyor_right", "向き：右 →" },
            { "stage_editor_status_conveyor_direction", "ベルトコンベアの向き: {0}" },
            { "stage_editor_box_pattern_all", "箱：□○△" },
            { "stage_editor_box_pattern_square", "箱：四角" },
            { "stage_editor_box_pattern_round", "箱：丸" },
            { "stage_editor_box_pattern_triangle", "箱：三角" },
            { "stage_editor_bomb_pattern_both", "爆弾：2種交互" },
            { "stage_editor_bomb_pattern_spawn", "爆弾：生成時に起動" },
            { "stage_editor_bomb_pattern_pickup", "爆弾：持つと起動" },
            { "stage_editor_status_box_pattern", "投下する箱: {0}" },
            { "stage_editor_status_bomb_pattern", "製造する爆弾: {0}" },
            { "stage_editor_status_move_direction", "移動方向: {0:0}度（M＋ホイール）" },
            { "stage_editor_status_action_strength", "飛び出す強さ: {0:0.0}" },
            { "stage_editor_weight_threshold", "1\u4eba\u3042\u305f\u308a" },
            { "stage_editor_status_weight_threshold", "\u8a08\u91cf\u5668\u306e1\u4eba\u3042\u305f\u308a\u4f5c\u52d5\u91cd\u91cf\u3092 {0:0} INK \u306b\u8a2d\u5b9a\u3057\u307e\u3057\u305f\u3002" },
            { "stage_editor_status_weight_threshold_invalid", "1\uff5e2000 \u306e\u6570\u5024\u3092\u5165\u529b\u3057\u3066\u304f\u3060\u3055\u3044\u3002" },
            { "ink_personal_cap", "\u500b\u4eba\u4e0a\u9650" },
            { "ink_team_formula", "\u5168\u4f53\u4e0a\u9650\uff08{0}\u4eba\uff09" },
            { "draw_pen_size", "\u30da\u30f3\u306e\u592a\u3055" },
            { "draw_ink", "\u30a4\u30f3\u30af" },
            { "draw_clear_part", "\u30d1\u30fc\u30c4\u6d88\u53bb" },
            { "draw_undo_once", "1\u3064\u623b\u3059" },
            { "draw_reset_all", "\u5168\u30ea\u30bb\u30c3\u30c8" },
            { "draw_reset_confirm_title", "\u5168\u30ea\u30bb\u30c3\u30c8\u3057\u307e\u3059\u304b\uff1f" },
            { "draw_reset_confirm_message", "\u3059\u3079\u3066\u306e\u30d1\u30fc\u30c4\u3092\u6d88\u3057\u3066\u3001\u6700\u521d\u306e\u666e\u901a\u306e\u30ad\u30e3\u30e9\u30af\u30bf\u30fc\u306b\u623b\u3057\u307e\u3059\u3002" },
            { "draw_reset_confirm_yes", "\u30ea\u30bb\u30c3\u30c8\u3059\u308b" },
            { "draw_reset_confirm_no", "\u30ad\u30e3\u30f3\u30bb\u30eb" },
            { "draw_preset_title", "{0}\u306e\u30d7\u30ea\u30bb\u30c3\u30c8" },
            { "draw_preset_button", "\u30d7\u30ea\u30bb\u30c3\u30c8" },
            { "draw_preset_sets_title", "5\u30ad\u30e3\u30e9\u30fb\u30d7\u30ea\u30bb\u30c3\u30c8" },
            { "draw_preset_five_species", "\ud83e\uddd1 \u4eba\u3000\ud83d\udc31 \u732b\u3000\ud83d\udc26 \u9ce5\u3000\ud83d\udc22 \u30ab\u30e1\u3000\ud83d\udfe2 \u30b9\u30e9\u30a4\u30e0" },
            { "draw_preset_all_species", "5\u30ad\u30e3\u30e9\u3059\u3079\u3066" },
            { "draw_preset_slot", "\u67a0 {0}" },
            { "draw_preset_saved", "\u767b\u9332\u6e08" },
            { "draw_preset_empty", "\u7a7a\u304d" },
            { "draw_preset_register", "\u767b\u9332" },
            { "draw_preset_apply", "\u30bb\u30c3\u30c8" },
            { "draw_preset_save_confirm_title", "\u30d7\u30ea\u30bb\u30c3\u30c8{0}\u306b\u767b\u9332\uff1f" },
            { "draw_preset_save_confirm_message", "\u73fe\u5728\u306e{0}\u306e\u7d75\u3092\u30d7\u30ea\u30bb\u30c3\u30c8{1}\u306b\u767b\u9332\u3057\u307e\u3059\u3002" },
            { "draw_preset_overwrite_confirm_message", "\u30d7\u30ea\u30bb\u30c3\u30c8{1}\u306e{0}\u306e\u7d75\u3092\u4e0a\u66f8\u304d\u3057\u307e\u3059\u3002" },
            { "draw_preset_load_confirm_title", "\u30d7\u30ea\u30bb\u30c3\u30c8{0}\u3092\u30bb\u30c3\u30c8\uff1f" },
            { "draw_preset_load_confirm_message", "\u73fe\u5728\u306e{0}\u306e\u7d75\u3092\u30d7\u30ea\u30bb\u30c3\u30c8{1}\u306e\u5185\u5bb9\u306b\u7f6e\u304d\u63db\u3048\u307e\u3059\u3002" },
            { "stage_category_decoration", "背景装飾" },
            { "stage_object_background_tree", "背景・木" },
            { "stage_object_background_grass", "背景・草" },
            { "stage_object_background_flower", "背景・花" },
            { "stage_object_background_bush", "背景・茂み" },
            { "stage_object_background_cloud", "背景・雲" },
            { "stage_object_background_push", "背景文字・PUSH" },
            { "stage_object_background_arrow", "背景・矢印" },
            { "stage_object_background_cat_face", "\u732b\u306e\u9854" },
            { "stage_object_background_dog_face", "\u72ac\u306e\u9854" },
            { "stage_object_background_stick_figure", "\u68d2\u4eba\u9593" },
            { "stage_object_background_smiley", "\u30b9\u30de\u30a4\u30eb" },
            { "stage_object_background_heart", "\u30cf\u30fc\u30c8" },
            { "stage_object_background_star", "\u661f" },
            { "stage_object_background_moon", "\u6708" },
            { "stage_object_background_sun", "\u592a\u967d" },
            { "stage_object_background_rain", "\u96e8" },
            { "stage_object_background_lightning", "\u96f7" },
            { "stage_object_background_rainbow", "\u8679" },
            { "stage_object_background_mountain", "\u5c71" },
            { "stage_object_background_four_leaf_clover", "\u56db\u3064\u8449\u306e\u30af\u30ed\u30fc\u30d0\u30fc" },
            { "stage_object_background_mushroom", "\u30ad\u30ce\u30b3" },
            { "stage_object_background_apple", "\u30ea\u30f3\u30b4" },
            { "stage_object_background_banana", "\u30d0\u30ca\u30ca" },
            { "stage_object_background_watermelon", "\u30b9\u30a4\u30ab" },
            { "stage_object_background_donut", "\u30c9\u30fc\u30ca\u30c4" },
            { "stage_object_background_ice_cream", "\u30a2\u30a4\u30b9\u30af\u30ea\u30fc\u30e0" },
            { "stage_object_background_coffee_cup", "\u30b3\u30fc\u30d2\u30fc\u30ab\u30c3\u30d7" },
            { "stage_object_background_pizza", "\u30d4\u30b6" },
            { "stage_object_background_bread", "\u30d1\u30f3" },
            { "stage_object_background_paper_airplane", "\u7d19\u98db\u884c\u6a5f" },
            { "stage_object_background_airplane", "\u98db\u884c\u6a5f" },
            { "stage_object_background_rocket", "\u30ed\u30b1\u30c3\u30c8" },
            { "stage_object_background_ufo", "UFO" },
            { "stage_object_background_hot_air_balloon", "\u6c17\u7403" },
            { "stage_object_background_house", "\u5bb6" },
            { "stage_object_background_castle", "\u304a\u57ce" },
            { "stage_object_background_treasure_chest", "\u5b9d\u7bb1" },
            { "stage_object_background_mole", "モグラ" },
            { "stage_object_background_fossil", "化石" },
            { "stage_object_background_crystal", "水晶" },
            { "stage_object_background_ancient_pot", "古い壺" },
            { "stage_object_background_key", "\u9375" },
            { "stage_object_background_key_needed", "\u9375\u3092\u9375\u7a74\u3078" },
            { "stage_object_background_sword", "\u5263" },
            { "stage_object_background_crown", "\u738b\u51a0" },
            { "stage_object_background_shield", "\u76fe" },
            { "stage_object_background_gem", "\u5b9d\u77f3" },
            { "stage_object_background_coin", "\u30b3\u30a4\u30f3" },
            { "stage_object_background_bone", "\u9aa8" },
            { "stage_object_background_light_bulb", "\u96fb\u7403" },
            { "stage_object_background_gear", "\u6b6f\u8eca" },
            { "stage_object_background_spring", "\u30d0\u30cd" },
            { "stage_object_background_magnet", "\u78c1\u77f3" },
            { "stage_object_background_dice", "\u30b5\u30a4\u30b3\u30ed" },
            { "stage_object_background_speech_bubble", "\u5439\u304d\u51fa\u3057" },
            { "stage_object_background_check_mark", "\u30c1\u30a7\u30c3\u30af\u30de\u30fc\u30af" },
            { "stage_object_background_question_mark", "\u306f\u3066\u306a" },
            { "stage_object_background_exclamation_mark", "\u3073\u3063\u304f\u308a" },
            { "stage_object_background_loop_arrow", "\u304f\u308b\u3063\u3068\u77e2\u5370" },
            { "stage_object_background_jump", "JUMP!" },
            { "stage_object_background_throw", "Throw!" },
            { "stage_object_background_start", "START!" },
            { "stage_object_background_goal", "GOAL!" },
            { "stage_editor_status_rotate_decoration", "\u56de\u8ee2\uff1aShift\uff0b\u30db\u30a4\u30fc\u30eb\uff080.5\u5ea6\u5358\u4f4d\uff09" },
            { "stage_editor_status_rotate_mounted", "回転：Shift＋ホイール（15度）／Alt併用（1度）" },
            { "stage_editor_status_rotate_object", "回転：Shift＋ホイール（15度）／Alt併用（1度）" },
            { "stage_editor_status_scale_decoration", "\u62e1\u5927\u30fb\u7e2e\u5c0f\uff1a\u30db\u30a4\u30fc\u30eb" },
            { "stage_editor_link_action", "\u52d5\u4f5c" },
            { "stage_editor_link_mode_reveal", "\u51fa\u73fe" },
            { "stage_editor_link_mode_hide", "\u6d88\u6ec5" },
            { "stage_editor_link_mode_unlock", "\u89e3\u9320" },
            { "stage_editor_link_mode_move", "移動" },
            { "stage_editor_link_mode_move_right", "床を右へ" },
            { "stage_editor_link_mode_move_up", "床を上へ" },
            { "stage_editor_link_mode_move_left", "床を左へ" },
            { "stage_editor_link_mode_move_down", "床を下へ" },
            { "stage_editor_status_link_action_changed", "\u30ea\u30f3\u30af\u52d5\u4f5c\uff1a{0}" },
            { "stage_editor_status_key_requires_keyhole", "\u9375\u306e\u30ea\u30f3\u30af\u5148\u306b\u306f\u9375\u7a74\u3092\u9078\u3093\u3067\u304f\u3060\u3055\u3044\u3002" },
            { "stage_editor_status_unlock_action_fixed", "\u9375\u2192\u9375\u7a74\u306e\u52d5\u4f5c\u306f\u300c\u89e3\u9320\u300d\u3067\u56fa\u5b9a\u3067\u3059\u3002" },
            { "stage_editor_snap_attach", "吸着" },
            { "stage_editor_link_source", "リンク元" },
            { "stage_editor_link_target", "リンク先" },
            { "stage_editor_unlink", "解除" },
            { "stage_editor_selected_add", "追加: {0} / 吸着: {1}" },
            { "stage_editor_selected_object", "{0}  位置 {1:0.0},{2:0.0}  サイズ {3:0.0},{4:0.0}" },
            { "stage_editor_boundary_resize_hint", "横サイズ＝右壁 / 縦サイズ＝天井（左壁・下端は固定）" },
            { "stage_editor_boundary_quick", "枠" },
            { "stage_editor_status_boundary_fitted", "配置物に合わせて外枠を作成しました。右壁と天井は選択後に調整できます。" },
            { "stage_editor_status_wall_fitted", "壁を上下の面に合わせました。" },
            { "stage_goal_label", "ゴール" },
            { "stage_room_number", "部屋 {0}" },
            { "stage_weapon_bomb", "爆弾" },
            { "stage_weapon_missile", "ミサイル" },
            { "stage_ready_label", "準備OK" },
            { "stage_dynamite_label", "TNT" },
            { "stage_editor_background_color", "背景色" },
            { "laser_relay_editor_players", "人数 {0}" },
            { "laser_relay_editor_round", "ステージ {0}" },
            { "laser_relay_editor_preview", "14-3 編集表示：{0}人 / ステージ{1}" },
            { "stage_editor_color_title", "背景の色" },
            { "stage_editor_color_reset", "元の色" },
            { "stage_editor_color_close", "閉じる" },
            { "stage_editor_color_opacity", "不透明度" },
            { "stage_editor_status_background_color", "背景色を変更しました。保存するとステージに反映されます。" },
            { "stage_editor_status_add_rect", "追加モード: {0}。マップ上でドラッグして作成します。" },
            { "stage_editor_status_add_point", "追加モード: {0}。マップ上をクリックして配置します。" },
            { "stage_editor_status_selected_from_list", "一覧から選択しました。" },
            { "stage_editor_no_match", "一致なし" },
            { "stage_editor_none", "(なし)" },
            { "stage_category_terrain", "地形" },
            { "stage_category_start_goal", "スタート・ゴール" },
            { "stage_category_switch", "スイッチ" },
            { "stage_category_door_gate", "ドア・ゲート" },
            { "stage_category_movable", "可動" },
            { "stage_category_action", "アクション" },
            { "stage_category_trap", "トラップ" },
            { "stage_category_gimmick", "ギミック" },
            { "stage_category_enemy", "敵キャラ" },
            { "stage_object_platform", "床" },
            { "stage_object_wall", "壁" },
            { "stage_object_spawn", "開始" },
            { "stage_object_goal", "ゴール" },
            { "stage_object_balance_scale", "天秤" },
            { "stage_object_weight", "重り" },
            { "stage_object_ceiling", "天井" },
            { "stage_object_half_platform", "半床" },
            { "stage_object_one_way_platform", "一方通行床" },
            { "stage_object_ice_floor", "氷床" },
            { "stage_object_slippery_slope", "滑る坂" },
            { "stage_object_climbable_wall", "登れる壁" },
            { "stage_object_rope", "ロープ" },
            { "stage_object_ladder", "はしご" },
            { "stage_object_cloud_platform", "雲床" },
            { "stage_object_breakable_floor", "壊れる床" },
            { "stage_object_falling_floor", "崩れる床" },
            { "stage_object_moving_platform", "動く床" },
            { "stage_object_moving_one_way_platform", "粘着移動一方通行床" },
            { "ricochet_breaker_title", "8-2  リフレクト・ドロー" },
            { "ricochet_breaker_goal", "体で球を跳ね返して NICO DRAW を壊そう！" },
            { "ricochet_breaker_blocks", "ブロック {0}　球 ×{1}" },
            { "ricochet_breaker_retry", "TIME UP  リトライまで {0}" },
            { "ricochet_breaker_start_in", "開始まで {0}" },
            { "ricochet_enemy_round", "ROUND {0} / {1}" },
            { "ricochet_enemy_balls", "BALL {0} / {1}" },
            { "ricochet_enemy_next", "次のラウンドまで {0}" },
            { "ricochet_enemy_failed", "球を3回ロスト！ リトライ" },
            { "ricochet_enemy_clear", "全ラウンド クリア！" },
            { "ricochet_enemy_remaining", "残りの敵 {0}" },
            { "stage_object_handgun", "銃" },
            { "stage_object_bullet_breakable_wall", "銃弾で壊れる壁" },
            { "stage_object_spike_planet", "トゲ惑星" },
            { "stage_object_enemy_flyer_zigzag", "ジグザグ飛行敵" },
            { "stage_object_enemy_flyer_orbit", "旋回飛行敵" },
            { "stage_object_bazooka", "バズーカ" },
            { "stage_object_enemy_bomber", "爆弾投下飛行敵" },
            { "stage_object_moving_spike_planet", "動くトゲ球" },
            { "stage_object_pose_character_key", "ポーズ鍵" },
            { "stage_object_pose_character_keyhole", "ポーズ鍵穴" },
            { "stage_object_updraft_zone", "上昇気流" },
            { "stage_object_speed_ring2_x", "加速マーク" },
            { "stage_object_speed_ring3_x", "加速マーク（強）" },
            { "stage_object_redraw_zone", "書き直しゾーン" },
            { "stage_editor_bullet_wall_hits", "必要な銃弾数" },
            { "stage_editor_status_bullet_wall_hits", "必要な銃弾数: {0}" },
            { "stage_object_rotating_platform", "回転床" },
            { "stage_object_checkpoint", "チェックポイント" },
            { "stage_object_warp_entrance", "ワープ入口" },
            { "stage_object_warp_exit", "ワープ出口" },
            { "stage_object_respawn_point", "リスポーン地点" },
            { "stage_object_mid_goal", "中間ゴール" },
            { "stage_object_goal_effect", "ゴール演出" },
            { "stage_object_button", "ボタン" },
            { "stage_object_weight_button", "重さボタン" },
            { "stage_object_simultaneous_button", "同時押しボタン" },
            { "stage_object_hold_button", "押している間ボタン" },
            { "stage_object_triangle_box", "三角の箱" },
            { "stage_object_lever", "レバー" },
            { "stage_object_toggle_switch", "トグルスイッチ" },
            { "stage_object_timer_switch", "タイマースイッチ" },
            { "stage_object_sensor", "センサー" },
            { "stage_object_red_switch", "赤スイッチ" },
            { "stage_object_blue_switch", "青スイッチ" },
            { "stage_object_green_switch", "緑スイッチ" },
            { "stage_object_yellow_switch", "黄スイッチ" },
            { "stage_object_pressure_plate", "圧力板" },
            { "stage_object_remote_control", "リモコン" },
            { "stage_object_ink_scale", "\u8a08\u91cf\u5668" },
            { "stage_object_door", "ドア" },
            { "stage_object_locked_door", "鍵付きドア" },
            { "stage_object_shutter", "シャッター" },
            { "stage_object_fence", "柵" },
            { "stage_object_laser_gate", "レーザーゲート" },
            { "stage_object_color_gate", "色ゲート" },
            { "stage_object_one_way_gate", "一方通行ゲート" },
            { "stage_object_timed_gate", "時間制限ゲート" },
            { "stage_object_breakable_wall", "爆弾で壊れる壁" },
            { "stage_object_hidden_wall", "隠し壁" },
            { "stage_object_wood_box", "木箱" },
            { "stage_object_iron_box", "鉄箱" },
            { "stage_object_ball", "ボール" },
            { "stage_object_barrel", "樽" },
            { "stage_object_rock", "岩" },
            { "stage_object_ice_block", "氷ブロック" },
            { "stage_object_floating_box", "浮遊箱" },
            { "stage_object_rubber_box", "ゴム箱" },
            { "stage_object_bomb", "爆弾（生成時に起動）" },
            { "stage_object_pickup_fuse_bomb", "爆弾（持つと起動）" },
            { "stage_object_bomb_dropper", "爆弾製造機" },
            { "stage_object_dynamite", "ダイナマイト" },
            { "stage_object_enemy_walker", "歩く敵" },
            { "stage_object_enemy_jumper", "ジャンプする敵" },
            { "stage_object_enemy_charger", "突進する敵" },
            { "stage_object_enemy_flyer", "空を飛ぶ敵" },
            { "stage_object_enemy_shooter", "弾を撃つ敵" },
            { "stage_object_enemy_dropper", "敵製造機" },
            { "stage_object_missile_launcher", "ミサイル発射装置" },
            { "stage_editor_link_mode_activate", "作動" },
            { "stage_editor_status_activate_action_fixed", "この装置のリンク動作は「作動」固定です" },
            { "stage_editor_launch_interval", "発射間隔" },
            { "stage_editor_enemy_size", "敵の大きさ" },
            { "stage_editor_status_enemy_pattern", "製造する敵: {0}" },
            { "stage_editor_status_enemy_size", "敵の大きさ: {0:0.0}" },
            { "stage_object_key", "鍵" },
            { "stage_object_coin", "コイン" },
            { "stage_object_star", "星" },
            { "stage_object_battery", "バッテリー" },
            { "stage_object_bucket", "バケツ" },
            { "stage_object_jump_pad", "ジャンプ台" },
            { "stage_object_spring", "バネ" },
            { "stage_object_conveyor_left", "コンベア左" },
            { "stage_object_conveyor_right", "コンベア右" },
            { "stage_object_elevator", "エレベーター" },
            { "stage_object_fan", "扇風機" },
            { "stage_object_magnet", "磁石" },
            { "stage_object_belt", "ベルトコンベア" },
            { "stage_object_box_dropper", "箱を落とす装置" },
            { "stage_object_spike_dropper", "トゲ落下装置" },
            { "stage_object_collectible_fish", "魚" },
            { "stage_object_collectible_coin", "コイン" },
            { "stage_object_collectible_star", "星" },
            { "stage_object_challenge_clock", "デジタル時計" },
            { "stage_object_beam_emitter", "ビーム発射装置" },
            { "stage_rule_normal", "通常" },
            { "stage_rule_timed", "時間制限" },
            { "stage_rule_survival", "サバイバル" },
            { "stage_rule_seconds", "{0:0}秒" },
            { "stage_rule_count", "×{0}" },
            { "stage_rule_all", "全部" },
            { "stage_editor_status_rule", "ステージルール: {0}" },
            { "challenge_time_remaining", "残り {0:0.0} 秒" },
            { "challenge_collection_progress", "{0}  {1} / {2}" },
            { "challenge_time_up", "TIME UP" },
            { "challenge_retry_hint", "Rキーでやり直し" },
            { "survival_mode_title", "11-2  サバイバル" },
            { "survival_goal", "1人でも生き残ればクリア！" },
            { "survival_goal_sub", "光った床へ移動しよう" },
            { "grain_rain_target", "目標  {0:0}g  (90g × 人数)" },
            { "grain_rain_ready", "自由に動いて受け止める位置を決めよう" },
            { "grain_rain_catch", "粒を拾え！" },
            { "grain_rain_measuring", "計測中..." },
            { "grain_rain_floor_clear", "床の粒を除外しています" },
            { "grain_rain_clear", "CLEAR!" },
            { "grain_rain_failed", "あと少し！" },
            { "grain_rain_result", "頭に残った粒  {0:0}g / {1:0}g" },
            { "grain_rain_success", "レア粒の計測成功！" },
            { "grain_rain_retry", "もう一度、頭の形も工夫しよう" },
            { "grain_rain_round", "ステージ {0}/{1}" },
            { "grain_rain_blizzard", "右から流れる粒の吹雪を受け止めろ！" },
            { "grain_rain_forecast", "人数分の予告地点へ高速の粒が落ちてくる！" },
            { "ice_speedrun_title", "10-1  氷のスピードラン　誰か1人がゴールでクリア" },
            { "ice_speedrun_start", "スタート！" },
            { "ice_speedrun_time_up", "TIME UP  リトライまで {0}" },
            { "survival_get_ready", "準備して！" },
            { "survival_start", "スタート！" },
            { "game_over", "GAME OVER" },
            { "msg_body_too_large_for_spawn", "この体ではスタート地点に入りません。小さく書き直してください。" },
            { "msg_body_too_large_for_ready_room", "体が部屋からはみ出す可能性があります。サイズを見直してください。" },
            { "ready_room_status", "準備 {0} / {1}" },
            { "ready_room_recommended", "推奨キャラ" },
            { "ready_room_recommended_none", "なし" },
            { "ready_room_restriction", "※部屋を出ると キャラ変更・書き直し不可" },
            { "ready_room_restriction_redraw_allowed", "※部屋を出ると キャラ変更不可（書き直し可）" },
            { "ready_room_game_default", "みんなで準備してゲームを始めよう" },
            { "ready_room_game_2_2", "制限時間内に魚を集めよう" },
            { "ready_room_game_4_3", "クレヨンキングを倒そう" },
            { "ready_room_game_6_2", "ハードルをよけて生き残ろう" },
            { "ready_room_game_6_3", "水槽の穴を体で全部ふさごう" },
            { "ready_room_game_7_1", "体の形を使って鍵を開けよう" },
            { "ready_room_game_8_1", "落ちてくる柱をよけよう" },
            { "ready_room_game_8_2", "ボールを跳ね返してブロックを壊そう" },
            { "ready_room_game_8_3", "INK磁石でボールを引き寄せ、穴へ入れよう" },
            { "ready_room_game_9_1", "ミサイルをよけて生き残ろう" },
            { "ready_room_game_9_2", "落ちながらコインを集めよう" },
            { "ready_room_game_9_3", "落ちてくる粒を集めて、目標重量を目指そう" },
            { "ready_room_game_10_1", "スピードラン。制限時間内にゴールしよう。" },
            { "ready_room_game_10_3", "銃弾を反射させて敵を倒そう" },
            { "ready_room_game_11_1", "暗闇を照らして、幽霊から逃げ切ろう" },
            { "ready_room_game_11_2", "最後まで生き残ろう" },
            { "ready_room_game_11_3", "爆弾を投げてブロックを全部壊そう" },
            { "ready_room_game_12_1", "箱を壊して、コインを集めよう" },
            { "ready_room_game_12_2", "制限時間内にコインを集めよう" },
            { "ready_room_game_12_3", "制限時間内にコインを集めよう" },
            { "ready_room_game_13_1", "敵から味方を守ろう" },
            { "ready_room_game_13_2", "ボールを跳ね返して敵を倒そう" },
            { "ready_room_game_14_1", "制限時間内にゴールしよう" },
            { "ready_room_game_14_2", "傘で雨にあたらないようにしよう" },
            { "ready_room_game_14_3", "身体でレーザーを順番に反射してゴールへつなごう" },
            { "ready_room_game_15_1", "クレヨンデビルを倒そう" },
            { "ready_room_game_15_2", "クレヨンストーカーを倒そう" },
            { "ready_room_game_15_3", "敵を倒そう" },
            { "ready_room_clear_one_survivor", "一人でも生き残ればクリア。" },
            { "ready_room_clear_one_goal", "一人でもゴールすればクリア。" },
            { "spike_chase_monitor_title", "トゲ壁 発進まで" },
            { "spike_chase_monitor_ready", "合図まで待て！" },
            { "spike_chase_monitor_goal", "右のゴールへ走れ！" },
            { "wind_speedrun_title", "風向きが交互に変わる！" },
            { "wind_speedrun_hint", "INKが少ないほど 強く流される" },
            { "wind_speedrun_timer", "TIME {0:0.0}" },
            { "wind_speedrun_time_up", "TIME UP" },
            { "umbrella_rain_title", "傘の下から出るな！" },
            { "umbrella_rain_hint", "雨に触れると脱落　誰か1人が雨宿りへ" },
            { "umbrella_rain_all_out", "全員が雨に打たれた！" },
            { "linked_shield_title", "14-3  リンクシールド" },
            { "linked_shield_ready", "ボタンのリンク先を確認！" },
            { "linked_shield_hint", "味方へ飛ぶミサイルを1秒の壁で防げ！" },
            { "linked_shield_failed", "全員被弾…  リスタート" },
            { "linked_shield_clear", "60秒防衛成功！" },
            { "laser_relay_monitor", "ラウンド {0}/3    のこり {1:0.0}秒" },
            { "laser_relay_progress", "ゴール {0}/{1}" },
            { "laser_relay_hint", "身体の輪郭の角度が、そのままレーザーの反射角になる" },
            { "laser_relay_round_clear", "ラウンド {0} クリア！" },
            { "laser_relay_timeout", "時間切れ！  このラウンドをやり直します" },
            { "flying_boss_hp", "クレヨンデビル  HP  {0} / {1}" },
            { "flying_boss_ready", "3・2・1  発進準備" },
            { "flying_boss_controls", "移動キー：床移動　マウス：照準　クリック：ミサイル　F：爆弾" },
            { "flying_boss_clear", "BOSS撃破！" },
            { "flying_boss_failed", "全機撃墜…" },
            { "flying_boss_homing_warning", "全員に追尾ミサイル！ 散開！" },
            { "flying_boss_target_warning", "TARGET！ 仲間から離れろ！" },
            { "flying_boss_suction_warning", "吸い込み！ 逆方向へ逃げろ！" },
            { "mirror_boss_recording", "行動を記録中…  あと {0} 秒" },
            { "mirror_boss_recording_hint", "自由に走って、跳んで、止まってみよう" },
            { "mirror_boss_phase", "PHASE {0}　偽物 残り {1}体" },
            { "mirror_boss_hint", "発射床へ誘導 → 対応する赤スイッチを踏め！" },
            { "mirror_boss_clear", "偽物をすべて撃破！" },
            { "mirror_boss_clear_sub", "本物は、最後までここにいる。" },
            { "mirror_brawl_phase_ready", "PHASE {0}　開始まで" },
            { "mirror_brawl_ready_hint", "同じ姿のCPUチームを倒せ" },
            { "mirror_brawl_phase", "PHASE {0}　残り {1}体" },
            { "mirror_brawl_phase_clear", "PHASE {0} クリア！" },
            { "mirror_brawl_hint", "赤ボタンで武器を確保　爆弾：Fで投げる　ミサイル：クリック" },
            { "mirror_brawl_missile_ready", "ミサイル準備完了！　マウスで狙ってクリック" },
            { "mirror_brawl_clear", "CPUチーム撃破！" },
            { "mirror_brawl_clear_sub", "ラスボス戦クリア！" },
            { "mirror_brawl_failed", "TIME UP" },
            { "mirror_brawl_time_up", "TIME UP！" },
            { "mirror_brawl_all_down", "全員やられた！" },
            { "mirror_brawl_retry", "失敗！　3秒後に最初からやり直します" },
            { "side_boss_hp", "クレヨンストーカー  HP {0} / {1}" },
            { "side_boss_ready", "3・2・1  逃走準備" },
            { "side_boss_run", "右へ逃げろ！ 緑の装置で反撃！" },
            { "side_boss_clear", "追跡ボス撃破！" },
            { "side_boss_failed", "全員つかまった…" },
            { "spike_chase_run", "走れ！" },
            { "survival_watch_floor", "光る床をよく見て！" },
            { "survival_remaining", "生き残れ！  残り時間" },
            { "survival_safe_countdown", "安全床 {0}か所 ／ 消えるまで {1:0.0}" },
            { "survival_floor_dropped", "光った床から落ちるな！" },
            { "survival_all_dead", "全員脱落…" },
            { "survival_retrying", "まもなくリスタート" },
            { "survival_clear_title", "生存者あり！" },
            { "survival_clear_sub", "サバイバル成功" },
            { "pillar_survival_title", "8-1  柱サバイバル" },
            { "pillar_survival_goal_sub", "予告を見て、落ちる柱をよけよう" },
            { "pillar_survival_watch_up", "上から来る！  光った場所をよけろ" },
            { "pillar_survival_clear", "柱を耐え切った！" },
            { "boss_name", "4-3  クレヨンキング" },
            { "boss_health", "HP  {0} / {1}" },
            { "boss_enter_room", "奥の部屋へ進め" },
            { "boss_appears", "WARNING...  BOSS!" },
            { "boss_fight", "銃で攻撃  /  亀の甲羅で防御" },
            { "boss_invulnerable", "GUARD!  今は無敵" },
            { "boss_charge", "CHARGE!  突進に注意" },
            { "boss_charge_countdown", "突進まで {0} ！  上の足場へ逃げろ" },
            { "boss_special_warning", "SPECIAL ATTACK!  反射弾から逃げろ" },
            { "boss_defeated", "BOSS DEFEATED!" },
            { "boss_all_out", "全員脱落...  ボス戦をやり直します" },
            { "boss_waiting_count", "待合室  {0} / {1}  全員集まれ！" },
            { "jump_rope_mode_title", "6-2  ハードルサバイバル" },
            { "jump_rope_goal", "1人でも生き残ればクリア！" },
            { "jump_rope_goal_sub", "60秒間、右から来るハードルを飛び越えよう" },
            { "jump_rope_ready", "右側から来るハードルに注意！" },
            { "jump_rope_jump_now", "ジャンプ！" },
            { "jump_rope_keep_jumping", "ハードルを飛び越えろ！" },
            { "jump_rope_slow", "SLOW...  いったん減速！" },
            { "jump_rope_accelerate", "加速！ ここからラストスパート！" },
            { "jump_rope_all_out", "全員脱落…" },
            { "jump_rope_clear_title", "ハードル成功！" },
            { "jump_rope_clear_sub", "生き残りクリア" },
            { "escort_title", "5-3  味方をゴールへ運ぼう" },
            { "escort_friend_active", "味方キャラ  進行中" },
            { "escort_instruction", "箱・足場・自分の体で道をつなごう" },
            { "escort_defense_title", "10-2  \u5473\u65b9\u3092\u5b88\u3063\u3066\u30b4\u30fc\u30eb\u3078" },
            { "escort_friend_defeat_cry", "AAAH!" },
            { "escort_defense_instruction", "\u5148\u56de\u308a\u3057\u3066\u7f60\u30fb\u7bb1\u30fb\u6575\u3092\u6392\u9664\u3057\u3088\u3046" },
            { "escort_respawning", "再排出まで  {0:0.0}" },
            { "escort_respawn_sub", "落ちた味方を準備中…" },
            { "escort_clear_title", "護送成功！" },
            { "escort_clear_sub", "味方キャラがゴールしました" },
            { "drawn_escort_monitor", "ラウンド {0}/3    準備 {1:0.0}秒" },
            { "drawn_escort_plan", "体の線で道をつなごう" },
            { "drawn_escort_running", "発進！  味方をゴールまで運ぼう" },
            { "drawn_escort_round_running", "ラウンド {0}/3" },
            { "drawn_escort_round_clear", "ラウンド {0} クリア！" },
            { "aquarium_seal_monitor", "ラウンド {0}/3    のこり {1:0.0}秒" },
            { "aquarium_seal_progress", "穴 {0}/{1}    体で全部ふさごう！" },
            { "aquarium_seal_round_clear", "ラウンド {0} クリア！" },
            { "aquarium_seal_timeout", "時間切れ！  このラウンドをやり直します" },
            { "aquarium_seal_box_hint", "穴を塞げ！" },
            { "tilt_board_monitor", "ラウンド {0}/3    のこり {1:0.0}秒" },
            { "tilt_board_progress", "ボール {0}/{1}    穴に入れよう！" },
            { "tilt_board_maze", "磁力でボールを引き寄せ、迷路の先へ運ぼう！" },
            { "tilt_board_round_clear", "ラウンド {0} クリア！" },
            { "tilt_board_timeout", "時間切れ！  このラウンドをやり直します" },
            { "tilt_board_hint", "担当レーンを動いて引き寄せろ！  INKが多いほど磁力アップ" },
            { "drawn_escort_snack", "最終ラウンド：上のおやつを取ろう！" },
            { "drawn_escort_final_plan", "上のおやつを通る道を作ろう！\n体の線で道をつなごう" },
            { "drawn_escort_tutorial", "頭や体の線も、味方が歩ける足場になる" },
            { "drawn_escort_launch", "味方 発進" },
            { "drawn_escort_failed", "味方が落ちてしまった！" },
            { "drawn_escort_game_over", "ゲームオーバー" },
            { "redraw_unavailable_escort_run", "味方の走行中は書き直しできません。" },
            { "ricochet_title", "10-3  反射リレー" },
            { "ricochet_round", "ROUND {0} / {1}" },
            { "ricochet_ammo", "残弾 {0}" },
            { "ricochet_instruction", "仲間の体で弾を反射して敵へ当てよう" },
            { "ricochet_failed", "弾切れ！ 最初からやり直し" },
            { "ricochet_clear", "5 ROUND CLEAR!" },
            { "stage_object_escort_spawner", "味方キャラ排出機" },
            { "stage_object_escort_goal", "味方キャラゴール" },
            { "stage_object_escort_player_only_floor", "味方が通り抜ける床" },
            { "stage_object_escort_head_bridge", "頭の足場見本" },
            { "stage_object_seesaw", "シーソー" },
            { "stage_object_turntable", "回転台" },
            { "stage_object_cannon", "大砲" },
            { "stage_object_catapult", "投石機" },
            { "stage_object_spike", "トゲ" },
            { "stage_object_fire", "火" },
            { "stage_object_water", "水" },
            { "stage_object_poison", "毒" },
            { "stage_object_laser", "レーザー" },
            { "stage_object_falling_rock", "落石" },
            { "stage_object_press_machine", "プレス機" },
            { "stage_object_electricity", "電気" },
            { "stage_object_saw", "ノコギリ" },
            { "stage_object_black_hole", "ブラックホール" },
            { "stage_object_gear", "歯車" },
            { "stage_object_big_gear", "歯車(大)" },
            { "stage_object_rope_pulley", "ロープ滑車" },
            { "stage_object_slider", "スライダー" },
            { "stage_object_rotating_bar", "回転棒" },
            { "stage_object_pendulum", "振り子" },
            { "stage_object_keyhole", "カギ穴" },
            { "stage_object_stage_boundary", "ステージ外枠" },
            { "stage_object_clock", "時計" },
            { "stage_object_counter", "カウンター" },
            { "stage_object_traffic_light", "信号機" }
        };

        private static readonly Dictionary<string, string> GeneratedEnglish = new Dictionary<string, string>
        {
            { "multi_room_setup", "ROOM SETUP" },
            { "multi_participants", "PLAYERS" },
            { "multi_room_create_title", "CREATE ROOM" },
            { "multi_room_join_title", "JOIN ROOM" },
            { "multi_room_id", "ROOM ID" },
            { "multi_local_test_no_invite", "LOCAL TEST (INVITES OFF)" },
            { "multi_you_badge", "YOU" },
            { "slime_missile_title", "9-1  SLIME MISSILE CAVE" },
            { "slime_missile_floor_warning", "FLOOR VANISHING! GRAB A WALL" },
            { "slime_missile_hint", "WALL-JUMP AND DODGE PIERCING MISSILES!" },
            { "tower_defense_title", "8-3  PROTECT YOUR FRIEND!" },
            { "tower_defense_phase", "PHASE {0} / {1}" },
            { "tower_defense_time_remaining", "TIME {0:0.0}" },
            { "tower_defense_protect", "Defend your friend from both sides" },
            { "tower_defense_airstrike", "AIRSTRIKE IN {0}" },
            { "tower_defense_retry", "FRIEND DOWN... RETRY IN {0}" },
            { "tower_defense_clear", "DEFENSE CLEAR!" },
            { "value_coin_amount", "COIN {0} / {1}" },
            { "value_coin_time", "TIME {0}" },
            { "value_coin_hint", "BREAK BOXES AND COLLECT COINS!" },
            { "value_coin_time_up", "TIME UP... RETRY IN 5" },
            { "value_coin_clear", "100 COIN CLEAR!" },
            { "human_circuit_powered", "POWER ON!" },
            { "title_single", "SINGLE" },
            { "title_multi", "MULTI" },
            { "title_draw", "DRAW" },
            { "title_option", "OPTION" },
            { "option_back", "BACK  ESC" },
            { "option_back_esc", "BACK  ESC" },
            { "ui_back_esc", "BACK  ESC" },
            { "title_exit", "EXIT" },
            { "title_debug", "DEBUG" },
            { "trailer_debug_title", "TRAILER CAPTURE DEBUG" },
            { "trailer_debug_scenario_01", "① 4-player aerial relay" },
            { "trailer_debug_scenario_01_help", "Human throws Bird, then Bird → Cat → Slime relay" },
            { "trailer_debug_start", "START SEQUENCE" },
            { "trailer_debug_back", "BACK  ESC" },
            { "trailer_debug_edit_shapes", "EDIT CHARACTERS" },
            { "steam_header_debug_button", "STEAM HEADER" },
            { "steam_header_capture", "EXPORT 920×430 PNG" },
            { "steam_header_exit", "BACK TO TITLE" },
            { "steam_header_ready", "Check the composition, then export the PNG" },
            { "steam_header_saved", "Saved: {0}" },
            { "player_redrawing_status", "REDRAWING..." },
            { "character_change_ready_room_only", "CHARACTERS CAN ONLY BE CHANGED IN THE READY ROOM." },
            { "trailer_debug_capture_help", "R  REPLAY     H  HIDE HUD     ESC  TITLE" },
            { "trailer_tas_title", "TRAILER TAS RECORDER" },
            { "trailer_tas_help", "Drag actors while idle | Overdub Human → Bird → Cat → Slime | F9 Record  F10 Play  P Pause  O Step" },
            { "trailer_tas_record", "F9 RECORD/STOP" },
            { "trailer_tas_play", "F10 PLAY ALL" },
            { "trailer_tas_reset", "F8 RESET TAKE" },
            { "trailer_tas_clear", "CLEAR SELECTED" },
            { "trailer_tas_pause", "P PAUSE" },
            { "trailer_tas_step", "O STEP" },
            { "trailer_tas_status_idle", "IDLE" },
            { "trailer_tas_status_recording", "RECORDING" },
            { "trailer_tas_status_playback", "PLAYING ALL TRACKS" },
            { "trailer_tas_paused", "PAUSED" },
            { "trailer_tas_status_format", "{0} | Selected: {1} | {2} frames | Speed {3:0.##}x" },
            { "trailer_tas_preset_format", "PRESET {0}" },
            { "trailer_tas_save_preset", "SAVE SHAPES" },
            { "trailer_tas_placement_help", "While idle, drag characters to set their starting positions" },
            { "menu_continue", "Continue" },
            { "menu_to_title", "Title" },
            { "language_settings", "Language Settings" },
            { "multi_play", "MULTI PLAY" },
            { "multi_random_button", "Random Match" },
            { "multi_room_button", "Private" },
            { "multi_random_match", "Random Match" },
            { "multi_searching_players", "Searching for players" },
            { "multi_searching_slot", "Searching" },
            { "multi_random_status_default", "Searching for players...\n\n●□□□\n1 / 4 PLAYERS\n\nP1  You    READY?\n✏  Searching\n✏  Searching\n✏  Searching" },
            { "multi_room_title", "ROOM" },
            { "multi_create_room", "Create Room" },
            { "multi_join_room", "Join Room" },
            { "multi_create_room_body", "Max Players\n\n<color=#1F63D8><b>4</b></color>\n\nVisibility\n\n<color=#0E7A2A><b>Public</b></color>" },
            { "multi_max_players", "Max Players" },
            { "multi_visibility", "Visibility" },
            { "multi_visibility_short", "Visibility" },
            { "multi_public", "Public" },
            { "multi_private", "Private" },
            { "multi_toggle_visibility", "Toggle" },
            { "multi_prev", "<" },
            { "multi_next", ">" },
            { "multi_create", "Create" },
            { "multi_join_room_help", "Enter the room code shown on the host screen.\n\nWith EOS configured, friends can join remotely.\nIf using Direct TCP, enter IP:7777." },
            { "multi_lobby_id_placeholder", "Enter Room Code" },
            { "multi_join", "Join" },
            { "multi_refresh", "Refresh" },
            { "multi_room_lobby", "ROOM LOBBY" },
            { "multi_lobby_status_default", "ID: -\nPlayers 0 / 4" },
            { "multi_copy_id", "Copy ID" },
            { "multi_start_stage_1_1", "Start 1-1" },
            { "multi_stage_select", "Stage Select" },
            { "multi_all_ready_required", "Stage select unlocks when everyone is READY." },
            { "multi_leave_confirm", "Leave this room?" },
            { "multi_leave_yes", "Leave" },
            { "multi_leave_no", "Cancel" },
            { "multi_host_selecting_stage", "Host is choosing a stage..." },
            { "multi_leave", "Leave" },
            { "multi_matching", "Matching..." },
            { "multi_host_only_start", "Only the host can start." },
            { "multi_no_lobby_id", "No room code to copy." },
            { "multi_copied_lobby_id", "Copied Lobby ID." },
            { "multi_copied_room_code", "Copied room code." },
            { "multi_copied_connection_id", "Copied connection ID." },
            { "multi_no_online_lobby_id", "Offline. No room code is available." },
            { "multi_offline_lobby_id", "Offline" },
            { "multi_room_code_label", "Code" },
            { "multi_connecting", "Connecting..." },
            { "multi_room", "Room" },
            { "multi_players", "Players" },
            { "multi_local", "Local" },
            { "multi_ready", "READY" },
            { "multi_wait", "WAIT" },
            { "multi_host", "HOST" },
            { "multi_default_room_name", "Draw Together" },
            { "multi_friend_room_name", "Friend Room" },
            { "online_player_you", "You" },
            { "player_controlled_marker", "YOU" },
            { "online_player_host", "Host" },
            { "online_player_number", "Player {0}" },
            { "online_fake_random_ready", "Fake random match ready." },
            { "online_fake_initialized", "Fake backend initialized." },
            { "online_content_mismatch", "Online play is unavailable because the game data or version does not match. Verify the game files in Steam." },
            { "online_host_disconnected", "The host disconnected, so the room was closed." },
            { "online_logging_in", "Logging in..." },
            { "online_local_test_player", "Online as local test player." },
            { "online_fake_stage_start", "Fake stage start." },
            { "online_private_room_created", "Private room created." },
            { "online_public_room_created", "Public room created." },
            { "online_joined_fake_room", "Joined fake room." },
            { "online_direct_initialized", "Direct TCP initialized." },
            { "online_ready", "Online ready. Host or join a room." },
            { "online_room_created", "Room created. Share the ID with your friend." },
            { "online_failed_to_host", "Failed to host: {0}" },
            { "online_room_id_format", "Room ID must be host-ip:port." },
            { "online_joining_room", "Joining room..." },
            { "online_failed_to_join", "Failed to join: {0}" },
            { "online_left_lobby", "Left lobby." },
            { "online_ready_changed", "Ready changed." },
            { "online_accept_failed", "Accept failed." },
            { "online_player_joined", "{0} joined." },
            { "online_player_left_notice", "{0} left the session." },
            { "online_lobby_updated", "Lobby updated." },
            { "online_starting_stage", "Starting stage {0}." },
            { "online_eos_initialized", "EOS initialized. Configure EOS Plugin before login." },
            { "online_eos_login", "EOS login..." },
            { "online_eos_connect_not_ready", "EOS Connect interface is not ready." },
            { "online_eos_login_failed", "EOS login failed: {0}" },
            { "online_eos_create_lobby_failed", "EOS create lobby failed: {0}" },
            { "online_eos_room_created", "EOS room created. Share this room code." },
            { "online_eos_enter_lobby_id", "Enter an EOS Lobby ID." },
            { "online_eos_enter_room_code", "Enter a room code." },
            { "online_eos_room_code_failed", "Failed to register room code: {0}" },
            { "online_eos_room_code_search_failed", "Room code search failed: {0}" },
            { "online_eos_room_code_not_found", "No room found for code {0}." },
            { "online_eos_room_code_collision", "Room code collided. Please create the room again." },
            { "online_stage_select_opened", "Host opened stage select." },
            { "online_stage_select_closed", "Host closed stage select." },
            { "online_eos_join_lobby_failed", "EOS join lobby failed: {0}" },
            { "online_eos_joined_room", "Joined EOS room." },
            { "online_eos_left_lobby", "Left EOS lobby." },
            { "online_ready_on", "Ready." },
            { "online_ready_off", "Not ready." },
            { "online_eos_device_create_failed", "EOS Device ID creation failed: {0}" },
            { "online_eos_creating_device_id", "Creating EOS Device ID..." },
            { "online_eos_device_login_failed", "EOS Device ID login failed: {0}" },
            { "online_eos_online_as", "EOS online as {0}." },
            { "online_lobby_members_updated", "Lobby members updated." },
            { "online_eos_not_logged_in", "EOS is not logged in. Configure EOS Plugin and login first." },
            { "online_eos_disabled", "EOS is disabled." },
            { "stage_clear_title", "STAGE CLEAR!" },
            { "stage_clear_body", "Stage {0} clear!" },
            { "stage_clear_body_generic", "Clear!" },
            { "stage_clear_next", "Next {0}" },
            { "stage_clear_back", "Stage Select" },
            { "stage_clear_all_done", "All Clear!" },
            { "stage_label", "Stage {0}" },
            { "stage_world_label", "WORLD {0}" },
            { "stage_species_available", "AVAILABLE CHARACTERS" },
            { "stage_species_available_compact", "USE: {0}" },
            { "stage_select_debug_created", "CREATED" },
            { "stage_select_debug_not_created", "NOT CREATED" },
            { "draw_species_locked", "{0} is not available in this stage." },
            { "draw_species_already_used", "Another player is already using {0}. Choose an available character." },
            { "draw_species_swap_title", "CHARACTER SWAP" },
            { "draw_species_swap_hint", "{0} is in use. Press COMPLETE to request a swap." },
            { "draw_species_swap_request", "{0} wants to trade your {1} for {2}." },
            { "draw_species_swap_accept", "SWAP" },
            { "draw_species_swap_reject", "DECLINE" },
            { "draw_species_swap_pending", "Waiting for a response to the swap request..." },
            { "draw_species_swap_accepted", "Characters swapped!" },
            { "draw_species_swap_rejected", "The swap request was declined." },
            { "draw_species_swap_unavailable", "The other player could not be found." },
            { "stage_object_grain_emitter", "GRAIN DISPENSER" },
            { "stage_object_grain_scale", "100g SCALE" },
            { "stage_object_grain_gate", "WEIGHT GATE" },
            { "stage_object_escort_friend_button", "FRIEND BUTTON" },
            { "stage_object_escort_player_one_way_floor", "PLAYER-ONLY ONE-WAY FLOOR" },
            { "species_human", "Human" },
            { "species_cat", "Cat" },
            { "species_bird", "Bird" },
            { "species_turtle", "Turtle" },
            { "species_slime", "Slime" },
            { "stage_editor_objects_tab", "Objects" },
            { "stage_editor_links_tab", "Links" },
            { "stage_editor_status_debug_start_help", "Drag the character to start test play from that position. The saved stage spawn is not changed." },
            { "stage_editor_status_debug_start_drag", "Moving the test-play start position." },
            { "stage_editor_status_debug_start", "Test start: X {0:0.00} / Y {1:0.00}" },
            { "stage_editor_status_validation_failed", "Cannot save: {0} error(s). {1}" },
            { "stage_editor_status_saved_with_warnings", "Saved: {0} ({1} warning(s))" },
            { "multi_room_status_default", "Create Room\nRoom Name  [........]\nPlayers  2 / 3 / 4\nPublic / Private\n\nJoin Room\nRoom ID  [......]" },
            { "stage_editor_help_runtime", "Drag to place / Drag empty space in Select mode: box select / Arrows: move selection (hold Alt for fine movement)\nWheel: scale  Shift+Wheel: rotate  +Alt: fine rotate\nX+Wheel: width  Alt+Wheel: height  Moving platform M+Wheel: direction" },
            { "stage_editor_selected_multiple", "{0} objects selected" },
            { "stage_editor_status_range_selecting", "Objects fully enclosed by the box will be selected." },
            { "stage_editor_status_range_selected", "Selected {0} objects. Drag a selected object to move them together." },
            { "stage_editor_status_range_empty", "No objects are fully enclosed by the selection box." },
            { "stage_editor_status_range_moved", "Moved {0} selected objects together." },
            { "stage_editor_category", "Category" },
            { "stage_editor_search", "Search" },
            { "stage_editor_type", "Type" },
            { "stage_editor_search_placeholder", "Search word" },
            { "stage_editor_object_list", "Object List" },
            { "stage_editor_link_list", "Link List" },
            { "stage_editor_width_minus", "W-" },
            { "stage_editor_width_plus", "W+" },
            { "stage_editor_height_minus", "H-" },
            { "stage_editor_height_plus", "H+" },
            { "stage_editor_redo", "Redo" },
            { "stage_editor_copy", "Copy" },
            { "stage_editor_status_copied", "Object copied flush to the right." },
            { "stage_editor_copy_right", "→ RIGHT" },
            { "stage_editor_copy_down", "↓ DOWN" },
            { "stage_editor_copy_left", "← LEFT" },
            { "stage_editor_copy_up", "↑ UP" },
            { "stage_editor_status_copy_direction", "Copy direction: {0}" },
            { "stage_editor_status_copied_direction", "Object copied flush toward {0}." },
            { "stage_editor_status_nudge", "Position: X {0:0.00} / Y {1:0.00} (arrow keys)" },
            { "stage_editor_action_strength", "Launch Power" },
            { "stage_editor_move_distance", "Move Distance" },
            { "stage_editor_move_speed", "Move Speed" },
            { "stage_editor_bomb_fuse_seconds", "Fuse (sec)" },
            { "stage_editor_crumble_delay", "Crumble Delay (sec)" },
            { "stage_editor_bomb_wall_hits", "Explosions Required" },
            { "stage_editor_conveyor_speed", "Belt Speed" },
            { "stage_editor_drop_interval", "Drop Interval (sec)" },
            { "stage_editor_beam_interval", "Beam Interval (sec)" },
            { "stage_editor_box_size", "Box Size" },
            { "stage_editor_bomb_size", "Bomb Size" },
            { "stage_editor_spike_size", "Spike Size" },
            { "stage_editor_status_move_distance", "Move distance: {0:0.0}" },
            { "stage_editor_status_move_speed", "Move speed: {0:0.0}" },
            { "stage_editor_status_bomb_fuse_seconds", "Fuse time: {0:0.0} sec" },
            { "stage_editor_status_crumble_delay", "Crumble delay: {0:0.0} sec" },
            { "stage_editor_status_bomb_wall_hits", "Wall durability: {0:0} explosions" },
            { "stage_editor_status_conveyor_speed", "Belt speed: {0:0.0}" },
            { "stage_editor_status_drop_interval", "Drop interval: {0:0.0} sec" },
            { "stage_editor_status_box_size", "Dropped box size: {0:0.0}" },
            { "stage_editor_status_spike_size", "Dropped spike size: {0:0.0}" },
            { "stage_editor_status_bomb_size", "Manufactured bomb size: {0:0.0}" },
            { "stage_editor_conveyor_left", "Direction: ← Left" },
            { "stage_editor_conveyor_right", "Direction: Right →" },
            { "stage_editor_status_conveyor_direction", "Conveyor direction: {0}" },
            { "stage_editor_box_pattern_all", "Boxes: □○△" },
            { "stage_editor_box_pattern_square", "Boxes: Square" },
            { "stage_editor_box_pattern_round", "Boxes: Round" },
            { "stage_editor_box_pattern_triangle", "Boxes: Triangle" },
            { "stage_editor_bomb_pattern_both", "Bombs: Alternate Both" },
            { "stage_editor_bomb_pattern_spawn", "Bomb: Fuse on Spawn" },
            { "stage_editor_bomb_pattern_pickup", "Bomb: Fuse on Pickup" },
            { "stage_editor_status_box_pattern", "Dropped boxes: {0}" },
            { "stage_editor_status_bomb_pattern", "Manufactured bombs: {0}" },
            { "stage_editor_status_move_direction", "Move direction: {0:0}° (M + wheel)" },
            { "stage_editor_status_action_strength", "Launch power: {0:0.0}" },
            { "stage_editor_weight_threshold", "Per Player" },
            { "stage_editor_status_weight_threshold", "Scale trigger weight set to {0:0} INK per player." },
            { "stage_editor_status_weight_threshold_invalid", "Enter a number from 1 to 2000." },
            { "ink_personal_cap", "PERSONAL CAP" },
            { "ink_team_formula", "TEAM CAP ({0}P)" },
            { "draw_pen_size", "PEN SIZE" },
            { "draw_ink", "INK" },
            { "draw_clear_part", "CLEAR PART" },
            { "draw_undo_once", "UNDO" },
            { "draw_reset_all", "RESET ALL" },
            { "draw_reset_confirm_title", "RESET EVERYTHING?" },
            { "draw_reset_confirm_message", "Erase every body part and restore the original character." },
            { "draw_reset_confirm_yes", "RESET" },
            { "draw_reset_confirm_no", "CANCEL" },
            { "draw_preset_title", "{0} PRESETS" },
            { "draw_preset_button", "PRESETS" },
            { "draw_preset_sets_title", "5-CHARACTER PRESETS" },
            { "draw_preset_five_species", "HUMAN   CAT   BIRD   TURTLE   SLIME" },
            { "draw_preset_all_species", "all five characters" },
            { "draw_preset_slot", "SLOT {0}" },
            { "draw_preset_saved", "SAVED" },
            { "draw_preset_empty", "EMPTY" },
            { "draw_preset_register", "SAVE" },
            { "draw_preset_apply", "SET" },
            { "draw_preset_save_confirm_title", "SAVE TO PRESET {0}?" },
            { "draw_preset_save_confirm_message", "Save the current {0} drawing to preset {1}." },
            { "draw_preset_overwrite_confirm_message", "Overwrite the {0} drawing in preset {1}." },
            { "draw_preset_load_confirm_title", "SET PRESET {0}?" },
            { "draw_preset_load_confirm_message", "Replace the current {0} drawing with preset {1}." },
            { "stage_category_decoration", "Background" },
            { "stage_object_background_tree", "Background Tree" },
            { "stage_object_background_grass", "Background Grass" },
            { "stage_object_background_flower", "Background Flower" },
            { "stage_object_background_bush", "Background Bush" },
            { "stage_object_background_cloud", "Background Cloud" },
            { "stage_object_background_push", "Background PUSH" },
            { "stage_object_background_arrow", "Background Arrow" },
            { "stage_object_background_cat_face", "Cat Face" },
            { "stage_object_background_dog_face", "Dog Face" },
            { "stage_object_background_stick_figure", "Stick Figure" },
            { "stage_object_background_smiley", "Smiley" },
            { "stage_object_background_heart", "Heart" },
            { "stage_object_background_star", "Star" },
            { "stage_object_background_moon", "Moon" },
            { "stage_object_background_sun", "Sun" },
            { "stage_object_background_rain", "Rain" },
            { "stage_object_background_lightning", "Lightning" },
            { "stage_object_background_rainbow", "Rainbow" },
            { "stage_object_background_mountain", "Mountain" },
            { "stage_object_background_four_leaf_clover", "Four-leaf Clover" },
            { "stage_object_background_mushroom", "Mushroom" },
            { "stage_object_background_apple", "Apple" },
            { "stage_object_background_banana", "Banana" },
            { "stage_object_background_watermelon", "Watermelon" },
            { "stage_object_background_donut", "Donut" },
            { "stage_object_background_ice_cream", "Ice Cream" },
            { "stage_object_background_coffee_cup", "Coffee Cup" },
            { "stage_object_background_pizza", "Pizza" },
            { "stage_object_background_bread", "Bread" },
            { "stage_object_background_paper_airplane", "Paper Airplane" },
            { "stage_object_background_airplane", "Airplane" },
            { "stage_object_background_rocket", "Rocket" },
            { "stage_object_background_ufo", "UFO" },
            { "stage_object_background_hot_air_balloon", "Hot-air Balloon" },
            { "stage_object_background_house", "House" },
            { "stage_object_background_castle", "Castle" },
            { "stage_object_background_treasure_chest", "Treasure Chest" },
            { "stage_object_background_mole", "Mole" },
            { "stage_object_background_fossil", "Fossil" },
            { "stage_object_background_crystal", "Crystal" },
            { "stage_object_background_ancient_pot", "Ancient Pot" },
            { "stage_object_background_key", "Key" },
            { "stage_object_background_key_needed", "Key to Keyhole" },
            { "stage_object_background_sword", "Sword" },
            { "stage_object_background_crown", "Crown" },
            { "stage_object_background_shield", "Shield" },
            { "stage_object_background_gem", "Gem" },
            { "stage_object_background_coin", "Coin" },
            { "stage_object_background_bone", "Bone" },
            { "stage_object_background_light_bulb", "Light Bulb" },
            { "stage_object_background_gear", "Gear" },
            { "stage_object_background_spring", "Spring" },
            { "stage_object_background_magnet", "Magnet" },
            { "stage_object_background_dice", "Dice" },
            { "stage_object_background_speech_bubble", "Speech Bubble" },
            { "stage_object_background_check_mark", "Check Mark" },
            { "stage_object_background_question_mark", "Question Mark" },
            { "stage_object_background_exclamation_mark", "Exclamation Mark" },
            { "stage_object_background_loop_arrow", "Loop Arrow" },
            { "stage_object_background_jump", "JUMP!" },
            { "stage_object_background_throw", "Throw!" },
            { "stage_object_background_start", "START!" },
            { "stage_object_background_goal", "GOAL!" },
            { "stage_editor_status_rotate_decoration", "Rotate: Shift + wheel (0.5 degree precision)" },
            { "stage_editor_status_rotate_mounted", "Rotate: Shift + wheel (15°), with Alt (1°)" },
            { "stage_editor_status_rotate_object", "Rotate: Shift + wheel (15°), with Alt (1°)" },
            { "stage_editor_status_scale_decoration", "Scale: mouse wheel" },
            { "stage_editor_link_action", "Action" },
            { "stage_editor_link_mode_reveal", "Appear" },
            { "stage_editor_link_mode_hide", "Disappear" },
            { "stage_editor_link_mode_unlock", "Unlock" },
            { "stage_editor_link_mode_move", "Move" },
            { "stage_editor_link_mode_move_right", "Move Platform Right" },
            { "stage_editor_link_mode_move_up", "Move Platform Up" },
            { "stage_editor_link_mode_move_left", "Move Platform Left" },
            { "stage_editor_link_mode_move_down", "Move Platform Down" },
            { "stage_editor_status_link_action_changed", "Link action: {0}" },
            { "stage_editor_status_key_requires_keyhole", "A key must link to a keyhole." },
            { "stage_editor_status_unlock_action_fixed", "Key to keyhole links always use Unlock." },
            { "stage_editor_snap_attach", "Snap" },
            { "stage_editor_link_source", "Link From" },
            { "stage_editor_link_target", "Link To" },
            { "stage_editor_unlink", "Unlink" },
            { "stage_editor_selected_add", "Add: {0} / Snap: {1}" },
            { "stage_editor_selected_object", "{0}  Pos {1:0.0},{2:0.0}  Size {3:0.0},{4:0.0}" },
            { "stage_editor_boundary_resize_hint", "Width = right wall / Height = ceiling (left wall and lower edge stay fixed)" },
            { "stage_editor_boundary_quick", "Frame" },
            { "stage_editor_status_boundary_fitted", "Created a frame around the placed objects. Select it to adjust the right wall and ceiling." },
            { "stage_editor_status_wall_fitted", "Fitted the wall between the upper and lower surfaces." },
            { "stage_goal_label", "GOAL" },
            { "stage_room_number", "ROOM {0}" },
            { "stage_weapon_bomb", "BOMB" },
            { "stage_weapon_missile", "MISSILE" },
            { "stage_ready_label", "READY" },
            { "stage_dynamite_label", "TNT" },
            { "stage_editor_background_color", "Background" },
            { "laser_relay_editor_players", "PLAYERS {0}" },
            { "laser_relay_editor_round", "STAGE {0}" },
            { "laser_relay_editor_preview", "14-3 preview: {0} player(s) / stage {1}" },
            { "stage_editor_color_title", "Background Color" },
            { "stage_editor_color_reset", "Default" },
            { "stage_editor_color_close", "Close" },
            { "stage_editor_color_opacity", "Opacity" },
            { "stage_editor_status_background_color", "Background color changed. Save the stage to keep it." },
            { "stage_editor_status_add_rect", "Add mode: {0}. Drag on the map to create it." },
            { "stage_editor_status_add_point", "Add mode: {0}. Click the map to place it." },
            { "stage_editor_status_selected_from_list", "Selected from list." },
            { "stage_editor_no_match", "No match" },
            { "stage_editor_none", "(none)" },
            { "stage_category_terrain", "Terrain" },
            { "stage_category_start_goal", "Start/Goal" },
            { "stage_category_switch", "Switch" },
            { "stage_category_door_gate", "Door/Gate" },
            { "stage_category_movable", "Movable" },
            { "stage_category_action", "Action" },
            { "stage_category_trap", "Trap" },
            { "stage_category_gimmick", "Gimmick" },
            { "stage_category_enemy", "Enemies" },
            { "stage_object_platform", "Floor" },
            { "stage_object_wall", "Wall" },
            { "stage_object_spawn", "Start" },
            { "stage_object_goal", "Goal" },
            { "stage_object_balance_scale", "Balance Scale" },
            { "stage_object_weight", "Weight" },
            { "stage_object_ceiling", "Ceiling" },
            { "stage_object_half_platform", "Half Floor" },
            { "stage_object_one_way_platform", "One-way Floor" },
            { "stage_object_ice_floor", "Ice Floor" },
            { "stage_object_slippery_slope", "Slippery Slope" },
            { "stage_object_climbable_wall", "Climbable Wall" },
            { "stage_object_rope", "Rope" },
            { "stage_object_ladder", "Ladder" },
            { "stage_object_cloud_platform", "Cloud Floor" },
            { "stage_object_breakable_floor", "Breakable Floor" },
            { "stage_object_falling_floor", "Crumbling Floor" },
            { "stage_object_moving_platform", "Moving Floor" },
            { "stage_object_moving_one_way_platform", "Sticky Moving One-Way Floor" },
            { "ricochet_breaker_title", "8-2  REFLECT DRAW" },
            { "ricochet_breaker_goal", "Bounce the ball with your body and break NICO DRAW!" },
            { "ricochet_breaker_blocks", "BLOCKS {0}  BALLS ×{1}" },
            { "ricochet_breaker_retry", "TIME UP  RETRY IN {0}" },
            { "ricochet_breaker_start_in", "START IN {0}" },
            { "ricochet_enemy_round", "ROUND {0} / {1}" },
            { "ricochet_enemy_balls", "BALL {0} / {1}" },
            { "ricochet_enemy_next", "NEXT ROUND IN {0}" },
            { "ricochet_enemy_failed", "3 BALLS LOST! RETRY" },
            { "ricochet_enemy_clear", "ALL ROUNDS CLEAR!" },
            { "ricochet_enemy_remaining", "ENEMIES LEFT {0}" },
            { "stage_object_handgun", "Gun" },
            { "stage_object_bullet_breakable_wall", "Bullet-Breakable Wall" },
            { "stage_object_spike_planet", "Spike Planet" },
            { "stage_object_enemy_flyer_zigzag", "Zigzag Flyer" },
            { "stage_object_enemy_flyer_orbit", "Orbit Flyer" },
            { "stage_object_bazooka", "Bazooka" },
            { "stage_object_enemy_bomber", "Bombing Flyer" },
            { "stage_object_moving_spike_planet", "Moving Spike Ball" },
            { "stage_object_pose_character_key", "Pose Key" },
            { "stage_object_pose_character_keyhole", "Pose Keyhole" },
            { "stage_object_updraft_zone", "Updraft" },
            { "stage_object_speed_ring2_x", "Speed Mark" },
            { "stage_object_speed_ring3_x", "Speed Mark (Strong)" },
            { "stage_object_redraw_zone", "Redraw Zone" },
            { "stage_editor_bullet_wall_hits", "Bullets Required" },
            { "stage_editor_status_bullet_wall_hits", "Bullets Required: {0}" },
            { "stage_object_rotating_platform", "Rotating Floor" },
            { "stage_object_checkpoint", "Checkpoint" },
            { "stage_object_warp_entrance", "Warp Entrance" },
            { "stage_object_warp_exit", "Warp Exit" },
            { "stage_object_respawn_point", "Respawn Point" },
            { "stage_object_mid_goal", "Mid Goal" },
            { "stage_object_goal_effect", "Goal Effect" },
            { "stage_object_button", "Button" },
            { "stage_object_weight_button", "Weight Button" },
            { "stage_object_simultaneous_button", "Simultaneous Button" },
            { "stage_object_hold_button", "Hold Button" },
            { "stage_object_triangle_box", "Triangle Box" },
            { "stage_object_lever", "Lever" },
            { "stage_object_toggle_switch", "Toggle Switch" },
            { "stage_object_timer_switch", "Timer Switch" },
            { "stage_object_sensor", "Sensor" },
            { "stage_object_red_switch", "Red Switch" },
            { "stage_object_blue_switch", "Blue Switch" },
            { "stage_object_green_switch", "Green Switch" },
            { "stage_object_yellow_switch", "Yellow Switch" },
            { "stage_object_pressure_plate", "Pressure Plate" },
            { "stage_object_remote_control", "Remote Control" },
            { "stage_object_ink_scale", "Ink Scale" },
            { "stage_object_door", "Door" },
            { "stage_object_locked_door", "Locked Door" },
            { "stage_object_shutter", "Shutter" },
            { "stage_object_fence", "Fence" },
            { "stage_object_laser_gate", "Laser Gate" },
            { "stage_object_color_gate", "Color Gate" },
            { "stage_object_one_way_gate", "One-way Gate" },
            { "stage_object_timed_gate", "Timed Gate" },
            { "stage_object_breakable_wall", "Bomb-Breakable Wall" },
            { "stage_object_hidden_wall", "Hidden Wall" },
            { "stage_object_wood_box", "Wood Box" },
            { "stage_object_iron_box", "Iron Box" },
            { "stage_object_ball", "Ball" },
            { "stage_object_barrel", "Barrel" },
            { "stage_object_rock", "Rock" },
            { "stage_object_ice_block", "Ice Block" },
            { "stage_object_floating_box", "Floating Box" },
            { "stage_object_rubber_box", "Rubber Box" },
            { "stage_object_bomb", "Bomb (Fuse Starts on Spawn)" },
            { "stage_object_pickup_fuse_bomb", "Bomb (Fuse Starts on Pickup)" },
            { "stage_object_bomb_dropper", "Bomb Maker" },
            { "stage_object_dynamite", "Dynamite" },
            { "stage_object_enemy_walker", "Walker Enemy" },
            { "stage_object_enemy_jumper", "Jumper Enemy" },
            { "stage_object_enemy_charger", "Charger Enemy" },
            { "stage_object_enemy_flyer", "Flying Enemy" },
            { "stage_object_enemy_shooter", "Shooter Enemy" },
            { "stage_object_enemy_dropper", "Enemy Spawner" },
            { "stage_object_missile_launcher", "Missile Launcher" },
            { "stage_editor_link_mode_activate", "Activate" },
            { "stage_editor_status_activate_action_fixed", "This device always uses Activate" },
            { "stage_editor_launch_interval", "Launch Interval" },
            { "stage_editor_enemy_size", "Enemy Size" },
            { "stage_editor_status_enemy_pattern", "Spawned enemy: {0}" },
            { "stage_editor_status_enemy_size", "Enemy size: {0:0.0}" },
            { "stage_object_key", "Key" },
            { "stage_object_coin", "Coin" },
            { "stage_object_star", "Star" },
            { "stage_object_battery", "Battery" },
            { "stage_object_bucket", "Bucket" },
            { "stage_object_jump_pad", "Jump Pad" },
            { "stage_object_spring", "Spring" },
            { "stage_object_conveyor_left", "Conveyor Left" },
            { "stage_object_conveyor_right", "Conveyor Right" },
            { "stage_object_elevator", "Elevator" },
            { "stage_object_fan", "Fan" },
            { "stage_object_magnet", "Magnet" },
            { "stage_object_belt", "Conveyor Belt" },
            { "stage_object_box_dropper", "Box Dropper" },
            { "stage_object_spike_dropper", "Spike Dropper" },
            { "stage_object_collectible_fish", "Fish" },
            { "stage_object_collectible_coin", "Coin" },
            { "stage_object_collectible_star", "Star" },
            { "stage_object_challenge_clock", "Digital Clock" },
            { "stage_object_beam_emitter", "Beam Emitter" },
            { "stage_rule_normal", "Normal" },
            { "stage_rule_timed", "Time Limit" },
            { "stage_rule_survival", "Survival" },
            { "stage_rule_seconds", "{0:0}s" },
            { "stage_rule_count", "x{0}" },
            { "stage_rule_all", "ALL" },
            { "stage_editor_status_rule", "Stage rule: {0}" },
            { "challenge_time_remaining", "Time {0:0.0}" },
            { "challenge_collection_progress", "{0}  {1} / {2}" },
            { "challenge_time_up", "TIME UP" },
            { "challenge_retry_hint", "Press R to retry" },
            { "survival_mode_title", "11-2  SURVIVAL" },
            { "survival_goal", "ONE SURVIVOR CLEARS!" },
            { "survival_goal_sub", "MOVE TO THE GLOWING FLOOR" },
            { "grain_rain_target", "TARGET  {0:0}g  (90g x PLAYERS)" },
            { "grain_rain_ready", "MOVE INTO POSITION BEFORE THE RAIN" },
            { "grain_rain_catch", "CATCH THE GRAINS!" },
            { "grain_rain_measuring", "WEIGHING..." },
            { "grain_rain_floor_clear", "REMOVING GRAINS ON THE FLOOR" },
            { "grain_rain_clear", "CLEAR!" },
            { "grain_rain_failed", "SO CLOSE!" },
            { "grain_rain_result", "GRAINS ON HEADS  {0:0}g / {1:0}g" },
            { "grain_rain_success", "RARE GRAIN TARGET REACHED!" },
            { "grain_rain_retry", "TRY A BETTER HEAD SHAPE" },
            { "grain_rain_round", "STAGE {0}/{1}" },
            { "grain_rain_blizzard", "CATCH THE GRAIN BLIZZARD BLOWING IN FROM THE RIGHT!" },
            { "grain_rain_forecast", "FAST GRAINS WILL HIT THE MARKED SPOTS—ONE PER PLAYER!" },
            { "ice_speedrun_title", "10-1  ICE SPEEDRUN - ONE PLAYER FINISHES" },
            { "ice_speedrun_start", "START!" },
            { "ice_speedrun_time_up", "TIME UP  RETRY IN {0}" },
            { "survival_get_ready", "GET READY!" },
            { "survival_start", "START!" },
            { "game_over", "GAME OVER" },
            { "msg_body_too_large_for_spawn", "THIS BODY DOES NOT FIT AT THE START. PLEASE REDRAW IT SMALLER." },
            { "msg_body_too_large_for_ready_room", "THIS BODY MAY STICK OUT OF THE ROOM. PLEASE ADJUST ITS SIZE." },
            { "ready_room_status", "READY {0} / {1}" },
            { "ready_room_recommended", "RECOMMENDED" },
            { "ready_room_recommended_none", "NONE" },
            { "ready_room_restriction", "NO CHARACTER CHANGE OR REDRAW AFTER LEAVING" },
            { "ready_room_restriction_redraw_allowed", "NO CHARACTER CHANGE AFTER LEAVING (REDRAW OK)" },
            { "ready_room_game_default", "GET READY TO START THE GAME" },
            { "ready_room_game_2_2", "COLLECT THE FISH BEFORE TIME RUNS OUT" },
            { "ready_room_game_4_3", "DEFEAT THE CRAYON KING" },
            { "ready_room_game_6_2", "DODGE THE HURDLES AND SURVIVE" },
            { "ready_room_game_6_3", "PLUG EVERY AQUARIUM HOLE WITH YOUR BODIES" },
            { "ready_room_game_7_1", "USE YOUR BODY SHAPE TO UNLOCK THE PATH" },
            { "ready_room_game_8_1", "DODGE THE FALLING PILLARS" },
            { "ready_room_game_8_2", "REFLECT THE BALL AND BREAK EVERY BLOCK" },
            { "ready_room_game_8_3", "PULL THE BALL WITH INK MAGNETS AND SINK IT" },
            { "ready_room_game_9_1", "DODGE THE MISSILES AND SURVIVE" },
            { "ready_room_game_9_2", "COLLECT COINS WHILE FALLING" },
            { "ready_room_game_9_3", "COLLECT THE FALLING GRAINS AND REACH THE TARGET WEIGHT" },
            { "ready_room_game_10_1", "SPEED RUN. REACH THE GOAL BEFORE TIME RUNS OUT." },
            { "ready_room_game_10_3", "RICOCHET BULLETS TO DEFEAT THE ENEMIES" },
            { "ready_room_game_11_1", "LIGHT THE DARKNESS AND ESCAPE THE GHOSTS" },
            { "ready_room_game_11_2", "SURVIVE UNTIL TIME RUNS OUT" },
            { "ready_room_game_11_3", "THROW BOMBS TO BREAK EVERY BLOCK" },
            { "ready_room_game_12_1", "BREAK BOXES AND COLLECT COINS" },
            { "ready_room_game_12_2", "COLLECT COINS BEFORE TIME RUNS OUT" },
            { "ready_room_game_12_3", "COLLECT COINS BEFORE TIME RUNS OUT" },
            { "ready_room_game_13_1", "DEFEND YOUR FRIEND FROM THE ENEMIES" },
            { "ready_room_game_13_2", "BOUNCE THE BALL BACK TO DEFEAT THE ENEMIES" },
            { "ready_room_game_14_1", "REACH THE GOAL BEFORE TIME RUNS OUT" },
            { "ready_room_game_14_2", "USE THE UMBRELLA TO STAY OUT OF THE RAIN" },
            { "ready_room_game_14_3", "RELAY THE LASER THROUGH EVERY BODY TO THE GOAL" },
            { "ready_room_game_15_1", "DEFEAT CRAYON DEVIL" },
            { "ready_room_game_15_2", "DEFEAT CRAYON STALKER" },
            { "ready_room_game_15_3", "DEFEAT THE ENEMIES" },
            { "ready_room_clear_one_survivor", "CLEAR IF AT LEAST ONE PLAYER SURVIVES." },
            { "ready_room_clear_one_goal", "CLEAR WHEN ANY ONE PLAYER REACHES THE GOAL." },
            { "spike_chase_monitor_title", "SPIKE WALL LAUNCH" },
            { "spike_chase_monitor_ready", "WAIT FOR THE SIGNAL!" },
            { "spike_chase_monitor_goal", "RUN TO THE GOAL!" },
            { "wind_speedrun_title", "THE WIND KEEPS CHANGING!" },
            { "wind_speedrun_hint", "LESS INK = STRONGER WIND" },
            { "wind_speedrun_timer", "TIME {0:0.0}" },
            { "wind_speedrun_time_up", "TIME UP" },
            { "umbrella_rain_title", "STAY UNDER THE UMBRELLA!" },
            { "umbrella_rain_hint", "RAIN KNOCKS YOU OUT - REACH SHELTER" },
            { "umbrella_rain_all_out", "EVERYONE WAS CAUGHT IN THE RAIN!" },
            { "linked_shield_title", "14-3  LINKED SHIELDS" },
            { "linked_shield_ready", "CHECK EACH BUTTON LINK!" },
            { "linked_shield_hint", "BLOCK MISSILES WITH 1-SECOND SHIELDS!" },
            { "linked_shield_failed", "TEAM DOWN - RESTARTING" },
            { "linked_shield_clear", "60-SECOND DEFENSE CLEAR!" },
            { "laser_relay_monitor", "ROUND {0}/3    {1:0.0}s LEFT" },
            { "laser_relay_progress", "GOALS {0}/{1}" },
            { "laser_relay_hint", "YOUR BODY OUTLINE ANGLE BECOMES THE LASER REFLECTION ANGLE" },
            { "laser_relay_round_clear", "ROUND {0} CLEAR!" },
            { "laser_relay_timeout", "TIME UP!  RESTARTING THIS ROUND" },
            { "flying_boss_hp", "CRAYON DEVIL  HP  {0} / {1}" },
            { "flying_boss_ready", "3 - 2 - 1  READY TO LAUNCH" },
            { "flying_boss_controls", "MOVE: PLATFORM  CLICK: MISSILE  F: BOMB" },
            { "flying_boss_clear", "BOSS DEFEATED!" },
            { "flying_boss_failed", "ALL PLATFORMS DOWN" },
            { "flying_boss_homing_warning", "HOMING MISSILES - SPREAD OUT!" },
            { "flying_boss_target_warning", "TARGETED - LEAVE THE TEAM!" },
            { "flying_boss_suction_warning", "SUCTION - MOVE AWAY!" },
            { "mirror_boss_recording", "RECORDING MOVEMENT...  {0}s" },
            { "mirror_boss_recording_hint", "Run, jump, turn and stop — your double is learning." },
            { "mirror_boss_phase", "PHASE {0}  DOUBLES LEFT: {1}" },
            { "mirror_boss_hint", "Lure one onto a launch floor, then step on its red switch!" },
            { "mirror_boss_clear", "ALL DOUBLES DEFEATED!" },
            { "mirror_boss_clear_sub", "There was never another boss." },
            { "mirror_brawl_phase_ready", "PHASE {0}  STARTS IN" },
            { "mirror_brawl_ready_hint", "Defeat the CPU team wearing your exact forms." },
            { "mirror_brawl_phase", "PHASE {0}  LEFT: {1}" },
            { "mirror_brawl_phase_clear", "PHASE {0} CLEAR!" },
            { "mirror_brawl_hint", "Claim weapons with red switches. F: throw bomb  CLICK: missile" },
            { "mirror_brawl_missile_ready", "MISSILE READY!  Aim with the mouse and click." },
            { "mirror_brawl_clear", "CPU TEAM DEFEATED!" },
            { "mirror_brawl_clear_sub", "FINAL BATTLE CLEARED!" },
            { "mirror_brawl_failed", "TIME UP" },
            { "mirror_brawl_time_up", "TIME UP!" },
            { "mirror_brawl_all_down", "EVERYONE IS DOWN!" },
            { "mirror_brawl_retry", "FAILED! Restarting from the beginning in 3 seconds." },
            { "side_boss_hp", "CRAYON STALKER  HP {0} / {1}" },
            { "side_boss_ready", "3 - 2 - 1  GET READY" },
            { "side_boss_run", "RUN RIGHT! USE THE GREEN WEAPONS!" },
            { "side_boss_clear", "PURSUER DEFEATED!" },
            { "side_boss_failed", "EVERYONE WAS CAUGHT" },
            { "spike_chase_run", "RUN!" },
            { "survival_watch_floor", "WATCH THE GLOWING FLOOR!" },
            { "survival_remaining", "SURVIVE!  TIME LEFT" },
            { "survival_safe_countdown", "SAFE FLOORS {0} / DROP IN {1:0.0}" },
            { "survival_floor_dropped", "STAY ON THE SAFE FLOOR!" },
            { "survival_all_dead", "ALL PLAYERS OUT" },
            { "survival_retrying", "RESTARTING SOON" },
            { "survival_clear_title", "SURVIVOR FOUND!" },
            { "survival_clear_sub", "SURVIVAL CLEARED" },
            { "pillar_survival_title", "8-1  PILLAR SURVIVAL" },
            { "pillar_survival_goal_sub", "WATCH THE WARNING AND DODGE THE PILLARS" },
            { "pillar_survival_watch_up", "INCOMING!  LEAVE THE GLOWING ZONE" },
            { "pillar_survival_clear", "PILLAR SURVIVAL CLEARED!" },
            { "boss_name", "4-3  CRAYON KING" },
            { "boss_health", "HP  {0} / {1}" },
            { "boss_enter_room", "ENTER THE NEXT ROOM" },
            { "boss_appears", "WARNING...  BOSS!" },
            { "boss_fight", "SHOOT TO ATTACK  /  TURTLE SHELL TO GUARD" },
            { "boss_invulnerable", "GUARD!  BOSS IS INVULNERABLE" },
            { "boss_charge", "CHARGE!  GET OUT OF THE WAY" },
            { "boss_charge_countdown", "CHARGE IN {0}!  GET TO THE HIGH FLOORS" },
            { "boss_special_warning", "SPECIAL ATTACK!  DODGE THE RICOCHETS" },
            { "boss_defeated", "BOSS DEFEATED!" },
            { "boss_all_out", "ALL PLAYERS OUT...  RETRYING" },
            { "boss_waiting_count", "WAITING ROOM  {0} / {1}  GATHER UP!" },
            { "jump_rope_mode_title", "6-2  HURDLE SURVIVAL" },
            { "jump_rope_goal", "ONE SURVIVOR CLEARS!" },
            { "jump_rope_goal_sub", "JUMP OVER HURDLES FROM THE RIGHT FOR 60 SECONDS" },
            { "jump_rope_ready", "WATCH FOR HURDLES FROM THE RIGHT" },
            { "jump_rope_jump_now", "JUMP!" },
            { "jump_rope_keep_jumping", "JUMP THE HURDLES!" },
            { "jump_rope_slow", "SLOW... CATCH YOUR BREATH!" },
            { "jump_rope_accelerate", "SPEED UP! FINAL SPRINT!" },
            { "jump_rope_all_out", "ALL PLAYERS OUT" },
            { "jump_rope_clear_title", "HURDLE SURVIVAL CLEARED!" },
            { "jump_rope_clear_sub", "SURVIVOR FOUND" },
            { "escort_title", "5-3  ESCORT THE FRIEND" },
            { "escort_friend_active", "FRIEND MOVING" },
            { "escort_instruction", "BUILD A PATH WITH BOXES, PLATFORMS, AND YOUR BODY" },
            { "escort_defense_title", "10-2  PROTECT THE FRIEND" },
            { "escort_friend_defeat_cry", "AAAH!" },
            { "escort_defense_instruction", "RUN AHEAD AND CLEAR TRAPS, BOXES, AND ENEMIES" },
            { "escort_respawning", "NEXT FRIEND IN  {0:0.0}" },
            { "escort_respawn_sub", "PREPARING A NEW FRIEND..." },
            { "escort_clear_title", "ESCORT COMPLETE!" },
            { "escort_clear_sub", "THE FRIEND REACHED THE GOAL" },
            { "drawn_escort_monitor", "ROUND {0}/3    BUILD {1:0.0}s" },
            { "drawn_escort_plan", "CONNECT A PATH WITH YOUR BODY" },
            { "drawn_escort_running", "GO!  HELP THE FRIEND REACH THE GOAL" },
            { "drawn_escort_round_running", "ROUND {0}/3" },
            { "drawn_escort_round_clear", "ROUND {0} CLEAR!" },
            { "aquarium_seal_monitor", "ROUND {0}/3    {1:0.0}s LEFT" },
            { "aquarium_seal_progress", "HOLES {0}/{1}    PLUG THEM ALL WITH YOUR BODIES!" },
            { "aquarium_seal_round_clear", "ROUND {0} CLEAR!" },
            { "aquarium_seal_timeout", "TIME UP!  RESTARTING THIS ROUND" },
            { "aquarium_seal_box_hint", "PLUG THE HOLES!" },
            { "tilt_board_monitor", "ROUND {0}/3    {1:0.0}s LEFT" },
            { "tilt_board_progress", "BALLS {0}/{1}    ROLL THEM INTO THE HOLES!" },
            { "tilt_board_maze", "PULL THE BALL THROUGH THE MAZE WITH MAGNETISM!" },
            { "tilt_board_round_clear", "ROUND {0} CLEAR!" },
            { "tilt_board_timeout", "TIME UP!  RETRYING THIS ROUND" },
            { "tilt_board_hint", "MOVE WITHIN YOUR LANE!  MORE INK MEANS STRONGER MAGNETISM" },
            { "drawn_escort_snack", "FINAL ROUND: REACH THE SNACK ABOVE!" },
            { "drawn_escort_final_plan", "BUILD A PATH THROUGH THE SNACK ABOVE!\nCONNECT IT WITH YOUR BODY" },
            { "drawn_escort_tutorial", "THE FRIEND CAN WALK ON YOUR HEAD AND BODY LINES" },
            { "drawn_escort_launch", "FRIEND START" },
            { "drawn_escort_failed", "THE FRIEND FELL!" },
            { "drawn_escort_game_over", "GAME OVER" },
            { "redraw_unavailable_escort_run", "YOU CANNOT REDRAW WHILE THE FRIEND IS MOVING." },
            { "ricochet_title", "10-3  RICOCHET RELAY" },
            { "ricochet_round", "ROUND {0} / {1}" },
            { "ricochet_ammo", "AMMO {0}" },
            { "ricochet_instruction", "RICOCHET BULLETS OFF FRIENDS TO HIT THE TARGET" },
            { "ricochet_failed", "OUT OF AMMO! RESTARTING" },
            { "ricochet_clear", "5 ROUND CLEAR!" },
            { "stage_object_escort_spawner", "Friend Spawner" },
            { "stage_object_escort_goal", "Friend Goal" },
            { "stage_object_escort_player_only_floor", "Friend-Pass Floor" },
            { "stage_object_escort_head_bridge", "Head Bridge Example" },
            { "stage_object_seesaw", "Seesaw" },
            { "stage_object_turntable", "Turntable" },
            { "stage_object_cannon", "Cannon" },
            { "stage_object_catapult", "Catapult" },
            { "stage_object_spike", "Spike" },
            { "stage_object_fire", "Fire" },
            { "stage_object_water", "Water" },
            { "stage_object_poison", "Poison" },
            { "stage_object_laser", "Laser" },
            { "stage_object_falling_rock", "Falling Rock" },
            { "stage_object_press_machine", "Press Machine" },
            { "stage_object_electricity", "Electricity" },
            { "stage_object_saw", "Saw" },
            { "stage_object_black_hole", "Black Hole" },
            { "stage_object_gear", "Gear" },
            { "stage_object_big_gear", "Big Gear" },
            { "stage_object_rope_pulley", "Rope Pulley" },
            { "stage_object_slider", "Slider" },
            { "stage_object_rotating_bar", "Rotating Bar" },
            { "stage_object_pendulum", "Pendulum" },
            { "stage_object_keyhole", "Keyhole" },
            { "stage_object_stage_boundary", "Stage Boundary" },
            { "stage_object_clock", "Clock" },
            { "stage_object_counter", "Counter" },
            { "stage_object_traffic_light", "Traffic Light" }
        };

        public static event Action LanguageChanged;
        private static string currentLanguageCode = DefaultLanguageCode;

        // Kept for scene and code compatibility while language selection moves to stable locale codes.
        public static Language CurrentLanguage => string.Equals(currentLanguageCode, DefaultLanguageCode, StringComparison.OrdinalIgnoreCase)
            ? Language.Japanese
            : Language.English;
        public static string CurrentLanguageCode => currentLanguageCode;
        public static IReadOnlyList<LanguageDefinition> SupportedLanguages
        {
            get
            {
                LoadLanguageDefinitions();
                return languageDefinitions;
            }
        }
        public static LanguageDefinition CurrentLanguageDefinition
        {
            get
            {
                LoadLanguageDefinitions();
                return languageDefinitionsByCode.TryGetValue(currentLanguageCode, out LanguageDefinition definition)
                    ? definition
                    : languageDefinitionsByCode[DefaultLanguageCode];
            }
        }
        public static float CurrentUiTextScale => Mathf.Max(0.5f, CurrentLanguageDefinition.uiTextScale);
        public static string CurrentListSeparator => string.IsNullOrEmpty(CurrentLanguageDefinition.listSeparator)
            ? " / "
            : CurrentLanguageDefinition.listSeparator;
        public static bool CurrentLanguageIsRightToLeft => CurrentLanguageDefinition.rightToLeft;
        public static CultureInfo CurrentCulture
        {
            get
            {
                string cultureCode = CurrentLanguageDefinition.cultureCode;
                if (string.IsNullOrEmpty(cultureCode)) return CultureInfo.InvariantCulture;
                try
                {
                    return CultureInfo.GetCultureInfo(cultureCode);
                }
                catch (CultureNotFoundException)
                {
                    return CultureInfo.InvariantCulture;
                }
            }
        }

        public static Font LoadCurrentFont(Font fallback = null)
        {
            return LoadFontForLanguage(currentLanguageCode, fallback);
        }

        public static Font LoadFontForLanguage(string languageCode, Font fallback = null)
        {
            LanguageDefinition definition = GetLanguageDefinition(languageCode);
            if (definition == null) return fallback;
            string resourcePath = definition.fontResourcePath;
            Font resourceFont = string.IsNullOrEmpty(resourcePath)
                ? null
                : Resources.Load<Font>(resourcePath);
            if (resourceFont != null) return resourceFont;

            if (definition.systemFontNames != null && definition.systemFontNames.Length > 0)
            {
                if (!dynamicFonts.TryGetValue(definition.code, out Font dynamicFont) || dynamicFont == null)
                {
                    dynamicFont = Font.CreateDynamicFontFromOSFont(definition.systemFontNames, 32);
                    if (dynamicFont != null) dynamicFonts[definition.code] = dynamicFont;
                }
                if (dynamicFont != null) return dynamicFont;
            }
            return fallback;
        }

        private void Awake()
        {
            LoadExternalTables();
            string saved = NormalizeLegacyLanguageCode(PlayerPrefs.GetString("language", DefaultLanguageCode));
            SetLanguage(saved);
        }

        public static void SetLanguage(Language language)
        {
            SetLanguage(language == Language.Japanese ? DefaultLanguageCode : FallbackLanguageCode);
        }

        public static bool SetLanguage(string languageCode)
        {
            LoadExternalTables();
            string normalized = NormalizeLegacyLanguageCode(languageCode);
            if (!languageDefinitionsByCode.ContainsKey(normalized))
            {
                return false;
            }

            currentLanguageCode = normalized;
            PlayerPrefs.SetString("language", currentLanguageCode);
            LanguageChanged?.Invoke();
            return true;
        }

        public static bool IsCurrentLanguage(string languageCode)
        {
            return string.Equals(currentLanguageCode, languageCode, StringComparison.OrdinalIgnoreCase);
        }

        public static LanguageDefinition GetLanguageDefinition(string languageCode)
        {
            LoadLanguageDefinitions();
            string normalized = NormalizeLegacyLanguageCode(languageCode);
            return languageDefinitionsByCode.TryGetValue(normalized, out LanguageDefinition definition)
                ? definition
                : null;
        }

        public static string T(string key)
        {
            LoadExternalTables();
            if (TryResolveForLanguage(currentLanguageCode, key, out string value))
            {
                return value;
            }

            HashSet<string> visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { currentLanguageCode };
            string fallbackCode = CurrentLanguageDefinition.fallbackCode;
            while (!string.IsNullOrEmpty(fallbackCode) && visited.Add(fallbackCode))
            {
                if (TryResolveForLanguage(fallbackCode, key, out value))
                {
                    return value;
                }

                LanguageDefinition fallbackDefinition = GetLanguageDefinition(fallbackCode);
                fallbackCode = fallbackDefinition != null ? fallbackDefinition.fallbackCode : null;
            }

            if (visited.Add(FallbackLanguageCode) && TryResolveForLanguage(FallbackLanguageCode, key, out value))
            {
                return value;
            }

            return key;
        }

        private static bool TryResolveForLanguage(string languageCode, string key, out string value)
        {
            if (externalTables.TryGetValue(languageCode, out Dictionary<string, string> external)
                && external.TryGetValue(key, out value))
            {
                return true;
            }

            if (string.Equals(languageCode, DefaultLanguageCode, StringComparison.OrdinalIgnoreCase)
                && TryGetJapaneseOverride(key, out value))
            {
                return true;
            }

            Dictionary<string, string> generated = GetGeneratedTable(languageCode);
            if (generated != null && generated.TryGetValue(key, out value))
            {
                return true;
            }

            Dictionary<string, string> builtIn = GetBuiltInTable(languageCode);
            if (builtIn != null && builtIn.TryGetValue(key, out value))
            {
                return true;
            }

            value = null;
            return false;
        }

        private static Dictionary<string, string> GetGeneratedTable(string languageCode)
        {
            if (string.Equals(languageCode, DefaultLanguageCode, StringComparison.OrdinalIgnoreCase)) return GeneratedJapanese;
            if (string.Equals(languageCode, FallbackLanguageCode, StringComparison.OrdinalIgnoreCase)) return GeneratedEnglish;
            return null;
        }

        private static Dictionary<string, string> GetBuiltInTable(string languageCode)
        {
            if (string.Equals(languageCode, DefaultLanguageCode, StringComparison.OrdinalIgnoreCase)) return Japanese;
            if (string.Equals(languageCode, FallbackLanguageCode, StringComparison.OrdinalIgnoreCase)) return English;
            return null;
        }

        private static void LoadLanguageDefinitions()
        {
            if (loadedLanguageDefinitions) return;
            loadedLanguageDefinitions = true;

            TextAsset asset = Resources.Load<TextAsset>("Localization/languages");
            LanguageDefinitionFile file = asset != null
                ? JsonUtility.FromJson<LanguageDefinitionFile>(asset.text)
                : null;
            if (file?.entries != null)
            {
                for (int i = 0; i < file.entries.Length; i++)
                {
                    RegisterLanguageDefinition(file.entries[i]);
                }
            }

            if (!languageDefinitionsByCode.ContainsKey(DefaultLanguageCode))
            {
                RegisterLanguageDefinition(CreateDefaultDefinition(DefaultLanguageCode, "\u65e5\u672c\u8a9e", "\u30fb", 1f));
            }
            if (!languageDefinitionsByCode.ContainsKey(FallbackLanguageCode))
            {
                RegisterLanguageDefinition(CreateDefaultDefinition(FallbackLanguageCode, "EN", " / ", 0.75f));
            }
        }

        private static void RegisterLanguageDefinition(LanguageDefinition definition)
        {
            if (definition == null || string.IsNullOrWhiteSpace(definition.code)) return;
            definition.code = definition.code.Trim().ToLowerInvariant();
            if (languageDefinitionsByCode.ContainsKey(definition.code)) return;
            if (string.IsNullOrEmpty(definition.nativeName)) definition.nativeName = definition.code.ToUpperInvariant();
            if (definition.uiTextScale <= 0f) definition.uiTextScale = 1f;
            if (definition.resourcePaths == null) definition.resourcePaths = Array.Empty<string>();
            if (definition.systemFontNames == null) definition.systemFontNames = Array.Empty<string>();
            languageDefinitions.Add(definition);
            languageDefinitionsByCode.Add(definition.code, definition);
        }

        private static LanguageDefinition CreateDefaultDefinition(string code, string nativeName, string separator, float textScale)
        {
            return new LanguageDefinition
            {
                code = code,
                nativeName = nativeName,
                fallbackCode = code == FallbackLanguageCode ? string.Empty : FallbackLanguageCode,
                resourcePaths = code == DefaultLanguageCode
                    ? new[] { "Localization/ja", "Localization/stage_decorations_ja" }
                    : new[] { "Localization/en", "Localization/stage_decorations_en" },
                uiTextScale = textScale,
                listSeparator = separator,
                cultureCode = code == DefaultLanguageCode ? "ja-JP" : "en-US"
            };
        }

        private static string NormalizeLegacyLanguageCode(string languageCode)
        {
            if (string.IsNullOrWhiteSpace(languageCode)) return DefaultLanguageCode;
            if (string.Equals(languageCode, Language.Japanese.ToString(), StringComparison.OrdinalIgnoreCase)) return DefaultLanguageCode;
            if (string.Equals(languageCode, Language.English.ToString(), StringComparison.OrdinalIgnoreCase)) return FallbackLanguageCode;
            return languageCode.Trim().ToLowerInvariant();
        }

        private static void LoadExternalTables()
        {
            if (loadedExternalTables)
            {
                return;
            }

            loadedExternalTables = true;
            LoadLanguageDefinitions();
            for (int i = 0; i < languageDefinitions.Count; i++)
            {
                LanguageDefinition definition = languageDefinitions[i];
                Dictionary<string, string> table = new Dictionary<string, string>();
                externalTables[definition.code] = table;
                for (int pathIndex = 0; pathIndex < definition.resourcePaths.Length; pathIndex++)
                {
                    LoadExternalTable(definition.resourcePaths[pathIndex], table);
                }
            }
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
                case "ink_personal_status":
                    value = "\u500b\u4eba  {0:0.#} / {1:0}";
                    return true;
                case "ink_team_status":
                    value = "\u30c1\u30fc\u30e0  {0:0.#} / {1:0}  [{2}\u4eba]";
                    return true;
                case "msg_personal_ink_over":
                    value = "\u78ba\u5b9a\u3067\u304d\u307e\u305b\u3093\uff1a\u500b\u4eba\u30a4\u30f3\u30af\u304c {0:0.#}/{1:0} \u3067\u3059\u3002{2:0} \u6e1b\u3089\u3057\u3066\u304f\u3060\u3055\u3044\u3002";
                    return true;
                case "msg_team_ink_over":
                    value = "\u78ba\u5b9a\u3067\u304d\u307e\u305b\u3093\uff1a\u5168\u4f53\u30a4\u30f3\u30af\u304c {0:0.#}/{1:0} \u3067\u3059\u3002{2:0} \u6e1b\u3089\u3057\u3066\u304f\u3060\u3055\u3044\u3002";
                    return true;
                case "stage_clear_title":
                    value = "\u30b9\u30c6\u30fc\u30b8\u30af\u30ea\u30a2\uff01";
                    return true;
                case "stage_clear_body":
                    value = "Stage {0} \u30af\u30ea\u30a2\uff01";
                    return true;
                case "stage_clear_body_generic":
                    value = "\u30af\u30ea\u30a2\uff01";
                    return true;
                case "stage_clear_next":
                    value = "\u6b21\u3078 {0}";
                    return true;
                case "stage_clear_back":
                    value = "\u30b9\u30c6\u30fc\u30b8\u9078\u629e\u3078";
                    return true;
                case "stage_clear_all_done":
                    value = "\u5168\u30b9\u30c6\u30fc\u30b8\u30af\u30ea\u30a2\uff01";
                    return true;
                default:
                    value = null;
                    return false;
            }
        }

        public static string Format(string key, params object[] args)
        {
            return string.Format(CurrentCulture, T(key), args);
        }

        public static IReadOnlyList<string> GetMissingTranslationKeys(string languageCode)
        {
            LoadExternalTables();
            string normalized = NormalizeLegacyLanguageCode(languageCode);
            HashSet<string> referenceKeys = new HashSet<string>(English.Keys);
            referenceKeys.UnionWith(GeneratedEnglish.Keys);
            if (externalTables.TryGetValue(FallbackLanguageCode, out Dictionary<string, string> fallbackExternal))
            {
                referenceKeys.UnionWith(fallbackExternal.Keys);
            }

            List<string> missing = new List<string>();
            foreach (string key in referenceKeys)
            {
                if (!TryResolveForLanguage(normalized, key, out _)) missing.Add(key);
            }
            missing.Sort(StringComparer.Ordinal);
            return missing;
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
