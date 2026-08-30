# 各ステージの小部屋・モニター表示一覧

## 調査基準

- 対象: `1-1` ～ `15-3` の全45ステージ
- 「小部屋あり」は、`StageManager.RequiresChallengeReadyRoom()` によりゲーム開始前の待機小部屋が生成されるステージを指す。
- ゲーム説明モニターは次の2段表示。
  - 上段: ステージごとのゲーム説明（下表の「モニター表示」）
  - 下段: `準備 {準備済み人数} / {参加人数}`
  - 英語設定時の下段: `READY {準備済み人数} / {参加人数}`
- 全員がボタンを押すと小部屋は終了するため、小部屋のモニター上にカウントダウンは表示されない。
- `10-3` までの対象ステージでは、その右隣に推奨キャラモニターも並ぶ（詳細は後述）。
- `11-1` 以降は使用キャラ固定のため、推奨キャラモニターは表示しない。
- モニター文字は言語ごとの長さに合わせて最大2行へ自動折り返しし、安全余白内へ収まる文字サイズに自動調整する。
- 画面には薄い文字発光、走査線、ガラス反射、色むらを重ね、モニターに映っている質感を付ける。
- 「なし」のステージには、この開始前待機小部屋とそのモニターは生成されない。

## 一覧

| ステージ | 小部屋 | モニター表示（日本語） | Monitor text (English) |
|---|---:|---|---|
| 1-1 | なし | — | — |
| 1-2 | なし | — | — |
| 1-3 | なし | — | — |
| 2-1 | なし | — | — |
| 2-2 | あり | 制限時間内に魚を集めよう | COLLECT THE FISH BEFORE TIME RUNS OUT |
| 2-3 | なし | — | — |
| 3-1 | なし | — | — |
| 3-2 | なし | — | — |
| 3-3 | なし | — | — |
| 4-1 | なし | — | — |
| 4-2 | なし | — | — |
| 4-3 | あり | クレヨンキングを倒そう | DEFEAT THE CRAYON KING |
| 5-1 | なし | — | — |
| 5-2 | なし | — | — |
| 5-3 | なし | — | — |
| 6-1 | なし | — | — |
| 6-2 | あり | ハードルをよけて生き残ろう | DODGE THE HURDLES AND SURVIVE |
| 6-3 | あり | 迫るトゲ壁から逃げよう | ESCAPE THE CHASING SPIKE WALL |
| 7-1 | あり | 体の形を使って鍵を開けよう | USE YOUR BODY SHAPE TO UNLOCK THE PATH |
| 7-2 | なし | — | — |
| 7-3 | なし | — | — |
| 8-1 | あり | 落ちてくる柱をよけよう | DODGE THE FALLING PILLARS |
| 8-2 | あり | ボールを跳ね返してブロックを壊そう | REFLECT THE BALL AND BREAK EVERY BLOCK |
| 8-3 | あり | 敵から味方を守ろう | DEFEND YOUR FRIEND FROM THE ENEMIES |
| 9-1 | あり | ミサイルをよけて生き残ろう | DODGE THE MISSILES AND SURVIVE |
| 9-2 | あり | 落ちながらコインを集めよう | COLLECT COINS WHILE FALLING |
| 9-3 | あり | 落ちてくる粒を集めて、目標重量を目指そう | COLLECT THE FALLING GRAINS AND REACH THE TARGET WEIGHT |
| 10-1 | あり | スピードラン。制限時間内にゴールしよう。 | SPEED RUN. REACH THE GOAL BEFORE TIME RUNS OUT. |
| 10-2 | なし | — | — |
| 10-3 | あり | 銃弾を反射させて敵を倒そう | RICOCHET BULLETS TO DEFEAT THE ENEMIES |
| 11-1 | なし | — | — |
| 11-2 | あり | 最後まで生き残ろう | SURVIVE UNTIL TIME RUNS OUT |
| 11-3 | あり | 爆弾を投げてブロックを全部壊そう | THROW BOMBS TO BREAK EVERY BLOCK |
| 12-1 | あり | 箱を壊して、コインを集めよう | BREAK BOXES AND COLLECT COINS |
| 12-2 | あり | 制限時間内にコインを集めよう | COLLECT COINS BEFORE TIME RUNS OUT |
| 12-3 | あり | 制限時間内にコインを集めよう | COLLECT COINS BEFORE TIME RUNS OUT |
| 13-1 | あり | 敵から味方を守ろう | DEFEND YOUR FRIEND FROM THE ENEMIES |
| 13-2 | あり | ボールを跳ね返して敵を倒そう | BOUNCE THE BALL BACK TO DEFEAT THE ENEMIES |
| 13-3 | なし | — | — |
| 14-1 | あり | 制限時間内にゴールしよう | REACH THE GOAL BEFORE TIME RUNS OUT |
| 14-2 | あり | 傘で雨にあたらないようにしよう | USE THE UMBRELLA TO STAY OUT OF THE RAIN |
| 14-3 | あり | 味方の盾を作ってミサイルを防ごう | MAKE ALLY SHIELDS TO BLOCK THE MISSILES |
| 15-1 | あり | クレヨンデビルを倒そう | DEFEAT CRAYON DEVIL |
| 15-2 | あり | クレヨンストーカーを倒そう | DEFEAT CRAYON STALKER |
| 15-3 | あり | 敵を倒そう | DEFEAT THE ENEMIES |

