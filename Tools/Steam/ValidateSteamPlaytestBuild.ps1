param(
    [string]$BuildPath = ".\Builds\NICODRAWSteamPlaytest"
)

$ErrorActionPreference = "Stop"
$resolvedBuild = (Resolve-Path -LiteralPath $BuildPath).Path
$expectedRoot = [System.IO.Path]::GetFullPath($resolvedBuild)
if (-not (Test-Path -LiteralPath $expectedRoot -PathType Container)) {
    throw "Build directory was not found: $expectedRoot"
}

$failures = [System.Collections.Generic.List[string]]::new()
$warnings = [System.Collections.Generic.List[string]]::new()
$exePath = Join-Path $expectedRoot "NICO DRAW.exe"
$dataPath = Join-Path $expectedRoot "NICO DRAW_Data"

if (-not (Test-Path -LiteralPath $exePath -PathType Leaf)) { $failures.Add("NICO DRAW.exe is missing.") }
if (-not (Test-Path -LiteralPath (Join-Path $expectedRoot "GameAssembly.dll") -PathType Leaf)) {
    $failures.Add("GameAssembly.dll is missing; this is not an IL2CPP build.")
}
if (Test-Path -LiteralPath (Join-Path $dataPath "Managed\Assembly-CSharp.dll") -PathType Leaf) {
    $failures.Add("Assembly-CSharp.dll is present; do not upload the Mono build.")
}
if (Test-Path -LiteralPath (Join-Path $expectedRoot "steam_appid.txt") -PathType Leaf) {
    $failures.Add("steam_appid.txt must not be included in a Steam depot.")
}

$secretFiles = @(Get-ChildItem -LiteralPath $expectedRoot -Recurse -File -ErrorAction SilentlyContinue |
    Where-Object { $_.Name -match "(?i)(private.*key|signing.*key|content-signing-private|\.pem$|\.pfx$)" })
if ($secretFiles.Count -gt 0) { $failures.Add("A private key or certificate file appears to be included in the build.") }

$bootConfig = Join-Path $dataPath "boot.config"
if (Test-Path -LiteralPath $bootConfig -PathType Leaf) {
    $bootText = Get-Content -LiteralPath $bootConfig -Raw
    if ($bootText -match "player-connection-debug=1|wait-for-managed-debugger=1") {
        $failures.Add("Development/debug player settings were detected in boot.config.")
    }
}

$eosConfig = Join-Path $dataPath "StreamingAssets\EOS\eos_product_config.json"
if (-not (Test-Path -LiteralPath $eosConfig -PathType Leaf)) {
    $failures.Add("EOS product configuration is missing.")
} else {
    $eos = Get-Content -LiteralPath $eosConfig -Raw | ConvertFrom-Json
    if ($eos.ProductName -ne "NICO DRAW") { $failures.Add("EOS ProductName is not NICO DRAW.") }
}

if (-not (Test-Path -LiteralPath (Join-Path $expectedRoot "steam_api64.dll") -PathType Leaf)) {
    $warnings.Add("Steamworks runtime is not present. EOS Device ID playtests work, but Steam ownership is not enforced.")
}

if ($warnings.Count -gt 0) {
    $warnings | ForEach-Object { Write-Warning $_ }
}
if ($failures.Count -gt 0) {
    $failures | ForEach-Object { Write-Error $_ -ErrorAction Continue }
    throw "Steam Playtest build validation failed with $($failures.Count) problem(s)."
}

Write-Output "Steam Playtest build validation passed: $expectedRoot"
