# PICO / DrawBody 実装ガイド

このファイルは、今後このプロジェクトを変更する開発者・エージェント向けの実装メモです。
画面だけを直して既存データ、テストプレイ、オンライン同期を壊さないため、変更前に関連項目を確認してください。

## 基本方針

- Unity 6（現在のプロジェクトバージョンは `6000.1.2f1`）の2Dゲーム。
- ステージとUIは「ノート／キャンバス＋クレヨン」の世界観を維持する。
- UIの枠や直線は整えて配置し、読みにくい手描き風の歪みは使わない。
- 表示文言をコードへ直接増やさず、`LocalizationManager`のIDを使う。日本語・英語を同じ変更で追加する。
- `StageObjectType`はJSONへ数値として保存されるため、既存値の途中へ追加しない。新しい値は必ずenum末尾へ追加する。
- オブジェクトIDはリンクと通信の識別子でもある。コピー時を含め、必ず一意にする。
- 変更後はUnityのConsoleまたは`Editor.log`でコンパイルエラーがないことを確認する。

## ステージデータ

中心となる型は`Assets/Scripts/StageData.cs`の`StageData`と`StageObjectData`。

`StageObjectData`の主なフィールド:

- `objectId`: 保存・選択・リンク・通信で共通利用する一意ID。
- `type`: `StageObjectType`。
- `position`, `size`, `rotation`: 配置情報。
- `pathPoints`, `pathThickness`: フリーハンド地形。
- `connectedRects`: 見た目を結合しつつ個別IDを保つ地形部品。
- `keepSeparate`: 接続表示を保ちながら、別オブジェクトとして扱う指定。
- `actionStrength`: ジャンプ台の強さや計量器の作動重量など、種類固有の数値。
- `linkTargetId`, `linkAction`: リンク先と動作方法。

フィールドを追加した場合は、少なくとも次を同時に更新する:

1. `RuntimeStageEditor.Persistence.cs`の`CloneData`
2. Undo/Redo用スナップショット
3. コピー処理
4. 保存JSONとの後方互換性
5. 必要なら通信モデル

保存先はEditor上では`Assets/Resources/Stages/{stageId}.json`。
`RuntimeStageEditor.Persistence.cs`の`Save`、`LoadWorkingData`、`CreateStageData`が担当する。

## ステージ編集画面

`RuntimeStageEditor`はpartial classで役割を分割している。

- `RuntimeStageEditor.cs`: 状態、入力の入口、参照解決。
- `RuntimeStageEditor.Editing.cs`: 配置、移動、拡縮、吸着、フリーハンド、外枠。
- `RuntimeStageEditor.Ui.cs`: UIコマンド、ドロップダウン、選択表示。
- `RuntimeStageEditor.Links.cs`: リンク元／先、リンク動作。
- `RuntimeStageEditor.ListPanel.cs`: オブジェクト・リンク一覧。
- `RuntimeStageEditor.History.cs`: Undo/Redo。
- `RuntimeStageEditor.Persistence.cs`: 読込、保存、テストプレイ、編集画面への復帰。
- `StageEditorVisualPolisher.cs`: 実行時UIの配置・装飾・追加コントロール。

### 新しいステージオブジェクトを追加する手順

1. `StageData.cs`の`StageObjectType`末尾へ追加。
2. `StageObjectCatalog.cs`へカテゴリ、配置方式、種類を登録。
3. `StageObjectFactory.CreateDefaultData`へ初期サイズ・初期値を追加。
4. `StageObjectFactory.Create`から専用生成処理へ分岐。
5. `AddEditorMetadata`を必ず呼び、ID・種類・リンク情報をGameObjectへ持たせる。
6. 日本語・英語の`stage_object_{snake_case_name}`を追加。
7. リンク元になれる場合は`RuntimeStageEditor.Links.cs`の`CanBeLinkSource`へ追加。
8. オンラインで状態を持つ場合はホスト権限とスナップショット復元まで実装。

### 編集UIを追加するとき

- 実行時生成UIは`StageEditorVisualPolisher.Polish`から作る。
- `LayoutToolPanel`で生成と配置、`RefreshState`で表示条件と現在値を同期する。
- 選択変更、Undo/Redo、再構築、言語切替の全経路で同じ値が出るようにする。
- `InputField.SetTextWithoutNotify`だけでは実行時生成した`Text`が更新されない場合がある。必要なら表示Textも明示同期する。
- 数値入力は入力形式、文字数、最小・最大値をUIと保存処理の両方で制限する。
- 変更前に`PushUndo()`を呼び、変更後に対象を再構築して一覧と表示を更新する。

