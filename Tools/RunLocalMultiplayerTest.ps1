param(
    [Parameter(Mandatory = $true)]
    [string]$GameExe,
    [ValidateRange(2, 4)]
    [int]$Players = 4,
    [string]$Stage = "13-1",
    [ValidateRange(1024, 65535)]
    [int]$Port = 17777
)

$resolvedExe = (Resolve-Path -LiteralPath $GameExe -ErrorAction Stop).Path
$existingListener = Get-NetTCPConnection -State Listen -LocalPort $Port -ErrorAction SilentlyContinue
if ($null -ne $existingListener) {
    throw "Port $Port is already in use. Close every previous DrawBody test window before starting another local multiplayer test."
}

$logDirectory = Join-Path (Split-Path -Parent $PSScriptRoot) "Temp\LocalMultiplayerTest"
New-Item -ItemType Directory -Force -Path $logDirectory | Out-Null
$common = @(
    "-pico-regression-port=$Port",
    "-pico-regression-players=$Players",
    "-pico-regression-stage=$Stage",
    "-pico-debug-no-time-limit"
)

Start-Process -FilePath $resolvedExe -ArgumentList ($common + @(
    "-pico-regression-role=host",
    "-pico-regression-name=Host",
    "-logFile",
    (Join-Path $logDirectory "host.log")
))

Start-Sleep -Milliseconds 500
for ($player = 2; $player -le $Players; $player++) {
    Start-Process -FilePath $resolvedExe -ArgumentList ($common + @(
        "-pico-regression-role=client",
        "-pico-regression-name=P$player",
        "-logFile",
        (Join-Path $logDirectory "player-$player.log")
    ))
}
