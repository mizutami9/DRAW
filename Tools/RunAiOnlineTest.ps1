param(
    [string]$GameExe = ".\Builds\NICO DRAW AI Test\NICO DRAW.exe",
    [ValidateRange(2, 4)]
    [int]$Players = 4,
    [string]$Stage = "1-1",
    [ValidateSet("Direct", "EOS")]
    [string]$Transport = "Direct",
    [ValidateRange(1024, 65535)]
    [int]$Port = 18777,
    [ValidateRange(1, 120)]
    [int]$TimeoutMinutes = 12,
    [switch]$KeepWindows
)

$ErrorActionPreference = "Stop"
$resolvedExe = (Resolve-Path -LiteralPath $GameExe).Path
$projectRoot = Split-Path -Parent $PSScriptRoot
$runId = Get-Date -Format "yyyyMMdd-HHmmss"
$runDirectory = Join-Path $projectRoot "Temp\AiOnlineTests\$runId"
$coordinationPath = Join-Path $runDirectory "eos-room-code.txt"
New-Item -ItemType Directory -Force -Path $runDirectory | Out-Null

if ($Transport -eq "Direct") {
    $listener = Get-NetTCPConnection -State Listen -LocalPort $Port -ErrorAction SilentlyContinue
    if ($null -ne $listener) {
        throw "Port $Port is already in use."
    }
}

$common = @(
    "-pico-ai-test",
    "-pico-ai-run-id=$runId",
    "-pico-ai-report-dir=`"$runDirectory`"",
    "-pico-ai-coordination=`"$coordinationPath`"",
    "-pico-regression-backend=$($Transport.ToLowerInvariant())",
    "-pico-regression-port=$Port",
    "-pico-regression-players=$Players",
    "-pico-regression-stage=$Stage",
    "-pico-debug-no-time-limit",
    "-screen-width", "960",
    "-screen-height", "540",
    "-screen-fullscreen", "0"
)

