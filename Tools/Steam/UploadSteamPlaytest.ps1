param(
    [Parameter(Mandatory = $true)][uint32]$AppId,
    [Parameter(Mandatory = $true)][uint32]$DepotId,
    [Parameter(Mandatory = $true)][string]$SteamCmdPath,
    [Parameter(Mandatory = $true)][string]$SteamUser,
    [string]$BuildPath = ".\Builds\NICODRAWSteamPlaytest",
    [string]$Description = "NICO DRAW Steam Playtest",
    [string]$SetLiveBranch = ""
)

$ErrorActionPreference = "Stop"
$scriptRoot = $PSScriptRoot
$projectRoot = (Resolve-Path -LiteralPath (Join-Path $scriptRoot "..\..")).Path
$resolvedBuild = (Resolve-Path -LiteralPath $BuildPath).Path
$resolvedSteamCmd = (Resolve-Path -LiteralPath $SteamCmdPath).Path

& (Join-Path $scriptRoot "ValidateSteamPlaytestBuild.ps1") -BuildPath $resolvedBuild

$generatedRoot = Join-Path $scriptRoot "Generated"
$outputRoot = Join-Path $generatedRoot "Output"
New-Item -ItemType Directory -Force -Path $generatedRoot, $outputRoot | Out-Null

function ConvertTo-VdfPath([string]$path) { return $path.Replace("\", "/") }
$contentRoot = ConvertTo-VdfPath $resolvedBuild
$outputPath = ConvertTo-VdfPath $outputRoot
$depotScript = Join-Path $generatedRoot "depot_build_$DepotId.vdf"
$appScript = Join-Path $generatedRoot "app_build_$AppId.vdf"
$depotScriptVdf = ConvertTo-VdfPath $depotScript
$liveLine = if ([string]::IsNullOrWhiteSpace($SetLiveBranch)) { "" } else { "`n    `"SetLive`" `"$SetLiveBranch`"" }

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
    "FileExclusion" "steam_appid.txt"
    "FileExclusion" "*_BurstDebugInformation_DoNotShip*"
    "FileExclusion" "*_BackUpThisFolder_ButDontShipItWithYourGame*"
}
"@

$appVdf = @"
"AppBuild"
{
    "AppID" "$AppId"
    "Desc" "$Description"
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

Write-Output "Uploading Playtest AppID $AppId / DepotID $DepotId. Steam Guard may prompt for confirmation."
& $resolvedSteamCmd +login $SteamUser +run_app_build $appScript +quit
if ($LASTEXITCODE -ne 0) { throw "steamcmd upload failed with exit code $LASTEXITCODE." }
Write-Output "Upload completed. Confirm the build and branch in Steamworks before granting tester access."
