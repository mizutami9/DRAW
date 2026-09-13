param(
    [string]$GameExe = ".\Builds\NICO DRAW Demo Test\NICO DRAW.exe",
    [ValidateRange(2, 4)]
    [int]$Players = 4,
    [string]$Stage = "14-3",
    [ValidateRange(1024, 65535)]
    [int]$Port = 19433,
    [ValidateRange(1, 10)]
    [int]$TimeoutMinutes = 3,
    [switch]$ConcurrentRedraw
)

$ErrorActionPreference = "Stop"
$resolvedExe = (Resolve-Path -LiteralPath $GameExe).Path
$projectRoot = Split-Path -Parent $PSScriptRoot
$runId = Get-Date -Format "yyyyMMdd-HHmmss"
$runDirectory = Join-Path $projectRoot "Temp\DemoMultiplayerSmoke\$runId"
New-Item -ItemType Directory -Force -Path $runDirectory | Out-Null

if ($null -ne (Get-NetTCPConnection -State Listen -LocalPort $Port -ErrorAction SilentlyContinue)) {
    throw "Port $Port is already in use."
}

$common = @(
    "-pico-ai-test",
    "-pico-multiplayer-smoke",
    "-pico-ai-run-id=$runId",
    "-pico-ai-report-dir=`"$runDirectory`"",
    "-pico-regression-backend=direct",
    "-pico-regression-port=$Port",
    "-pico-regression-players=$Players",
    "-pico-regression-stage=$Stage",
    "-pico-debug-no-time-limit",
    "-screen-width", "800", "-screen-height", "450", "-screen-fullscreen", "0"
)
if ($ConcurrentRedraw) { $common += "-pico-smoke-concurrent-redraw" }

$started = New-Object System.Collections.Generic.List[System.Diagnostics.Process]
for ($index = 1; $index -le $Players; $index++) {
    $role = if ($index -eq 1) { "host" } else { "client" }
    $name = if ($index -eq 1) { "Host" } else { "P$index" }
    $arguments = $common + @(
        "-pico-ai-slot=$($index - 1)",
        "-pico-regression-role=$role",
        "-pico-regression-name=$name",
        "-logFile", (Join-Path $runDirectory "client-$index-unity.log")
    )
    $started.Add((Start-Process -FilePath $resolvedExe -ArgumentList $arguments -PassThru))
    Start-Sleep -Milliseconds $(if ($index -eq 1) { 700 } else { 250 })
}

$deadline = (Get-Date).AddMinutes($TimeoutMinutes)
while ((Get-Date) -lt $deadline) {
    if (@(Get-ChildItem -LiteralPath $runDirectory -Filter "client-*.done" -ErrorAction SilentlyContinue).Count -ge $Players) { break }
    if (@($started | Where-Object HasExited).Count -gt 0) { break }
    Start-Sleep -Seconds 1
}

$reports = @(Get-ChildItem -LiteralPath $runDirectory -Filter "client-*.json" -ErrorAction SilentlyContinue | ForEach-Object {
    try { Get-Content -Raw -Encoding UTF8 $_.FullName | ConvertFrom-Json } catch { }
})
$logErrors = @(Get-ChildItem -LiteralPath $runDirectory -Filter "*-unity.log" | Select-String -Pattern "Exception:|NullReferenceException|ArgumentException|MissingReferenceException|\[PICO INTEGRITY\]")
$maxPositionDesync = 0.0
$speciesMismatches = 0
for ($slot = 0; $slot -lt $Players; $slot++) {
    $observations = @($reports | ForEach-Object { $_.players | Where-Object slot -eq $slot | Select-Object -First 1 })
    $species = @($observations | ForEach-Object species | Select-Object -Unique)
    if ($species.Count -gt 1) { $speciesMismatches++ }
    for ($a = 0; $a -lt $observations.Count; $a++) {
        for ($b = $a + 1; $b -lt $observations.Count; $b++) {
            $dx = [double]$observations[$a].position.x - [double]$observations[$b].position.x
            $dy = [double]$observations[$a].position.y - [double]$observations[$b].position.y
            $distance = [Math]::Sqrt($dx * $dx + $dy * $dy)
            if ($distance -gt $maxPositionDesync) { $maxPositionDesync = $distance }
        }
    }
}
$passed = $reports.Count -eq $Players -and @($reports | Where-Object result -ne "PASS").Count -eq 0 -and
    $logErrors.Count -eq 0 -and $maxPositionDesync -le 0.75 -and $speciesMismatches -eq 0
$summary = [ordered]@{
    runId = $runId
    executable = $resolvedExe
    stage = $Stage
    players = $Players
    transport = "Direct TCP"
    redrawMode = if ($ConcurrentRedraw) { "Concurrent" } else { "Staggered" }
    result = if ($passed) { "PASS" } else { "FAIL" }
    maxPositionDesync = [Math]::Round($maxPositionDesync, 4)
    speciesMismatches = $speciesMismatches
    reports = $reports
    logErrors = @($logErrors | ForEach-Object Line)
}
$summary | ConvertTo-Json -Depth 12 | Set-Content -Encoding UTF8 (Join-Path $runDirectory "report.json")

$lines = foreach ($report in ($reports | Sort-Object slot)) {
    "Player$([int]$report.slot + 1): $($report.result) - $(@($report.passedChecks).Count) passed / $(@($report.failedChecks).Count) failed"
    foreach ($failure in @($report.failedChecks)) { "  FAIL: $failure" }
}
$markdown = @"
# Demo Multiplayer Smoke Test

Stage: $Stage  
Players: $Players  
Transport: Direct TCP (four independent demo client processes)  
Redraw mode: $(if ($ConcurrentRedraw) { "Concurrent" } else { "Staggered" })  
Result: $(if ($passed) { "PASS" } else { "FAIL" })

$($lines -join "`n")

Unity log errors: $($logErrors.Count)  
Maximum position disagreement: $([Math]::Round($maxPositionDesync, 4))  
Species mismatches: $speciesMismatches  
Artifacts: $runDirectory
"@
$markdown | Set-Content -Encoding UTF8 (Join-Path $runDirectory "report.md")
Write-Host $markdown

foreach ($process in $started) {
    if (!$process.HasExited) { Stop-Process -Id $process.Id -Force -ErrorAction SilentlyContinue }
}
if (!$passed) { exit 1 }
