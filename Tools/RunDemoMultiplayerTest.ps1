param(
    [string]$GameExe = ".\Builds\NICO DRAW Demo\NICO DRAW.exe",
    [ValidateRange(2, 4)]
    [int]$Players = 4,
    [ValidateRange(1024, 65535)]
    [int]$Port = 17777
)

$launcher = Join-Path $PSScriptRoot "RunLocalMultiplayerTest.ps1"
& $launcher -GameExe $GameExe -Players $Players -Port $Port -FullFlow
