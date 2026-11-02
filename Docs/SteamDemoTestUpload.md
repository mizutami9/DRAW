# Steamデモ `test` ブランチへのアップロード手順

この文書は、**製品版と同じフルコンテンツを、SteamデモAppの非公開 `test` ブランチへアップロードする手順**をまとめたものです。

通常のデモ版、製品版Appのdefaultブランチ、Steam Playtest版とは別物です。App IDやビルド出力先を取り違えないでください。

## 対象

| 項目 | 値 |
|---|---|
| 用途 | デモApp上で製品版フルコンテンツをテスト |
| Steam App ID | `5231000` |
| Steam Depot ID | `5231001` |
| Steamブランチ | `test` |
| Unityビルド種別 | `steam-demo-full-test` |
| ビルド出力先 | `Builds/NICO DRAW Steam Demo Full Test/` |
| Scripting Backend | IL2CPP |

このビルドには `NICO_DRAW_STEAM` と `NICO_DRAW_STEAM_DEMO_APP` を含め、`NICO_DRAW_DEMO` は含めません。これにより、コンテンツは製品版相当のまま、Steam実行時はデモApp IDへ接続します。

## 間違えやすい配信先

| 配信物 | App ID | Depot ID | 出力先 |
|---|---:|---:|---|
| 今回のフルコンテンツtest | `5231000` | `5231001` | `NICO DRAW Steam Demo Full Test` |
| 公開デモ版 | `5231000` | `5231001` | `NICO DRAW Steam Demo` |
| 製品版 | `5090120` | `5090121` | `NICO DRAW Steam Product` |
| Steam Playtest版 | `5230890` | `5230891` | `NICO DRAW Steam Playtest` |

同じデモApp IDでも、公開デモ版とフルコンテンツtest版は中身が異なります。`test` ブランチへ製品内容を載せる場合は、必ず `NICO DRAW Steam Demo Full Test` を使用します。

## 事前条件

- Unity `6000.1.2f1`
- Unity HubでWindows Build Support（IL2CPP）を導入済み
- `sdk/tools/ContentBuilder/builder/steamcmd.exe` が存在する
- SteamworksでApp `5231000` とDepot `5231001` のアップロード権限がある
- Steam Guardを求められた場合に認証できる
- Unityのスクリプトコンパイルエラーが0件
- アップロード対象の変更が保存済み

## 1. フルコンテンツtestビルドを作成

### Unityメニューから作成する場合

Unity Editorでプロジェクトを開き、次を実行します。

```text
PICO > Build Windows Steam Demo Full-Content Test
```

成功すると、以下へビルドされます。

```text
Builds/NICO DRAW Steam Demo Full Test/NICO DRAW.exe
```

### コマンドラインから通常Editorを起動する場合

このPCでは `-batchmode` がheadlessライセンス不足で失敗する場合があります。その場合は、`-batchmode` を付けず通常Editorとして実行します。

```powershell
& "C:\Program Files\Unity\Hub\Editor\6000.1.2f1\Editor\Unity.exe" `
  -projectPath "C:\workSpace\PICO" `
  -executeMethod DrawBody.EditorTools.WindowsBuildMenu.BuildWindowsSteamDemoFullContentTest `
  -quit `
  -logFile "C:\workSpace\PICO\Logs\steam-demo-full-test-build-gui.log"
```

ログに次の2行があることを確認します。

```text
Build Finished, Result: Success.
Hardened full-content product build for the Steam demo test branch created:
```

## 2. ビルド識別情報を確認

次のファイルを開きます。

```text
Builds/NICO DRAW Steam Demo Full Test/nico_draw_build.json
```

最低限、以下と一致していることを確認します。

```json
{
  "flavor": "steam-demo-full-test",
  "scriptingBackend": "IL2CPP",
  "steamAppId": "5231000",
  "steamDepotId": "5231001"
}
```

さらに、ファイル更新日時が今回のビルド時刻になっていることを確認します。古いビルドを誤ってアップロードしないためです。

