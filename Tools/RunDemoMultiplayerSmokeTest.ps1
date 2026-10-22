param(
    [string]$GameExe = ".\Builds\NICO DRAW Demo Test\NICO DRAW.exe",
    [ValidateRange(2, 4)]
    [int]$Players = 4,
    [string]$Stage = "14-3",
    [ValidateSet("Direct", "EOS")]
    [string]$Transport = "Direct",
    [ValidateRange(1024, 65535)]
    [int]$Port = 19433,
    [ValidateRange(1, 180)]
    [int]$TimeoutMinutes = 3,
    [ValidateRange(0, 7200)]
    [int]$SoakSeconds = 0,
    [int]$Seed = 1337,
    [switch]$ConcurrentRedraw,
    [switch]$ReadyRoomSmoke,
    [switch]$CatBombSmoke,
    [switch]$SkipRedraw,
    [switch]$LateJoin,
    [ValidateRange(0.1, 10.0)]
    [double]$PositionTolerance = 1.75,
    [ValidateRange(0, 1000)]
    [int]$LatencyMs = 0,
    [ValidateRange(0, 50)]
    [int]$LossPercent = 0
)

$ErrorActionPreference = "Stop"
$resolvedExe = (Resolve-Path -LiteralPath $GameExe).Path
$projectRoot = Split-Path -Parent $PSScriptRoot
$runId = Get-Date -Format "yyyyMMdd-HHmmss"
$runDirectory = Join-Path $projectRoot "Temp\DemoMultiplayerSmoke\$runId"
$coordinationPath = Join-Path $runDirectory "eos-room-code.txt"
New-Item -ItemType Directory -Force -Path $runDirectory | Out-Null

if ($LateJoin -and $Players -lt 3) {
    throw "LateJoin requires at least 3 players so the stage can start before the final client joins."
}

if ($Transport -eq "Direct" -and $null -ne (Get-NetTCPConnection -State Listen -LocalPort $Port -ErrorAction SilentlyContinue)) {
    throw "Port $Port is already in use."
}

$common = @(
    "-pico-ai-test",
    "-pico-multiplayer-smoke",
    "-pico-ai-run-id=$runId",
    "-pico-ai-report-dir=`"$runDirectory`"",
    "-pico-ai-coordination=`"$coordinationPath`"",
    "-pico-regression-backend=$($Transport.ToLowerInvariant())",
    "-pico-regression-port=$Port",
    "-pico-regression-players=$Players",
    "-pico-regression-stage=$Stage",
    "-pico-net-latency-ms=$LatencyMs",
    "-pico-net-loss-percent=$LossPercent",
    "-pico-net-monitor",
    "-pico-debug-no-time-limit",
    "-screen-width", "800", "-screen-height", "450", "-screen-fullscreen", "0"
)
if ($ConcurrentRedraw) { $common += "-pico-smoke-concurrent-redraw" }
if ($ReadyRoomSmoke) { $common += "-pico-ready-room-smoke" }
if ($CatBombSmoke) { $common += "-pico-cat-bomb-smoke" }
if ($SkipRedraw) { $common += "-pico-smoke-skip-redraw" }
if ($LateJoin) { $common += "-pico-regression-start-players=$($Players - 1)" }
if ($SoakSeconds -gt 0) {
    $common += "-pico-random-soak"
    $common += "-pico-soak-seconds=$SoakSeconds"
    $common += "-pico-soak-seed=$Seed"
}

$modeLabel = if ($SoakSeconds -gt 0) {
    "Random soak ($SoakSeconds sec, seed $Seed)"
} elseif ($ReadyRoomSmoke) {
    "9-3 Ready Room"
} elseif ($CatBombSmoke) {
    "Cat scratch bomb"
} elseif ($LateJoin -and $SkipRedraw) {
    "Late join / gameplay only"
} elseif ($LateJoin) {
    "Late join"
} elseif ($SkipRedraw) {
    "Gameplay only"
} elseif ($ConcurrentRedraw) {
    "Concurrent"
} else {
    "Staggered"
}

