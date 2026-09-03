# SE使用一覧

更新日: 2026-09-01

ゲーム内のSEは `GameSfx` を経由し、OPTIONのSE音量設定と `SfxCatalog` の音量・ピッチ・連打制限が適用される。
音源欄は `Assets/Resources/` からの相対パス。`生成音` は実行時に波形を生成するため、音声ファイルを使用しない。

## UI

| 行動・タイミング | SfxId | 音源 | 主な実装 |
|---|---|---|---|
| キーボード／ゲームパッドで選択項目を移動 | `UiCursorMove` | `Audio/SFX/UI/ui_cursor_move.ogg` | `DoodleUiDirector.cs` |
| ボタンへカーソルを合わせる | `UiButtonHover` | `Audio/SFX/UI/ui_button_hover.ogg` | `DoodleUiDirector.cs` |
| 通常ボタンを押す | `UiButtonPress` | `Audio/SFX/UI/ui_button_press.ogg` | `DoodleUiDirector.cs` |
| 戻る・閉じる・キャンセル・退出ボタン、ESCメニューを閉じる | `UiButtonBack` | `Audio/SFX/UI/ui_button_back.ogg` | `DoodleUiDirector.cs`, `UIManager.cs` |
| タブ・ページ・キャラ／部位タブの切替、TABメニューを開閉 | `UiTabChange` | `Audio/SFX/UI/ui_tab_change.ogg` | `DoodleUiDirector.cs`, `UIManager.cs` |
| ドロップダウンを開く | `UiDropdownOpen` | `Audio/SFX/UI/ui_dropdown_open.ogg` | `DoodleUiDirector.cs` |
| ドロップダウンの項目を決定 | `UiDropdownSelect` | `Audio/SFX/UI/ui_dropdown_select.ogg` | `DoodleUiDirector.cs` |
| BGM／SEスライダーを動かす | `UiSliderTick` | `Audio/SFX/UI/ui_slider_tick.ogg` | `OptionSettingsController.cs` |
| トグルをON、準備状態や一部ギミックを有効化 | `UiToggleOn` | `Audio/SFX/UI/ui_toggle_on.ogg` | `DoodleUiDirector.cs`, 各ステージ制御 |
| トグルをOFF、拒否・変更不可の操作 | `UiToggleOff` | `Audio/SFX/UI/ui_toggle_off.ogg` | `DoodleUiDirector.cs`, `DrawManager.cs` |
| エモートを表示、味方が短い台詞を発する | `EmotePop` | `Audio/SFX/UI/emote_pop.ogg` | `PlayerEmoteController.cs`, 各護衛ステージ |

## お絵描き

| 行動・タイミング | SfxId | 音源 | 主な実装 |
|---|---|---|---|
| ペンで描き始める | `DrawPenStart` | **生成音** | `DrawFeedbackController.cs` |
| ペンを動かして描いている間 | `DrawPenLoop` | **生成音** | `DrawFeedbackController.cs` |
| ペンを離して描画を終える | `DrawPenEnd` | **生成音** | `DrawFeedbackController.cs` |
| 消しゴムを動かしている間 | `DrawEraserLoop` | **生成音** | `DrawFeedbackController.cs` |
| 消去を確定 | `DrawEraseComplete` | **生成音** | `DrawFeedbackController.cs` |
| 頭・胴体・腕・脚などの部位を変更 | `DrawPartChange` | **生成音** | `DrawManager.cs` |
| 人・猫・鳥・カメ・スライムを変更 | `DrawSpeciesChange` | **生成音** | `DrawManager.cs` |
| INK上限へ近づいた警告 | `DrawInkWarning` | **生成音** | `DrawManager.cs` |
| 個人／チームINK上限超過、描画不可 | `DrawInkOver` | **生成音** | `DrawManager.cs` |
| 完成、プリセット確定、描画内容を受理 | `DrawConfirm` | **生成音** | `DrawManager.cs`, `DrawScreenVisualPolisher.cs` |

## ステージ編集