### 座標・吸着・サイズ

- グリッド幅は`RuntimeStageEditor`の`gridSize`を基準にする。
- 床、壁、橋を接続するときは、見た目の線だけでなくコライダー面とデータ座標を揃える。
- 吸着閾値を広げすぎると、離れた天井と床が突然接続される。位置の移動量とオブジェクト寸法に対して小さい閾値を使う。
- 「見た目は結合、動作は個別」が必要な地形ではIDを統合しない。`keepSeparate`と接続表示を利用する。
- コピー後は元と同じ`objectId`を使わない。選択やホイール操作、通信ターゲットが別個体へ飛ぶ原因になる。

### テストプレイ

- `TestPlay`は編集カメラ状態を保存してからゲーム用カメラ倍率へ切り替える。
- `ResumeAfterTestPlay`は編集用オブジェクト、背景色、カメラ、UIを復元する。
- 編集時のズーム率をゲーム開始時へ持ち込まない。

## リンク・ギミック

リンクの実行は`StageGimmickLinkController`が担当する。

- リンク元: `StageEditorObject.linkTargetId`を持つオブジェクト。
- リンク先: `objectId`が`linkTargetId`と一致するオブジェクト。
- `Reveal`: 最初は非表示で、発動後に表示。
- `RevealGrowRightToLeft`: 橋などを右から左へ伸ばして表示。
- `Hide`: 最初は存在し、発動後に消す。
- `Unlock`: 鍵から鍵穴へ使う専用動作。

汎用リンク元には`StageGimmickTrigger`が付く。専用判定を持つ鍵穴や計量器には汎用Triggerを重ねず、専用コンポーネントから`ActivateFromTrigger`を呼ぶ。

### 計量器

- 種類: `StageObjectType.InkScale`
- 初期作動重量: 300 INK
- 入力範囲: 1～2000 INK（1人あたり）
- 実際の作動重量: 編集画面の入力値×現在の参加人数。ローカル追加キャラとオンラインロビー人数の両方に追従する。
- 重量: 各`PlayerAbilityController.CurrentProfile.TotalInk`の合計
- 計測対象: 計量器上の検出範囲に入り、`PlayerController2D.IsGrounded`であるキャラクター
- 積み重なったキャラクターも数えるため、検出範囲は上方向へ伸ばしている。
- 表示は`Mathf.MoveTowards`で高速カウントし、人数を掛けた作動重量を「超えた」ときだけ発動する。等しいだけでは発動しない。
- 発動後はラッチし、OFFへ戻さない。黄色から緑へ変化させる。
- 表示フォントはGameSceneで参照されている`Yomogi-Regular.ttf`を優先する。

## オンライン通信

基本方針はホスト権威方式。

- `StageGimmickSyncManager.ShouldAskHost`がtrueのクライアントは、結果を直接確定せず`link_request`を送る。
- ホストは`StageGimmickLinkController.HandleActivationRequest`で処理し、確定した`OnlineLinkGimmickState`を`link_state`として配信する。
- クライアントは`ApplyNetworkState`でリンク先とリンク元の見た目を確定する。
- ホストは一定間隔で`BroadcastAllStates`を送り、後から参加・一時的な受信漏れでも状態を戻せるようにする。
- 木箱など移動物は`objectId`単位で所有権を管理し、所有者だけがTransformを送る。
- ローカルで先にラッチ表示するとホスト却下時に戻せなくなるため、クライアントはホストのstateを受けるまで確定色へ変えない。

通信対象を追加するときの確認項目:

1. 誰が判定を行うか。原則ホスト。
2. リクエストと確定stateが分かれているか。
3. 途中参加／再同期用のスナップショットがあるか。
4. 同じオブジェクトをコピーしてもIDが衝突しないか。
5. 表示だけでなくCollider、Rigidbody、active状態も一致するか。
6. 1人プレイ時は通信管理がなくても同じロジックで動くか。

## 背景装飾画像

ゲーム内オブジェクトの色鉛筆アート、専用PNGとコード描画の使い分け、当たり判定を維持した差し替え手順は `Assets/Art/NicoDrawObjectArtGuide.md` を正本として参照する。

背景装飾の実体は透過PNGのSprite。配置物なので自動背景とは分離する。

### 画像の見た目