$started = New-Object System.Collections.Generic.List[System.Diagnostics.Process]
$originalTemp = $env:TEMP
$originalTmp = $env:TMP
$originalLocalAppData = $env:LOCALAPPDATA
$originalAppData = $env:APPDATA
$originalUserProfile = $env:USERPROFILE
try {
for ($index = 1; $index -le $Players; $index++) {
    if ($LateJoin -and $index -eq $Players) { Start-Sleep -Seconds 7 }
    $role = if ($index -eq 1) { "host" } else { "client" }
    $name = if ($index -eq 1) { "Host" } else { "P$index" }
    $profileDirectory = Join-Path $runDirectory "profile-$index"
    if ($Transport -eq "EOS") {
        New-Item -ItemType Directory -Force -Path $profileDirectory | Out-Null
        $env:TEMP = $profileDirectory
        $env:TMP = $profileDirectory
        $env:LOCALAPPDATA = Join-Path $profileDirectory "LocalAppData"
        $env:APPDATA = Join-Path $profileDirectory "AppData"
        $env:USERPROFILE = $profileDirectory
        New-Item -ItemType Directory -Force -Path $env:LOCALAPPDATA, $env:APPDATA | Out-Null
    }
    $arguments = $common + @(
        "-pico-ai-slot=$($index - 1)",
        "-pico-ai-client-id=$($index - 1)",
        "-pico-eos-cache-dir=`"$profileDirectory`"",
        "-pico-regression-role=$role",
        "-pico-regression-name=$name",
        "-logFile", (Join-Path $runDirectory "client-$index-unity.log")
    )
    $started.Add((Start-Process -FilePath $resolvedExe -ArgumentList $arguments -PassThru -WindowStyle Hidden))
    Start-Sleep -Milliseconds $(if ($index -eq 1) { 700 } else { 250 })
}
}
finally {
    $env:TEMP = $originalTemp
    $env:TMP = $originalTmp
    $env:LOCALAPPDATA = $originalLocalAppData
    $env:APPDATA = $originalAppData
    $env:USERPROFILE = $originalUserProfile
}

$minimumTimeoutMinutes = if ($SoakSeconds -gt 0) {
    [Math]::Ceiling(($SoakSeconds + 90) / 60.0)
} else { 0 }
$effectiveTimeoutMinutes = [Math]::Max($TimeoutMinutes, $minimumTimeoutMinutes)
$deadline = (Get-Date).AddMinutes($effectiveTimeoutMinutes)
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
    # CatBombSmoke deliberately overrides only each process' locally owned
    # ability profile so the test isolates scratch targeting and bomb sync.
    if (!$CatBombSmoke -and $species.Count -gt 1) { $speciesMismatches++ }
    for ($a = 0; $a -lt $observations.Count; $a++) {
        for ($b = $a + 1; $b -lt $observations.Count; $b++) {
            $dx = [double]$observations[$a].position.x - [double]$observations[$b].position.x
            $dy = [double]$observations[$a].position.y - [double]$observations[$b].position.y
            $distance = [Math]::Sqrt($dx * $dx + $dy * $dy)
            if ($distance -gt $maxPositionDesync) { $maxPositionDesync = $distance }
        }
    }
}
# Run telemetry invariant auditor across all recorded frames
$invariantViolations = 0
$invariantDetails = @()
$invariantAuditError = $null
$analyzerScript = Join-Path $PSScriptRoot "analyze_telemetry_invariants.py"
if (!(Test-Path $analyzerScript)) {
    $invariantViolations = 1
    $invariantAuditError = "Telemetry analyzer not found: $analyzerScript"
} else {
    try {
        $python = $null
        $pythonCandidates = New-Object System.Collections.Generic.List[string]
        foreach ($pythonName in @("python", "python3", "py")) {
            Get-Command $pythonName -All -ErrorAction SilentlyContinue | ForEach-Object {
                if (!$pythonCandidates.Contains($_.Source)) { $pythonCandidates.Add($_.Source) }
            }
        }
        Get-ChildItem -Path (Join-Path $env:LOCALAPPDATA "Programs\Python\Python*\python.exe") `
                -ErrorAction SilentlyContinue | ForEach-Object {
            if (!$pythonCandidates.Contains($_.FullName)) { $pythonCandidates.Add($_.FullName) }
        }
        foreach ($candidate in $pythonCandidates) {
            try {
                $null = & $candidate --version 2>&1
                if ($LASTEXITCODE -eq 0) {
                    $python = $candidate
                    break
                }
            } catch {
                continue
            }
        }
        if ($null -eq $python) { throw "Python 3 was not found (tried python, python3, and py)." }
        $auditOutput = @(& $python $analyzerScript --json $runDirectory)
        $auditExitCode = $LASTEXITCODE
        $auditJson = $auditOutput -join "`n"
        if ([string]::IsNullOrWhiteSpace($auditJson)) {
            throw "Telemetry analyzer produced no JSON (exit code $auditExitCode)."
        }
        $auditResult = $auditJson | ConvertFrom-Json -ErrorAction Stop
        if ($auditResult) {
            $first = if ($auditResult -is [array]) { $auditResult[0] } else { $auditResult }
            $invariantViolations = [int]$first.total_anomalies
            if ($first.unique_anomalies) {
                $invariantDetails = @($first.unique_anomalies | ForEach-Object {
                    "  * [$($_.type)] t=$([Math]::Round($_.time, 1))s | $($_.player) | $($_.detail)"
                })
            }
        }
        if ($auditExitCode -notin @(0, 1)) {
            throw "Telemetry analyzer failed with exit code $auditExitCode."
        }
        if ($auditExitCode -eq 1 -and $invariantViolations -eq 0) {
            throw "Telemetry analyzer reported failure without anomaly details."
        }
    } catch {
        $invariantViolations = [Math]::Max(1, $invariantViolations)
        $invariantAuditError = $_.Exception.Message
        $invariantDetails += "  * [Analyzer Failure] $invariantAuditError"
        Write-Warning "Failed to run telemetry invariant analysis: $invariantAuditError"
    }
}

$passed = $reports.Count -eq $Players -and @($reports | Where-Object result -ne "PASS").Count -eq 0 -and
    $logErrors.Count -eq 0 -and $maxPositionDesync -le $PositionTolerance -and $speciesMismatches -eq 0 -and
    $invariantViolations -eq 0
$summary = [ordered]@{
    runId = $runId
    executable = $resolvedExe
    stage = $Stage
    players = $Players
    transport = $Transport
    simulatedLatencyMs = $LatencyMs
    simulatedLossPercent = $LossPercent
    redrawMode = $modeLabel
    result = if ($passed) { "PASS" } else { "FAIL" }
    maxPositionDesync = [Math]::Round($maxPositionDesync, 4)
    positionTolerance = $PositionTolerance
    speciesMismatches = $speciesMismatches
    invariantViolations = $invariantViolations
    invariantAuditError = $invariantAuditError
    maxMeasuredRoundTripMs = [Math]::Round([double](($reports | Measure-Object -Property networkRoundTripMs -Maximum).Maximum), 2)
    simulatedPacketDrops = [int](($reports | Measure-Object -Property simulatedPacketDrops -Sum).Sum)
    networkCorrections = [int](($reports | Measure-Object -Property networkCorrections -Sum).Sum)
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
Transport: $Transport (four independent demo client processes)
Simulated network: ${LatencyMs}ms latency / ${LossPercent}% realtime packet loss
Redraw mode: $modeLabel
Result: $(if ($passed) { "PASS" } else { "FAIL" })

$($lines -join "`n")

Unity log errors: $($logErrors.Count)  
Maximum position disagreement: $([Math]::Round($maxPositionDesync, 4))  
Position tolerance: $PositionTolerance
Species mismatches: $speciesMismatches  
Telemetry invariant violations: $invariantViolations
Maximum measured RTT: $([Math]::Round([double](($reports | Measure-Object -Property networkRoundTripMs -Maximum).Maximum), 2))ms
Simulated packet drops: $([int](($reports | Measure-Object -Property simulatedPacketDrops -Sum).Sum))
Network corrections: $([int](($reports | Measure-Object -Property networkCorrections -Sum).Sum))
$(if ($invariantDetails.Count -gt 0) { "Invariant violations detail:`n$($invariantDetails -join "`n")`n" })
Artifacts: $runDirectory
"@
$markdown | Set-Content -Encoding UTF8 (Join-Path $runDirectory "report.md")
Write-Host $markdown

foreach ($process in $started) {
    if (!$process.HasExited) { Stop-Process -Id $process.Id -Force -ErrorAction SilentlyContinue }
}
if (!$passed) { exit 1 }
