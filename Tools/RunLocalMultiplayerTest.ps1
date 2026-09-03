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
$common = @(
    "-pico-regression-port=$Port",
    "-pico-regression-players=$Players",
    "-pico-regression-stage=$Stage"
)

Start-Process -FilePath $resolvedExe -ArgumentList ($common + @(
    "-pico-regression-role=host",
    "-pico-regression-name=Host"
))

Start-Sleep -Milliseconds 500
for ($player = 2; $player -le $Players; $player++) {
    Start-Process -FilePath $resolvedExe -ArgumentList ($common + @(
        "-pico-regression-role=client",
        "-pico-regression-name=P$player"
    ))
}
