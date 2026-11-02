param(
    [string]$GameExe = ".\Builds\NICO DRAW AI Test\NICO DRAW.exe",
    [ValidateSet("Direct", "EOS")]
    [string]$Transport = "Direct",
    [ValidateRange(2, 4)]
    [int]$Players = 4,
    [ValidateRange(1, 30)]
    [int]$TimeoutMinutesPerStage = 3,
    [int]$StartPort = 21000
)

$ErrorActionPreference = "Continue"
$projectRoot = Split-Path -Parent $PSScriptRoot
$runner = Join-Path $PSScriptRoot "RunDemoMultiplayerSmokeTest.ps1"
$stageFiles = Get-ChildItem -LiteralPath (Join-Path $projectRoot "Assets\Resources\Stages") -Filter "*.json"
$stages = @($stageFiles.BaseName | Sort-Object { [int]($_ -split '-')[0] }, { [int]($_ -split '-')[1] })
$results = New-Object System.Collections.Generic.List[object]

for ($index = 0; $index -lt $stages.Count; $index++) {
    $stage = $stages[$index]
    $port = $StartPort + $index
    Write-Host "[$($index + 1)/$($stages.Count)] $stage ($Transport)"
    & $runner -GameExe $GameExe -Players $Players -Stage $stage -Transport $Transport `
        -Port $port -TimeoutMinutes $TimeoutMinutesPerStage -StageLaunchSmoke
    $exitCode = $LASTEXITCODE
    $results.Add([pscustomobject]@{
        stage = $stage
        transport = $Transport
        players = $Players
        result = if ($exitCode -eq 0) { "PASS" } else { "FAIL" }
        exitCode = $exitCode
    })
}

$outputDirectory = Join-Path $projectRoot "Temp\AllStageOnlineLaunch"
New-Item -ItemType Directory -Force -Path $outputDirectory | Out-Null
$summaryPath = Join-Path $outputDirectory ("summary-{0}-{1}p-{2}.json" -f $Transport.ToLowerInvariant(), $Players, (Get-Date -Format "yyyyMMdd-HHmmss"))
$results | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath $summaryPath -Encoding UTF8
$failed = @($results | Where-Object result -ne "PASS")
Write-Host "All-stage online launch: $($results.Count - $failed.Count)/$($results.Count) PASS"
Write-Host "Summary: $summaryPath"
if ($failed.Count -gt 0) {
    Write-Host "Failed stages: $($failed.stage -join ', ')"
    exit 1
}