## 推奨キャラモニター

- `10-3` までの対象小部屋では、ゲーム説明モニターの右隣に推奨キャラモニターを表示する。
- 表示はキャラの手描きアイコンと人数（`×N`）。現在の参加人数に対応する構成だけを表示する。
- 1人用の指定がない場合と `7-1` は「なし」と表示する。
- `11-1` 以降は使用キャラ固定のため、推奨キャラモニターを表示しない。

| ステージ | 2人 | 3人 | 4人 |
|---|---|---|---|
| 2-2 | 人×1、猫×1 | 人×2、猫×1 | 人×2、猫×2 |
| 4-3 | 人×1、カメ×1 | 人×2、カメ×1 | 人×2、カメ×2 |
| 6-2 | 人×1、猫×1 | 人×1、猫×2 | 人×2、猫×2 |
| 6-3 | 人×1、猫×1 | 人×1、猫×1、鳥×1 | 人×1、猫×1、鳥×2 |
| 7-1 | なし | なし | なし |
| 8-1 | 人×1、猫×1 | 人×2、猫×1 | 人×2、猫×2 |
| 8-2 | 人×1、スライム×1 | 人×1、スライム×2 | 人×1、スライム×3 |
| 8-3 | 人×1、猫×1 | 人×1、猫×2 | 人×1、猫×3 |
| 9-1 | スライム×2 | スライム×3 | スライム×4 |
| 9-2 | 人×1、鳥×1 | 人×1、鳥×2 | 人×2、鳥×2 |
| 9-3 | スライム×2 | スライム×3 | スライム×4 |
| 10-1 | スライム×2 | スライム×3 | スライム×4 |
| 10-3 | 人×4 | 人×4 | 人×4 |

## 集計

- 小部屋あり: 26ステージ
- 小部屋なし: 19ステージ

## 実装上の小部屋生成条件

次のどちらかを満たす場合に小部屋が生成される。

1. ステージのルールが `Survival` または `BlockBreaker`
2. 個別指定されたステージ: `2-2`, `7-1`, `9-2`, `9-3`, `10-1`, `10-3`, `12-1`, `12-2`, `12-3`, `13-2`, `14-1`

## 参照元

- 小部屋の有無: `Assets/Scripts/StageManager.cs` の `RequiresChallengeReadyRoom()`
- モニター構成・表示更新: `Assets/Scripts/StageChallengeReadyRoomController.cs`
- 日本語・英語文言: `Assets/Scripts/LocalizationManager.cs` の `ready_room_game_*` と `ready_room_status`
- 各ステージのルール: `Assets/Resources/Stages/*.json` の `ruleMode`
