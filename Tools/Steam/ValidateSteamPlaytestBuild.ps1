param(
    [string]$BuildPath = ".\Builds\NICO DRAW Steam Playtest",
    [uint32]$ExpectedAppId = 0,
    [uint32]$ExpectedDepotId = 0
)

$ErrorActionPreference = "Stop"
$projectRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot "..\..")).Path
if (-not $PSBoundParameters.ContainsKey("BuildPath")) {
    $BuildPath = Join-Path $projectRoot "Builds\NICO DRAW Steam Playtest"
}
if (-not (Test-Path -LiteralPath $BuildPath -PathType Container)) {
    throw "Build directory was not found: $BuildPath"
}
$resolvedBuild = (Resolve-Path -LiteralPath $BuildPath).Path
$expectedRoot = $resolvedBuild

$failures = [System.Collections.Generic.List[string]]::new()
$warnings = [System.Collections.Generic.List[string]]::new()
$exePath = Join-Path $expectedRoot "NICO DRAW.exe"
$dataPath = Join-Path $expectedRoot "NICO DRAW_Data"
$distributionManifestPath = Join-Path $expectedRoot "nico_draw_build.json"

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
$buildLogs = @(Get-ChildItem -LiteralPath $expectedRoot -Recurse -File -Filter "*.log" -ErrorAction SilentlyContinue)
if ($buildLogs.Count -gt 0) {
    $warnings.Add("Build logs are present and will be excluded from the Steam depot upload.")
}
$il2CppBackup = Join-Path $expectedRoot "NICO DRAW_BackUpThisFolder_ButDontShipItWithYourGame"
if (Test-Path -LiteralPath $il2CppBackup -PathType Container) {
    $failures.Add("Unity IL2CPP backup artifacts are present; do not upload the BackUpThisFolder directory.")
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

if (-not (Test-Path -LiteralPath (Join-Path $dataPath "Plugins\x86_64\steam_api64.dll") -PathType Leaf)) {
    $failures.Add("Steamworks runtime is missing from NICO DRAW_Data/Plugins/x86_64.")
}

if (-not (Test-Path -LiteralPath $distributionManifestPath -PathType Leaf)) {
    $failures.Add("nico_draw_build.json is missing; the build flavor and Steam target cannot be verified.")
} else {
    try {
        $distribution = Get-Content -LiteralPath $distributionManifestPath -Raw | ConvertFrom-Json
        if ($distribution.scriptingBackend -ne "IL2CPP") {
            $failures.Add("The distribution manifest does not identify an IL2CPP build.")
        }
        if (@($distribution.scriptingDefines) -notcontains "NICO_DRAW_STEAM") {
            $failures.Add("The distribution manifest is missing the NICO_DRAW_STEAM define.")
        }
        if ($ExpectedAppId -ne 0 -and [string]$distribution.steamAppId -ne [string]$ExpectedAppId) {
            $failures.Add("Build AppID $($distribution.steamAppId) does not match upload AppID $ExpectedAppId.")
        }
        if ($ExpectedDepotId -ne 0 -and [string]$distribution.steamDepotId -ne [string]$ExpectedDepotId) {
            $failures.Add("Build DepotID $($distribution.steamDepotId) does not match upload DepotID $ExpectedDepotId.")
        }
        if ($ExpectedAppId -eq 5231000 -and $distribution.flavor -ne "steam-demo") {
            $failures.Add("The Steam Demo upload requires a steam-demo build.")
        }
        if ($ExpectedAppId -eq 5230890 -and $distribution.flavor -ne "steam-playtest") {
            $failures.Add("The Steam Playtest upload requires a steam-playtest build.")
        }
    } catch {
        $failures.Add("nico_draw_build.json could not be parsed: $($_.Exception.Message)")
    }
}

if ($warnings.Count -gt 0) {
    $warnings | ForEach-Object { Write-Warning $_ }
}
if ($failures.Count -gt 0) {
    $failures | ForEach-Object { Write-Error $_ -ErrorAction Continue }
    throw "Steam build validation failed with $($failures.Count) problem(s)."
}

Write-Output "Steam build validation passed: $expectedRoot"
