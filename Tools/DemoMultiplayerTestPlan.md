# デモ版 4人マルチプレイ・テスト項目

対象はデモ版と同じ `NICO_DRAW_DEMO` 制限を持つ4つの独立Windowsクライアントです。自動試験はローカルDirect TCPを使い、EOS固有の認証/P2P試験は別枠とします。

## 自動スモーク（14-3）

1. タイトル起動後、ホストがルームを作成できる。
2. 3クライアントが同じルームへ参加し、4人全員がREADYになる。
3. ホスト開始により全クライアントが14-3へ遷移する。
4. 4つのPlayer IDが欠落・重複せず、各端末に4体が生成される。
5. 各クライアントが通常のPlayerController入力経路で別々に移動・ジャンプできる。
6. ステージ内で各プレイヤーがDRAWを開ける。
7. DRAW中はその端末の所有プレイヤーだけ操作不能になり、確定後に操作が戻る。
8. 4人が時間をずらして別々の見た目へ書き直し、全端末が4人分のBodyDataを受信する。
9. 書き直し中の相互受信で例外、NaN/Infinity、異常速度、進行停止が発生しない。
10. Unity例外と整合性エラーがなく、全4クライアントが同じ試験結果を完了する。
11. 全端末から見た各プレイヤーの座標差が0.75以内で、種族表示が一致する。
12. 4人同時DRAW確定でも、受信を保留した形状がDRAW終了後に反映され、デッドロックしない。

## 手動確認を残す項目

- タイトルの「マルチプレイ」ボタン、ホスト/参加ボタン、ルームコード入力、READY表示の見た目とマウス操作。
- EOS Device IDまたはSteam認証を使う実サービス上の4アカウント接続。
- 通信切断、ホスト退出、再参加、途中参加。
- DRAW画面での実際のマウス描線、キャンセル、上限超過メッセージ、多言語表示。
- 2人同時に同一物体を掴む、死亡/復活、ステージクリア/次ステージ遷移。

## 実行

Unityメニュー `PICO > Build Windows Demo Multiplayer Test` で
`Builds/NICO DRAW Demo Test/NICO DRAW.exe` をビルド後:

```powershell
.\Tools\RunDemoMultiplayerSmokeTest.ps1 -Players 4 -Stage "14-3"
.\Tools\RunDemoMultiplayerSmokeTest.ps1 -Players 4 -Stage "14-3" -ConcurrentRedraw -Port 19434
```

成果物は `Temp/DemoMultiplayerSmoke/<run-id>/` に、クライアント別Unityログ、JSON、スクリーンショット、集計レポートとして保存されます。