## 3. アップロード前検証

プロジェクトルートで次を実行します。

```powershell
.\Tools\Steam\ValidateSteamPlaytestBuild.ps1 `
  -BuildPath ".\Builds\NICO DRAW Steam Demo Full Test" `
  -ExpectedAppId 5231000 `
  -ExpectedDepotId 5231001
```

成功時は次のように表示されます。

```text
Steam build validation passed: ...\Builds\NICO DRAW Steam Demo Full Test
```

この検証では、主に以下を確認します。

- IL2CPPビルドである
- Mono版の `Assembly-CSharp.dll` が混入していない
- App IDとDepot IDが一致する
- `steam-demo-full-test` である
- `NICO_DRAW_DEMO` が含まれていない
- SteamworksとEOSの実行ファイル・設定が存在する
- 秘密鍵、`steam_appid.txt`、IL2CPPバックアップフォルダが混入していない

検証に失敗した状態ではアップロードしないでください。

## 4. Steamへアップロードし、`test` ブランチへ反映

`<Steamユーザー名>`をSteamworksのビルド用アカウント名に置き換えます。

```powershell
.\Tools\Steam\UploadSteamDemo.ps1 `
  -SteamUser "<Steamユーザー名>" `
  -BuildPath ".\Builds\NICO DRAW Steam Demo Full Test" `
  -Description "NICO DRAW full-content test YYYY-MM-DD 変更概要" `
  -SetLiveBranch "test"
```

このスクリプトは、再度ビルド検証を実行してからSteamPipe用VDFを生成し、SteamCMDでアップロードします。

Steam Guardの確認が出た場合は、表示された指示に従って認証します。

成功時には以下が表示されます。

```text
Successfully finished AppID 5231000 build (BuildID ...).
Upload completed.
```

BuildIDは作業記録へ残してください。

## 5. `SetLive` の最終確認

生成された次のファイルを確認します。

```text
Tools/Steam/Generated/app_build_5231000.vdf
```

以下になっている必要があります。

```text
"AppID" "5231000"
"SetLive" "test"
"5231001" ".../depot_build_5231001.vdf"
```

その後、Steamworksのビルド画面でも、表示されたBuildIDがデモApp `5231000` の `test` ブランチへ割り当てられていることを確認します。

## 6. Steamクライアントで確認

1. SteamライブラリでNICO DRAW Demoのプロパティを開く
2. ベータ参加から `test` ブランチを選ぶ
3. 更新を完了する
4. ゲームが起動することを確認する
5. タイトル、LOCAL、ONLINE、対象ステージを最低限確認する
6. 今回の変更箇所が実際に反映されていることを確認する

オンライン変更を含む場合は、別PCまたは別Steamアカウントを使い、2クライアントで参加・同期・切断まで確認します。

## トラブル対応

### `No valid Unity Editor license found`

`-batchmode`用のheadlessライセンスがありません。Unityメニューから実行するか、通常Editor起動のコマンドを使用します。

### 検証でApp IDまたはflavorが違う

ビルドフォルダを取り違えています。`Build Windows Steam Demo Full-Content Test` で作り直し、`NICO DRAW Steam Demo Full Test` を指定します。

### SteamCMDがSteam Guardを要求する

Steam Guardを完了して再実行します。認証情報やコードをログ・Markdown・Gitへ保存しないでください。

### 間違ったビルドを `test` に設定した

Steamworksのビルド画面で、直前の正常なBuildIDを `test` ブランチへ再設定します。製品版defaultブランチや公開デモのdefaultブランチは変更しないでください。

## 関連ファイル

- `Assets/Editor/WindowsBuildMenu.cs`
- `Assets/Scripts/SteamDistributionIds.cs`
- `Tools/Steam/ValidateSteamPlaytestBuild.ps1`
- `Tools/Steam/UploadSteamDemo.ps1`
- `Tools/Steam/UploadSteamPlaytest.ps1`