- 授業中のノートへ描いたような、薄いクレヨン／色鉛筆の落書き。
- 写実的にしない。単純で読み取りやすい輪郭にする。
- 輪郭だけにせず、内側を薄く不均一に塗る。
- 色は明るめ、低～中彩度。背景としてキャラクターや床より目立たせない。
- 完全な左右対称や機械的な直線を避け、少量の手描き揺れを入れる。
- 画像背景は透明。白い四角や影を残さない。
- 外周に十分な透明余白を取り、回転・拡大時に線が切れないようにする。
- 1ファイルに1モチーフ。複数案を1枚へ並べない。
- 文字画像は通常フォントを置くだけにせず、文字自体をクレヨンで描いた画像として作る。

### ファイルと命名

保存場所:

`Assets/Resources/StageDecorations/CrayonSet/`

`StageObjectFactory.GetCrayonDecorationResourcePath`がenum名を自動変換する。

例:

```text
StageObjectType.BackgroundCatFace
-> Resources.Load<Sprite>("StageDecorations/CrayonSet/cat-face")
-> Assets/Resources/StageDecorations/CrayonSet/cat-face.png
```

命名規則:

- enumは`Background` + PascalCase。
- PNGは`Background`を外してkebab-case。
- Unity Import SettingsはTexture Typeを`Sprite (2D and UI)`にする。
- Alphaを保持する。
- 同じセットではCanvasサイズ、Pixels Per Unit、透明余白の割合を揃える。

### 新しい背景装飾を登録する手順

1. 上記方針で透過PNGを作る。
2. `CrayonSet`へkebab-case名で配置し、SpriteとしてImport。
3. `StageObjectType`末尾へ`BackgroundXxx`を追加。
4. `StageObjectCatalog`へ`Decoration / Point / Decoration`として登録。
5. `LocalizationManager`へ日本語・英語名を追加。
6. Editorで配置、選択、コピー、ホイール拡縮、Shift+ホイール回転を確認。
7. 通常プレイとタイトル裏ステージの両方で文字化け・白背景・床越しの透けがないか確認。

Spriteが見つからない場合、木・草・花・茂み・雲など一部は`DrawBackgroundDecoration`のコード描画へフォールバックする。画像品質を優先する種類では、ファイル名とResourceパスの不一致を先に疑う。

## フォント・ローカライズ

- 手書きフォント: `Assets/Art/Fonts/Yomogi-Regular.ttf`
- UI Textでは、同じ画面の既存Textからフォントを引き継ぐ。
- `TextMesh`へフォントを設定するときは`font`だけでなく、`MeshRenderer.sharedMaterial = font.material`も設定する。
- 文言IDは日本語・英語を同時追加する。
- enumの表示名は`StageObjectCatalog.GetObjectKey`が`stage_object_xxx`へ変換する。
- 日本語をコードへ追加する際、環境による文字化けを避ける必要がある場合はUnicodeエスケープを利用する。

## DRAW画面とキャラクター操作

- DRAW画面を開くたびに`DoodleUiDirector.ApplyTheme`を実行しない。全UIを走査する重い処理で、テーマはAwake/Startと言語変更時に適用済み。
- DRAW画面の個人上限は500、全体上限は参加人数×350。1人プレイの実質上限は350になる。
- 人数はオンライン時だけ`OnlineManager`のロビー人数を使う。オフラインで「キャラ追加」した場合は`StageManager.GetInkBudgetPlayerCount`で有効なprimary/secondaryキャラクターを数える。
- オフライン複数キャラの全体使用量には、編集中キャラのインクだけでなく、もう一方の確定済み`PlayerAbilityController.CurrentProfile.TotalInk`も加える。
- 個人上限と全体上限は別ゲージで、ラベルに「個人上限」「全体 n人×350」と式を表示する。全体上限は確定時に検証する。
- 全リセットは`DrawManager.ResetAllToDefault`で全種族の描画データを初期化し、人間・胴体選択へ戻す。確定前にキャンセルした場合は編集開始時のスナップショットへ戻れる。
- 手描きキャラクターは1本の線が多数のColliderへ分割される。長い／細かい足では64個を簡単に超えるため、取得判定に固定長64のPhysics結果バッファを使わない。
- 木箱取得はFを押した瞬間だけの処理なので、可変長`List<Collider2D>`で候補をすべて取得し、自分自身の線分を除外してから最短コライダー面を選ぶ。

## 最低限の確認手順

1. Unityのスクリプトコンパイルがエラー0件。
2. Editorで新規配置、選択、移動、サイズ変更、回転、コピー、削除、Undo/Redo。
3. 保存してEditorを開き直しても値・ID・リンクが維持される。
4. テストプレイから編集画面へ戻れる。
5. 1人プレイで発動前後のColliderと表示を確認。
6. 2クライアントでリンク元発動、リンク先動作、途中状態の同期を確認。
7. 日本語・英語を切り替え、重なり、ID露出、文字化けがないことを確認。
