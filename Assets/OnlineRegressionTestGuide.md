# 1台PCでのオンライン回帰テスト

Windowsビルドを作成後、PowerShellで次を実行する。

```powershell
.\Tools\RunLocalMultiplayerTest.ps1 -GameExe ".\Build\NicoDraw.exe" -Players 4 -Stage "13-1"
```

同じPC上でホスト1台と参加者1～3台が起動し、Direct TCPのローカル部屋へ自動参加する。
全員がREADYになると、指定したステージを自動開始する。通常起動ではこのモードは有効にならない。

主なオプション:

- `-Players 2～4`: 起動する人数
- `-Stage "15-3"`: 自動開始するステージ
- `-Port 17777`: 他のテストと重複しない待受ポート