| 行動・タイミング | SfxId | 音源 | 主な実装 |
|---|---|---|---|
| オブジェクトを新規配置 | `EditorObjectPlace` | `Audio/SFX/Editor/editor_object_place.ogg` | `RuntimeStageEditor.Editing.cs` |
| オブジェクトを選択 | `EditorObjectSelect` | `Audio/SFX/Editor/editor_object_select.ogg` | `RuntimeStageEditor.Editing.cs` |
| 選択物を移動中 | `EditorObjectMove` | `Audio/SFX/Editor/editor_object_move.ogg` | `RuntimeStageEditor.Editing.cs` |
| 移動物を置く、落下物が着地 | `EditorObjectDrop` | `Audio/SFX/Editor/editor_object_drop.ogg` | `RuntimeStageEditor.Editing.cs`, 一部ステージ |
| オブジェクトを拡大・縮小 | `EditorObjectResize` | `Audio/SFX/Editor/editor_object_resize.ogg` | `RuntimeStageEditor.Editing.cs` |
| オブジェクトを回転 | `EditorObjectRotate` | `Audio/SFX/Editor/editor_object_rotate.ogg` | `RuntimeStageEditor.Editing.cs` |
| オブジェクトをコピー | `EditorObjectCopy` | `Audio/SFX/Editor/editor_object_copy.ogg` | `RuntimeStageEditor.Editing.cs` |
| オブジェクトを削除 | `EditorObjectDelete` | `Audio/SFX/Editor/editor_object_delete.ogg` | `RuntimeStageEditor.Editing.cs` |
| 元に戻す | `EditorUndo` | `Audio/SFX/Editor/editor_undo.ogg` | `RuntimeStageEditor.History.cs` |
| やり直す | `EditorRedo` | `Audio/SFX/Editor/editor_redo.ogg` | `RuntimeStageEditor.History.cs` |

## プレイヤー共通

| 行動・タイミング | SfxId | 音源 | 主な実装 |
|---|---|---|---|
| 紙の床を歩く | `PlayerFootstepPaper` | `Audio/SFX/Player/player_footstep_paper.ogg` | `PlayerController2D.cs` |
| 軽く着地 | `PlayerLandSoft` | `Audio/SFX/Player/player_land_soft.ogg` | `PlayerController2D.cs` |
| 高所から強く着地 | `PlayerLandHard` | `Audio/SFX/Player/player_land_hard.ogg` | `PlayerController2D.cs` |
| 通常ジャンプ | `PlayerJump` | `Audio/SFX/Player/player_jump.ogg` | `PlayerController2D.cs` |
| 二段ジャンプ | `PlayerDoubleJump` | `Audio/SFX/Player/player_double_jump.ogg` | **予約済み・現在は二段ジャンプ機能なし** |
| ダメージを受ける | `PlayerHit` | `Audio/SFX/Player/player_hit.ogg` | `StageManager.cs`, 各ステージ制御 |
| やられる・消滅する | `PlayerDeath` | **生成音** | `StageManager.cs`, 各ステージ制御 |
| 復活する | `PlayerRespawn` | `Audio/SFX/Player/player_respawn.ogg` | `StageManager.cs` |
| 他プレイヤーや物体を押す | `PlayerPush` | **無音** | IDは維持するが再生しない |
| プレイヤー同士が積み重なる | `PlayerStacked` | `Audio/SFX/Player/player_stacked.ogg` | `PlayerController2D.cs` |

オンラインでは、ジャンプ・着地・死亡・復活などの受信イベントでも同じSEを再生する。

## キャラクター固有

| 行動・タイミング | SfxId | 音源 | 主な実装 |
|---|---|---|---|
| 人間が物・味方を持ち上げる | `HumanLift` | `Audio/SFX/Species/human_lift.ogg` | `PlayerCarryController.cs` |
| 人間が物・味方を投げる | `HumanThrow` | `Audio/SFX/Species/human_throw.ogg` | `PlayerCarryController.cs` |
| 猫が走る | `CatRunLoop` | `Audio/SFX/Species/cat_run_loop.ogg` | `PlayerController2D.cs` |
| 猫がジャンプ | `CatJump` | `Audio/SFX/Species/cat_jump.ogg` | `PlayerController2D.cs` |
| 猫のひっかきが物へ付く | `CatClawAttach` | `Audio/SFX/Species/cat_claw_attach.ogg` | `PlayerCarryController.cs` |
| 猫がひっかきで持った物を離す | `CatClawRelease` | `Audio/SFX/Species/cat_claw_release.ogg` | `PlayerCarryController.cs` |
| 鳥が羽ばたく | `BirdFlap` | `Audio/SFX/Species/bird_flap.ogg` | `PlayerController2D.cs`, `PlayerCarryController.cs` |
| 鳥が滑空する | `BirdGlideLoop` | `Audio/SFX/Species/bird_glide_loop.ogg` | `PlayerController2D.cs` |
| カメがジャンプ | `TurtleJump` | `Audio/SFX/Species/turtle_jump.ogg` | `PlayerController2D.cs` |
| カメが着地 | `TurtleLand` | `Audio/SFX/Species/turtle_land.ogg` | `PlayerController2D.cs` |
| カメが甲羅へ隠れる | `TurtleShellEnter` | **生成音** | `PlayerController2D.cs` |
| カメが甲羅から出る | `TurtleShellExit` | `Audio/SFX/Species/turtle_shell_exit.ogg` | `PlayerController2D.cs` |
| スライムが壁・味方へ吸着 | `SlimeStick` | `Audio/SFX/Species/slime_stick.ogg` | `PlayerController2D.cs`, `PlayerCarryController.cs` |
| スライムが吸着を解除 | `SlimeRelease` | `Audio/SFX/Species/slime_release.ogg` | `PlayerController2D.cs`, `PlayerCarryController.cs` |

