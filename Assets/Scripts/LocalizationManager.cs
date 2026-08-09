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
            { "ability_bird_glide", "羽 {0:0.0} INK / 滑空速度 {1:0.00}" },
            { "ability_human_status_combined", "人間の能力\n腕 {0:0.0} INK　腕力 ×{1:0.00}\n足 {2:0.0} INK　ジャンプ ×{3:0.00}" },
            { "ability_cat_status", "猫の能力\n足 {0:0.0} INK\n移動速度 ×{1:0.00}" },
            { "ability_bird_status", "鳥の能力\n羽 {0:0.0} / 350 INK\n滑空速度 {1:0.00}" },
            { "ability_turtle_status", "カメの能力\nSPACE長押し：甲羅で無敵\nF長押し：向いている側へ90°回転" },
            { "ability_slime_status", "スライムの能力\n体 {0:0.0} / 350 INK\n歩く速さ ×{1:0.00}　ジャンプ ×{2:0.00}\n粘着力 {3:0}%" },
            { "ability_card_human", "人間 － 腕力＋ジャンプ" },
            { "ability_card_cat", "猫 － ダッシュ" },
            { "ability_card_bird", "鳥 － 滑空" },
            { "ability_card_turtle", "カメ － 甲羅ガード" },
            { "ability_card_slime", "スライム － INK特性" },
            { "ability_effect_human_combined", "腕力 ×{0:0.00}　ジャンプ ×{1:0.00}" },
            { "ability_effect_cat", "走る速さ ×{0:0.00}" },
            { "ability_effect_bird", "ふんわり度 {0:0}%" },
            { "ability_effect_turtle", "SPACE 甲羅で無敵　／　F 90°回転" },
            { "ability_effect_slime", "歩く ×{0:0.00}　ジャンプ ×{1:0.00}\n粘着 {2:0}%" },
            { "ability_ink_human_combined", "腕 {0:0.0}/280　足 {1:0.0}/80 INK" },
            { "ability_ink_cat", "足 {0:0.0} / 120 INK" },
            { "ability_ink_bird", "羽 {0:0.0} / 350 INK" },
            { "ability_ink_turtle", "ボタンを押している間だけ発動" },
            { "ability_turtle_badge", "甲羅 READY" },
            { "ability_turtle_hint", "SPACEとFは押している間だけ発動！" },
            { "ability_ink_slime", "体 {0:0.0} / 350 INK" },
            { "ability_rank", "RANK {0}" },
            { "ability_gauge_low", "よわい" },
            { "ability_gauge_high", "つよい" },
            { "ability_slime_gauge_low", "軽い：粘着↑" },
            { "ability_slime_gauge_high", "重い：速さ・ジャンプ↑" },
            { "ability_slime_badge", "INK量 {0:0}%" },
            { "ability_slime_hint", "F長押し：仲間に吸着！ 壁ジャンプOK／少INK＝粘着・多INK＝機動力" },
            { "ability_growth_hint", "描くほど能力アップ！" },
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
            { "ability_cat_status", "CAT ABILITY\nLegs {0:0.0} INK\nMove speed ×{1:0.00}" },
            { "ability_bird_status", "BIRD ABILITY\nWings {0:0.0} / 350 INK\nGlide speed {1:0.00}" },
            { "ability_turtle_status", "TURTLE ABILITY\nHold SPACE: invincible shell\nHold F: turn 90° toward facing direction" },
            { "ability_slime_status", "SLIME ABILITY\nBody {0:0.0} / 350 INK\nMove ×{1:0.00}  Jump ×{2:0.00}\nGrip {3:0}%" },
            { "ability_card_human", "HUMAN - POWER + JUMP" },
            { "ability_card_cat", "CAT - DASH" },
            { "ability_card_bird", "BIRD - GLIDE" },
            { "ability_card_turtle", "TURTLE - SHELL GUARD" },
            { "ability_card_slime", "SLIME - INK TRADE-OFF" },
            { "ability_effect_human_combined", "Power ×{0:0.00}  Jump ×{1:0.00}" },
            { "ability_effect_cat", "Run speed ×{0:0.00}" },
            { "ability_effect_bird", "Float power {0:0}%" },
            { "ability_effect_turtle", "SPACE: SHELL  /  F: 90° TURN" },
            { "ability_effect_slime", "Move ×{0:0.00}  Jump ×{1:0.00}\nGrip {2:0}%" },
            { "ability_ink_human_combined", "Arms {0:0.0}/280  Legs {1:0.0}/80 INK" },
            { "ability_ink_cat", "Legs {0:0.0} / 120 INK" },
            { "ability_ink_bird", "Wings {0:0.0} / 350 INK" },
            { "ability_ink_turtle", "Active only while the button is held" },
            { "ability_turtle_badge", "SHELL READY" },
            { "ability_turtle_hint", "SPACE and F work while held!" },
            { "ability_ink_slime", "Body {0:0.0} / 350 INK" },
            { "ability_rank", "RANK {0}" },
            { "ability_gauge_low", "LOW" },
            { "ability_gauge_high", "HIGH" },
            { "ability_slime_gauge_low", "LIGHT: GRIP UP" },
            { "ability_slime_gauge_high", "HEAVY: SPEED + JUMP UP" },
            { "ability_slime_badge", "INK SIZE {0:0}%" },
            { "ability_slime_hint", "HOLD F: Stick to a friend! Wall jump / Less INK = grip, more = mobility" },
            { "ability_growth_hint", "Draw more to power up!" },
            { "label_high_platform", "1 High Platform" },
            { "label_heavy_switch", "2 Heavy Switch" },
            { "label_far_lever", "3 Far Lever" },
            { "label_narrow_hole", "4 Narrow Hole" },
            { "label_ball_hit", "5 Ball Hit" }
        };

        private static readonly Dictionary<string, string> GeneratedJapanese = new Dictionary<string, string>
        {
            { "title_single", "SINGLE" },
            { "title_multi", "MULTI" },
            { "title_draw", "DRAW" },
            { "title_option", "OPTION" },
            { "title_exit", "EXIT" },
            { "menu_continue", "続ける" },
            { "menu_to_title", "タイトルへ" },
            { "language_settings", "言語設定" },
            { "multi_play", "MULTI PLAY" },
            { "multi_random_button", "ランダムマッチ" },
            { "multi_room_button", "プライベート" },
            { "multi_random_match", "ランダムマッチ" },
            { "multi_searching_players", "プレイヤーを探しています" },
            { "multi_searching_slot", "募集中" },
            { "multi_random_status_default", "プレイヤーを探しています...\n\n●□□□\n1 / 4 PLAYERS\n\nP1  あなた    READY?\n✏  募集中\n✏  募集中\n✏  募集中" },
            { "multi_room_title", "ROOM" },
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
            { "draw_species_locked", "\u3053\u306e\u30b9\u30c6\u30fc\u30b8\u3067\u306f{0}\u306f\u4f7f\u3048\u307e\u305b\u3093" },
            { "species_human", "\u4eba" },
            { "species_cat", "\u732b" },
            { "species_bird", "\u9ce5" },
            { "species_turtle", "\u30ab\u30e1" },
            { "species_slime", "\u30b9\u30e9\u30a4\u30e0" },
            { "stage_editor_objects_tab", "Objects" },
            { "stage_editor_links_tab", "Links" },
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
            { "ink_team_formula", "\u5168\u4f53 {0}\u4eba\u00d7{1:0}" },
            { "draw_reset_all", "\u5168\u30ea\u30bb\u30c3\u30c8" },
            { "draw_reset_confirm_title", "\u5168\u30ea\u30bb\u30c3\u30c8\u3057\u307e\u3059\u304b\uff1f" },
            { "draw_reset_confirm_message", "\u3059\u3079\u3066\u306e\u30d1\u30fc\u30c4\u3092\u6d88\u3057\u3066\u3001\u6700\u521d\u306e\u666e\u901a\u306e\u30ad\u30e3\u30e9\u30af\u30bf\u30fc\u306b\u623b\u3057\u307e\u3059\u3002" },
            { "draw_reset_confirm_yes", "\u30ea\u30bb\u30c3\u30c8\u3059\u308b" },
            { "draw_reset_confirm_no", "\u30ad\u30e3\u30f3\u30bb\u30eb" },
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
            { "stage_editor_background_color", "背景色" },
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
            { "survival_get_ready", "準備して！" },
            { "survival_start", "スタート！" },
            { "survival_watch_floor", "光る床をよく見て！" },
            { "survival_remaining", "生き残れ！  残り時間" },
            { "survival_safe_countdown", "安全床 {0}か所 ／ 消えるまで {1:0.0}" },
            { "survival_floor_dropped", "光った床から落ちるな！" },
            { "survival_all_dead", "全員脱落…" },
            { "survival_retrying", "まもなくリスタート" },
            { "survival_clear_title", "生存者あり！" },
            { "survival_clear_sub", "サバイバル成功" },
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
            { "title_single", "SINGLE" },
            { "title_multi", "MULTI" },
            { "title_draw", "DRAW" },
            { "title_option", "OPTION" },
            { "title_exit", "EXIT" },
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
            { "draw_species_locked", "{0} is not available in this stage." },
            { "species_human", "Human" },
            { "species_cat", "Cat" },
            { "species_bird", "Bird" },
            { "species_turtle", "Turtle" },
            { "species_slime", "Slime" },
            { "stage_editor_objects_tab", "Objects" },
            { "stage_editor_links_tab", "Links" },
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
            { "ink_team_formula", "TEAM {0}\u00d7{1:0}" },
            { "draw_reset_all", "RESET ALL" },
            { "draw_reset_confirm_title", "RESET EVERYTHING?" },
            { "draw_reset_confirm_message", "Erase every body part and restore the original character." },
            { "draw_reset_confirm_yes", "RESET" },
            { "draw_reset_confirm_no", "CANCEL" },
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
            { "stage_editor_background_color", "Background" },
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
            { "survival_get_ready", "GET READY!" },
            { "survival_start", "START!" },
            { "survival_watch_floor", "WATCH THE GLOWING FLOOR!" },
            { "survival_remaining", "SURVIVE!  TIME LEFT" },
            { "survival_safe_countdown", "SAFE FLOORS {0} / DROP IN {1:0.0}" },
            { "survival_floor_dropped", "STAY ON THE SAFE FLOOR!" },
            { "survival_all_dead", "ALL PLAYERS OUT" },
            { "survival_retrying", "RESTARTING SOON" },
            { "survival_clear_title", "SURVIVOR FOUND!" },
            { "survival_clear_sub", "SURVIVAL CLEARED" },
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

            if (TryGetGeneratedLocalization(key, out string generated))
            {
                return generated;
            }

            Dictionary<string, string> table = CurrentLanguage == Language.Japanese ? Japanese : English;
            if (table.TryGetValue(key, out string value))
            {
                return value;
            }

            return English.TryGetValue(key, out string fallback) ? fallback : key;
        }

        private static bool TryGetGeneratedLocalization(string key, out string value)
        {
            Dictionary<string, string> table = CurrentLanguage == Language.Japanese ? GeneratedJapanese : GeneratedEnglish;
            return table.TryGetValue(key, out value);
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
            LoadExternalTable("Localization/stage_decorations_ja", ExternalJapanese);
            LoadExternalTable("Localization/stage_decorations_en", ExternalEnglish);
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
