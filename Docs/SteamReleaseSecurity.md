# Steam配布版セキュリティ手順

正規タイトルは `NICO DRAW`。Playtest版の既定バージョンは `0.1.0-playtest.1`。

## ビルド

- ローカル確認には従来どおり `PICO/Build Windows EXE` を使う。
- Steamへアップロードする成果物は `PICO/Build Windows Steam Release` を使う。
- Steam配布版にはUnity Hubから `Windows Build Support (IL2CPP)` を追加しておく。未導入時は配布用ビルドが停止する。
- 配布用出力は `Builds/NICODRAWSteamPlaytest/`。Mono版の `Assembly-CSharp.dll` をSteamへ登録しない。
- SteamPipeへのアップロード前に `Tools/Steam/ValidateSteamPlaytestBuild.ps1` を実行する。
- Playtest App IDとDepot IDを取得後は `Tools/Steam/UploadSteamPlaytest.ps1` でVDF生成とアップロードを行える。本編App IDではなくPlaytest子App IDを渡す。

例:

```powershell
.\Tools\Steam\UploadSteamPlaytest.ps1 `
  -AppId 1234567 `
  -DepotId 1234568 `
  -SteamCmdPath "C:\SteamworksSDK\tools\ContentBuilder\builder\steamcmd.exe" `
  -SteamUser "BUILD_ACCOUNT"
```

App ID、Depot ID、Steamworks SDKが未提供の状態ではアップロード自体は実行しない。
- Steamへ上げるEXEとDLLには、最終的に組織のコード署名証明書でAuthenticode署名する。

## コンテンツ署名鍵

- 署名対象は `Assets/Resources/Stages/*.json` と `Assets/Resources/Localization/*.json`。
- ビルド時にRSA-3072署名付きマニフェストを自動生成する。改変または異なる版を検出した場合、オフラインプレイは維持し、オンラインのみ拒否する。
- 秘密鍵はリポジトリへ入れない。標準位置は `%LOCALAPPDATA%/PICO/BuildSecurity/content-signing-private.xml`。
- CIでは環境変数 `PICO_CONTENT_SIGNING_KEY` に、CIのSecret領域へ保存した鍵ファイルのパスを設定する。
- 鍵は暗号化した別媒体にもバックアップする。漏えいした場合は公開鍵を差し替え、全ビルドを更新する。

## Steam/EOSでリリース前に必須の外部設定

現状のEOSログインはDevice ID方式であり、それだけではSteam購入者であることを証明しない。Steam Playtestの限定配布には利用できるが、コピーされた実行ファイルからのオンライン接続は防げない。Steamworks SDKとApp IDを受領後、次を正式販売版のリリース条件として実装・設定する。

1. Steam Auth Session Ticketを取得し、EOS ConnectへSteam外部資格情報として渡す。
2. 無効・期限切れ・別App IDのチケットではオンラインログインさせない。
3. EOS Developer PortalでSteam Integrated Platformと製品環境を設定する。
4. EOS Easy Anti-Cheatを使う場合は、秘密鍵をリポジトリ外へ置き、`etc/config/eos_plugin_tools_config.json` のツールパスをCI用に設定する。
5. Steamworksの `steamApiInterfaceVersionsArray` を使用SDK版に合わせる。空のまま配布しない。

## 運用上の注意

- Steamの「ファイルの整合性を確認」をサポート案内に含める。
- EOSへWindowsの端末名を送信しない。表示名にはゲーム内で設定したプレイヤー名だけを使う。
- Playtestビルドでは、非公開ルームの生コードをLobby Attributeへ保存せず、コード所持証明が済んだ参加者だけを同期対象にする。
- P2Pのホスト権威方式では、ホスト自身の完全な不正防止はできない。ランキングや報酬を付ける場合は専用サーバー側で結果を確定する。
- リリースごとにステージ署名、異なるビルド間の接続拒否、4人接続、途中参加、再試行、全ステージクリアを確認する。