## 爆発物・床・発射ギミック

| 行動・タイミング | SfxId | 音源 | 主な実装 |
|---|---|---|---|
| 爆弾の導火線が点火 | `BombFuseStart` | `Audio/SFX/Gimmick/bomb_fuse_start.ogg` | `StageBomb.cs`, ボス制御 |
| 爆弾の爆発前カウント | `BombTick` | `Audio/SFX/Gimmick/bomb_tick.ogg` | `StageBomb.cs`, `StageTowerDefenseController.cs` |
| 爆弾・バズーカ弾が爆発 | `BombExplosion` | **生成音** | `StageBomb.cs`, `StageBazooka.cs`, 各ボス制御 |
| 爆発で破壊可能壁を壊す | `BombWallBreak` | `Audio/SFX/Gimmick/bomb_wall_break.ogg` | `StageBomb.cs`, 反射ステージ制御 |
| ダイナマイトへ点火 | `DynamiteFuseStart` | `Audio/SFX/Gimmick/dynamite_fuse_start.ogg` | `StageDynamite.cs` |
| ダイナマイトの爆発前カウント | `DynamiteTick` | `Audio/SFX/Gimmick/dynamite_tick.ogg` | `StageDynamite.cs` |
| ダイナマイトが爆発 | `DynamiteExplosion` | **生成音** | `StageDynamite.cs` |
| 崩れる床が反応・警告 | `CrumblingFloorWarning` | **無音** | IDは維持するが再生しない |
| 崩れる床が崩壊 | `CrumblingFloorCollapse` | `Audio/SFX/Gimmick/crumbling_floor_collapse.ogg` | `StageCrumblingFloor.cs`, 各ステージ制御 |
| ビームを発射 | `BeamFire` | `Audio/SFX/Gimmick/beam_fire.ogg` | `StageBeamEmitter.cs`, ボス制御 |
| 大砲・バズーカ・落下物製造機などが発射 | `CannonFire` | `Audio/SFX/Gimmick/cannon_fire.ogg` | 各発射ギミック・ステージ制御 |
| ミサイルを発射 | `MissileLaunch` | `Audio/SFX/Gimmick/missile_launch.ogg` | `StageMissileLauncher.cs`, 各ボス制御 |
| ミサイルが着弾・爆発 | `MissileImpact` | `Audio/SFX/Gimmick/missile_impact.ogg` | `StageMissileLauncher.cs`, 各ボス制御 |
| 落下柱が来る直前の警告 | `PillarWarning` | `Audio/SFX/Gimmick/pillar_warning.ogg` | `StagePillarSurvivalController.cs` |
| 落下柱が地面へ衝突 | `PillarImpact` | `Audio/SFX/Gimmick/pillar_impact.ogg` | `StagePillarSurvivalController.cs` |

## スイッチ・移動・アイテム

| 行動・タイミング | SfxId | 音源 | 主な実装 |
|---|---|---|---|
| ボタン・レバー・重量スイッチを押す | `SwitchPress` | `Audio/SFX/Gimmick/switch_press.ogg` | `LeverSwitch.cs`, `WeightedSwitch.cs`, 各リンク制御 |
| 扉・ゲートが開く | `DoorOpen` | `Audio/SFX/Gimmick/door_open.ogg` | `MovingGate.cs` |
| 扉・ゲートが閉じる | `DoorClose` | `Audio/SFX/Gimmick/door_close.ogg` | `MovingGate.cs` |
| ジャンプ台から発射 | `JumpPadLaunch` | `Audio/SFX/Gimmick/jump_pad_launch.ogg` | `JumpPad.cs` |
| スピードブーストへ入る | `SpeedBoost` | `Audio/SFX/Gimmick/speed_boost.ogg` | `StageSpeedBoostRing.cs` |
| 鍵で鍵穴を解除 | `KeyUnlock` | `Audio/SFX/Gimmick/key_unlock.ogg` | `StageGimmickLinkController.cs` |
| 電気回路を1本接続 | `CircuitConnect` | `Audio/SFX/Gimmick/circuit_connect.ogg` | `StageHumanCircuitController.cs` |
| 電気回路をすべて完成 | `CircuitComplete` | `Audio/SFX/Gimmick/circuit_complete.ogg` | `StageHumanCircuitController.cs` |
| 箱が床や物へ強く当たる | `CrateImpact` | `Audio/SFX/Gimmick/crate_impact.ogg` | `StageValueCoinChallengeController.cs` |
| 箱が壊れる | `CrateBreak` | `Audio/SFX/Gimmick/crate_break.ogg` | `StageValueCoinChallengeController.cs` |
| コインを取得 | `CoinCollect` | `Audio/SFX/Gameplay/coin_collect.ogg` | `StageManager.cs`, コインステージ制御 |
| 魚・粒などの収集物を取得 | `CollectibleCollect` | `Audio/SFX/Gameplay/collectible_collect.ogg` | `StageManager.cs` |

