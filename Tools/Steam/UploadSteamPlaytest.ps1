param(
    [Parameter(Mandatory = $true)][uint32]$AppId,
    [Parameter(Mandatory = $true)][uint32]$DepotId,
    [Parameter(Mandatory = $true)][string]$SteamCmdPath,
    [Parameter(Mandatory = $true)][string]$SteamUser,
    [string]$BuildPath = ".\Builds\NICO DRAW Steam Playtest",
    [string]$Description = "NICO DRAW Steam Playtest",
    [string]$SetLiveBranch = ""
)

$ErrorActionPreference = "Stop"
$scriptRoot = $PSScriptRoot
$projectRoot = (Resolve-Path -LiteralPath (Join-Path $scriptRoot "..\..")).Path
if (-not $PSBoundParameters.ContainsKey("BuildPath")) {
    $BuildPath = Join-Path $projectRoot "Builds\NICO DRAW Steam Playtest"
}
if (-not (Test-Path -LiteralPath $BuildPath -PathType Container)) {
    throw "Build directory was not found: $BuildPath"
}
if (-not (Test-Path -LiteralPath $SteamCmdPath -PathType Leaf)) {
    throw "steamcmd executable was not found: $SteamCmdPath"
}
$resolvedBuild = (Resolve-Path -LiteralPath $BuildPath).Path
$resolvedSteamCmd = (Resolve-Path -LiteralPath $SteamCmdPath).Path

& (Join-Path $scriptRoot "ValidateSteamPlaytestBuild.ps1") `
    -BuildPath $resolvedBuild `
    -ExpectedAppId $AppId `
    -ExpectedDepotId $DepotId

$generatedRoot = Join-Path $scriptRoot "Generated"
$outputRoot = Join-Path $generatedRoot "Output"
New-Item -ItemType Directory -Force -Path $generatedRoot, $outputRoot | Out-Null

function ConvertTo-VdfString([string]$value) {
    if ($value -match "[`r`n]") { throw "VDF values cannot contain line breaks." }
    return $value.Replace("\", "\\").Replace('"', '\"')
}
function ConvertTo-VdfPath([string]$path) {
    return ConvertTo-VdfString $path.Replace("\", "/")
}
if (-not [string]::IsNullOrWhiteSpace($SetLiveBranch) -and $SetLiveBranch -notmatch '^[A-Za-z0-9._-]+$') {
    throw "SetLiveBranch contains unsupported characters. Use letters, numbers, dot, underscore, or hyphen."
}
$contentRoot = ConvertTo-VdfPath $resolvedBuild
$outputPath = ConvertTo-VdfPath $outputRoot
$safeDescription = ConvertTo-VdfString $Description
$depotScript = Join-Path $generatedRoot "depot_build_$DepotId.vdf"
$appScript = Join-Path $generatedRoot "app_build_$AppId.vdf"
$depotScriptVdf = ConvertTo-VdfPath $depotScript
$safeLiveBranch = ConvertTo-VdfString $SetLiveBranch
$liveLine = if ([string]::IsNullOrWhiteSpace($safeLiveBranch)) { "" } else { "`n    `"SetLive`" `"$safeLiveBranch`"" }

$depotVdf = @"
"DepotBuildConfig"
{
    "DepotID" "$DepotId"
    "ContentRoot" "$contentRoot"
    "FileMapping"
    {
        "LocalPath" "*"
        "DepotPath" "."
        "Recursive" "1"
    }
    "FileExclusion" "*.pdb"
    "FileExclusion" "*.log"
    "FileExclusion" "steam_appid.txt"
    "FileExclusion" "*_BurstDebugInformation_DoNotShip*"
    "FileExclusion" "*_BackUpThisFolder_ButDontShipItWithYourGame*"
}
"@

$appVdf = @"
"AppBuild"
{
    "AppID" "$AppId"
    "Desc" "$safeDescription"
    "BuildOutput" "$outputPath"
    "ContentRoot" "$contentRoot"$liveLine
    "Depots"
    {
        "$DepotId" "$depotScriptVdf"
    }
}
"@

[System.IO.File]::WriteAllText($depotScript, $depotVdf, [System.Text.UTF8Encoding]::new($false))
[System.IO.File]::WriteAllText($appScript, $appVdf, [System.Text.UTF8Encoding]::new($false))

Write-Output "Uploading Steam AppID $AppId / DepotID $DepotId. Steam Guard may prompt for confirmation."
& $resolvedSteamCmd +login $SteamUser +run_app_build $appScript +quit
if ($LASTEXITCODE -ne 0) { throw "steamcmd upload failed with exit code $LASTEXITCODE." }
Write-Output "Upload completed. Confirm the build and target branch in Steamworks before making it available."
