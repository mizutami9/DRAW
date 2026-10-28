# ローカル＋オンライン4人 回帰テスト

`RunLocalOnlineTopologyTests.ps1` は、仮想キーボード／仮想ゲームパッドを通常の参加処理へ入力し、複数プロセス間のDirect TCP通信で次の構成を確認する。

| ケース | ホスト接続 | 参加接続1 | 参加接続2 | 参加接続3 | 合計 |
|---|---|---|---|---|---:|
| keyboard-pad-local2-plus-online2 | P1キーボード＋P2パッド | パッド | キーボード | - | 4 |
| pad-pad-local2-plus-online2 | P1パッド＋P2パッド | パッド | パッド | - | 4 |
| pad-p1-plus-online3 | パッド | パッド | パッド | パッド | 4 |
| pad-keyboard-local2-plus-online2 | P1パッド＋P2キーボード | パッド | パッド | - | 4 |
| two-local-pairs-online | P1キーボード＋P2パッド | P1パッド＋P2パッド | - | - | 4 |
| joining-keyboard-pad-local2 | パッド | P1キーボード＋P2パッド | キーボード | - | 4 |
| joining-pad-keyboard-local2 | パッド | P1パッド＋P2キーボード | パッド | - | 4 |

最後の2ケースは、ローカル2人組がホストではなくオンライン参加側にいる場合の専用確認。

各プロセスで以下を検証する。

- P1/P2が指定デバイスで参加し、ゲームパッドIDを共有しない。
- ロビーがゲームプレイヤー4人を一意なIDで保持する。
- ステージ開始後に4体が生成され、各ローカルキャラクターを独立操作できる。
- ジャンプ、能力、主アクションのパッド入力が割り当て先へ届く。
- P2のSTARTでは共有メニューが開かず、P1のSTARTでは開く。
- メニュー中はキャラクター操作が止まり、P1パッドのUIカーソルが表示される。
- CONTINUE、RETRY、OPTION、LEAVE SESSIONが操作可能で、OPTIONが実際に開く。
- キャンセルでメニューを閉じてゲームへ戻れる。
- 実行中と終了時にUnity例外が発生しない。

実行例:

```powershell
.\Tools\RunLocalOnlineTopologyTests.ps1
```

入力デバイスはソフトウェア生成なので、Bluetooth切断・再接続やコントローラ固有ドライバーの挙動は別途実機で確認する。