$started = New-Object System.Collections.Generic.List[System.Diagnostics.Process]
$originalTemp = $env:TEMP
$originalTmp = $env:TMP
$originalLocalAppData = $env:LOCALAPPDATA
$originalAppData = $env:APPDATA
$originalUserProfile = $env:USERPROFILE
try {
    for ($index = 1; $index -le $Players; $index++) {
        $profileDirectory = Join-Path $runDirectory "profile-$index"
        New-Item -ItemType Directory -Force -Path $profileDirectory | Out-Null
        # EOS Device ID and SDK cache must be unique per process on one PC.
        $env:TEMP = $profileDirectory
        $env:TMP = $profileDirectory
        $env:LOCALAPPDATA = Join-Path $profileDirectory "LocalAppData"
        $env:APPDATA = Join-Path $profileDirectory "AppData"
        $env:USERPROFILE = $profileDirectory
        New-Item -ItemType Directory -Force -Path $env:LOCALAPPDATA, $env:APPDATA | Out-Null
        $role = if ($index -eq 1) { "host" } else { "client" }
        $name = if ($index -eq 1) { "AI1" } else { "AI$index" }
        $arguments = $common + @(
            "-pico-ai-slot=$($index - 1)",
            "-pico-eos-cache-dir=`"$profileDirectory`"",
            "-pico-regression-role=$role",
            "-pico-regression-name=$name",
            "-logFile", (Join-Path $runDirectory "client-$index-unity.log")
        )
        $process = Start-Process -FilePath $resolvedExe -ArgumentList $arguments -PassThru
        $started.Add($process)
        if ($index -eq 1) { Start-Sleep -Milliseconds 700 }
        else { Start-Sleep -Milliseconds 250 }
    }
}
finally {
    $env:TEMP = $originalTemp
    $env:TMP = $originalTmp
    $env:LOCALAPPDATA = $originalLocalAppData
    $env:APPDATA = $originalAppData
    $env:USERPROFILE = $originalUserProfile
}

$deadline = (Get-Date).AddMinutes($TimeoutMinutes)
$timedOut = $false
while ((Get-Date) -lt $deadline) {
    $doneFiles = @(Get-ChildItem -LiteralPath $runDirectory -Filter "client-*.done" -ErrorAction SilentlyContinue)
    if ($doneFiles.Count -ge $Players) { break }
    $unexpectedExit = @($started | Where-Object { $_.HasExited }).Count
    if ($unexpectedExit -gt 0) { break }
    Start-Sleep -Seconds 1
}
if (@(Get-ChildItem -LiteralPath $runDirectory -Filter "client-*.done" -ErrorAction SilentlyContinue).Count -lt $Players) {
    $timedOut = $true
}

$reports = @()
Get-ChildItem -LiteralPath $runDirectory -Filter "client-*.json" -ErrorAction SilentlyContinue |
    Where-Object { $_.Name -notlike "*-unity.log" } |
    ForEach-Object {
        try { $reports += (Get-Content -Raw -Encoding UTF8 $_.FullName | ConvertFrom-Json) }
        catch { }
    }

$desyncs = 0
if ($reports.Count -gt 1) {
    for ($slot = 0; $slot -lt $Players; $slot++) {
        $positions = @()
        foreach ($report in $reports) {
            $seen = @($report.players | Where-Object { $_.slot -eq $slot } | Select-Object -First 1)
            if ($seen.Count -gt 0) { $positions += $seen[0].position }
        }
        for ($a = 0; $a -lt $positions.Count; $a++) {
            for ($b = $a + 1; $b -lt $positions.Count; $b++) {
                $dx = [double]$positions[$a].x - [double]$positions[$b].x
                $dy = [double]$positions[$a].y - [double]$positions[$b].y
                if ([Math]::Sqrt($dx * $dx + $dy * $dy) -gt 2.5) { $desyncs++ }
            }
        }
    }
}

$exceptionCount = [int](($reports | Measure-Object -Property exceptions -Sum).Sum)
$physicsCount = [int](($reports | Measure-Object -Property suspiciousPhysicsEvents -Sum).Sum)
$deathCount = [int](($reports | Measure-Object -Property deaths -Sum).Sum)
$retryCount = [int](($reports | Measure-Object -Property retries -Sum).Sum)
$reportedIds = @($reports | Where-Object { -not [string]::IsNullOrWhiteSpace($_.playerId) } | ForEach-Object { $_.playerId })
$identityCollisions = [Math]::Max(0, $reportedIds.Count - @($reportedIds | Select-Object -Unique).Count)
$clearSeconds = [double](($reports | Measure-Object -Property elapsedSeconds -Maximum).Maximum)
$allPassed = !$timedOut -and $reports.Count -eq $Players -and
    @($reports | Where-Object { $_.result -ne "PASS" }).Count -eq 0
$result = if ($allPassed -and $exceptionCount -eq 0 -and $desyncs -eq 0 -and $identityCollisions -eq 0) { "PASS" } else { "FAIL" }

$summary = [ordered]@{
    runId = $runId
    stage = $Stage
    players = $Players
    transport = $Transport
    result = $result
    clearTimeSeconds = [Math]::Round($clearSeconds, 2)
    deaths = $deathCount
    retries = $retryCount
    networkDesync = $desyncs
    identityCollisions = $identityCollisions
    exceptions = $exceptionCount
    suspiciousPhysicsEvents = $physicsCount
    timedOut = $timedOut
    clients = $reports
}
$summary | ConvertTo-Json -Depth 12 | Set-Content -Encoding UTF8 (Join-Path $runDirectory "report.json")

$clientLines = for ($index = 0; $index -lt $Players; $index++) {
    $client = @($reports | Where-Object { $_.slot -eq $index } | Select-Object -First 1)
    if ($client.Count -gt 0) { "Player$($index + 1): $($client[0].result)" }
    else { "Player$($index + 1): NO REPORT" }
}
$markdown = @"
# NICO DRAW AI Online Test

Stage: $Stage  
Players: $Players  
Transport: $Transport  
Result: $result

Clear Time: $([Math]::Round($clearSeconds, 2))s  
Deaths: $deathCount  
Retries: $retryCount

Network desync: $desyncs  
Identity collisions: $identityCollisions  
Exceptions: $exceptionCount  
Suspicious physics events: $physicsCount

$($clientLines -join "  `n")

Artifacts: $runDirectory
"@
$markdown | Set-Content -Encoding UTF8 (Join-Path $runDirectory "report.md")
Write-Host $markdown

if (!$KeepWindows -or !$allPassed) {
    foreach ($process in $started) {
        if (!$process.HasExited) { Stop-Process -Id $process.Id -Force -ErrorAction SilentlyContinue }
    }
}

if ($result -ne "PASS") { exit 1 }