## 銃・敵・ボス

| 行動・タイミング | SfxId | 音源 | 主な実装 |
|---|---|---|---|
| 銃を発射 | `GunShot` | `Audio/SFX/Combat/gun_shot.ogg` | `StageGunCombat.cs` |
| 銃弾・ボールなどが壁で反射 | `Ricochet` | `Audio/SFX/Combat/ricochet.ogg` | 各反射ステージ制御 |
| 敵が攻撃前に力を溜める | `EnemyCharge` | `Audio/SFX/Enemy/enemy_charge.ogg` | `StageEnemyCharacter.cs` |
| 敵が弾を発射 | `EnemyShoot` | `Audio/SFX/Enemy/enemy_shoot.ogg` | `StageEnemyCharacter.cs`, ボス制御 |
| 敵がジャンプ | `EnemyJump` | `Audio/SFX/Enemy/enemy_jump.ogg` | `StageEnemyCharacter.cs`, ボス制御 |
| 敵を倒す | `EnemyDefeat` | **生成音** | 各戦闘ステージ制御 |
| 敵・甲羅・ボールが跳ね返る | `EnemyShellBounce` | `Audio/SFX/Enemy/enemy_shell_bounce.ogg` | 各戦闘・防衛ステージ制御 |
| ボスが突進を溜める | `BossCharge` | `Audio/SFX/Combat/boss_charge.ogg` | `StageBossBattleController.cs` |
| ボス攻撃の予兆 | `BossAttackWarning` | `Audio/SFX/Combat/boss_attack_warning.ogg` | 各ボス制御 |
| ボスが突進 | `BossDash` | `Audio/SFX/Combat/boss_dash.ogg` | 各ボス制御 |
| ボスがビームをチャージ | `BossBeamCharge` | `Audio/SFX/Combat/boss_beam_charge.ogg` | 各ボス制御 |
| ボスが吸引攻撃 | `BossSuction` | `Audio/SFX/Combat/boss_suction.ogg` | `StageFlyingPlatformBossController.cs` |

## ステージ進行・結果

| 行動・タイミング | SfxId | 音源 | 主な実装 |
|---|---|---|---|
| 開始カウントダウンの3・2・1 | `StageCountdownTick` | `Audio/SFX/Gameplay/stage_countdown_tick.ogg` | `StageManager.cs`, 各カウントダウンステージ |
| カウントダウン終了・ゲーム開始 | `StageCountdownGo` | `Audio/SFX/Gameplay/stage_countdown_go.ogg` | `StageManager.cs`, 待機室・各ステージ制御 |
| ゴールへ到達 | `GoalReached` | **生成音** | `Goal.cs` |
| ステージクリアが確定 | `StageClear` | **生成音** | `StageManager.cs` |
| ステージ失敗・GAME OVER | `StageFailed` | `Audio/SFX/Gameplay/stage_failed.ogg` | `StageManager.cs` |
| クリア画面のスタンプが紙へ当たる | `ClearStampImpact` | **生成音** | `UIManager.cs` |
| スタンプ後の祝福チャイム | `ClearCelebrationChime` | **生成音** | `UIManager.cs` |

## 補足

- `PlayAt` を使う爆発・ミサイル・敵攻撃などは、発生位置を持つ空間音として再生する。
- UI、カウントダウン、ステージ結果は画面全体へ聞かせる非空間音として再生する。
- 同一SEの短時間連打は `SfxCatalog` の `Cooldown` で抑制する。
- `StageClear` はクリア確定時、`ClearStampImpact` と `ClearCelebrationChime` は結果画面のアニメーションに合わせて時間差で再生する。
