param(
    [Parameter(Mandatory = $true)][string]$SteamUser,
    [string]$SteamCmdPath = ".\sdk\tools\ContentBuilder\builder\steamcmd.exe",
    [string]$BuildPath = ".\Builds\NICO DRAW Steam Demo",
    [string]$Description = "NICO DRAW Steam Demo",
    [string]$SetLiveBranch = ""
)

$ErrorActionPreference = "Stop"
$projectRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot "..\..")).Path
if (-not $PSBoundParameters.ContainsKey("SteamCmdPath")) {
    $SteamCmdPath = Join-Path $projectRoot "sdk\tools\ContentBuilder\builder\steamcmd.exe"
}
if (-not $PSBoundParameters.ContainsKey("BuildPath")) {
    $BuildPath = Join-Path $projectRoot "Builds\NICO DRAW Steam Demo"
}
$uploadScript = Join-Path $PSScriptRoot "UploadSteamPlaytest.ps1"
& $uploadScript `
    -AppId 5231000 `
    -DepotId 5231001 `
    -SteamCmdPath $SteamCmdPath `
    -SteamUser $SteamUser `
    -BuildPath $BuildPath `
    -Description $Description `
    -SetLiveBranch $SetLiveBranch
