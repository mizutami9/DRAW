param(
    [string]$GameExe = ".\Builds\NICO DRAW AI Test\NICO DRAW.exe",
    [ValidateRange(1024, 65530)]
    [int]$BasePort = 19640,
    [ValidateRange(1, 10)]
    [int]$TimeoutMinutes = 3,
    [string]$Stage = "1-1",
    [string]$CaseName = ""
)

$ErrorActionPreference = "Stop"
$resolvedExe = (Resolve-Path -LiteralPath $GameExe).Path
$projectRoot = Split-Path -Parent $PSScriptRoot
$runId = Get-Date -Format "yyyyMMdd-HHmmss"
$rootDirectory = Join-Path $projectRoot "Temp\LocalOnlineTopologyTests\$runId"
New-Item -ItemType Directory -Force -Path $rootDirectory | Out-Null

$cases = @(
    [ordered]@{ Name = "keyboard-pad-local2-plus-online2"; Layouts = @("keyboard-gamepad", "gamepad", "keyboard") },
    [ordered]@{ Name = "pad-pad-local2-plus-online2"; Layouts = @("gamepad-gamepad", "gamepad", "gamepad") },
    [ordered]@{ Name = "pad-p1-plus-online3"; Layouts = @("gamepad", "gamepad", "gamepad", "gamepad") },
    [ordered]@{ Name = "pad-keyboard-local2-plus-online2"; Layouts = @("gamepad-keyboard", "gamepad", "gamepad") },
    [ordered]@{ Name = "two-local-pairs-online"; Layouts = @("keyboard-gamepad", "gamepad-gamepad") },
    [ordered]@{ Name = "joining-keyboard-pad-local2"; Layouts = @("gamepad", "keyboard-gamepad", "keyboard") },
    [ordered]@{ Name = "joining-pad-keyboard-local2"; Layouts = @("gamepad", "gamepad-keyboard", "gamepad") }
)
if (![string]::IsNullOrWhiteSpace($CaseName)) {
    $cases = @($cases | Where-Object Name -eq $CaseName)
    if ($cases.Count -eq 0) { throw "Unknown topology case: $CaseName" }
}

$caseSummaries = New-Object System.Collections.Generic.List[object]
for ($caseIndex = 0; $caseIndex -lt $cases.Count; $caseIndex++) {
    $case = $cases[$caseIndex]
    $caseDirectory = Join-Path $rootDirectory $case.Name
    New-Item -ItemType Directory -Force -Path $caseDirectory | Out-Null
    $port = $BasePort + $caseIndex
    if ($null -ne (Get-NetTCPConnection -State Listen -LocalPort $port -ErrorAction SilentlyContinue)) {
        throw "Port $port is already in use."
    }

    $common = @(
        "-pico-ai-test",
        "-pico-local-online-test",
        "-pico-ai-run-id=$runId",
        "-pico-ai-report-dir=`"$caseDirectory`"",
        "-pico-topology-case=$($case.Name)",
        "-pico-regression-backend=direct",
        "-pico-regression-port=$port",
        "-pico-regression-players=4",
        "-pico-regression-stage=$Stage",
        "-pico-debug-no-time-limit",
        "-screen-width", "800", "-screen-height", "450", "-screen-fullscreen", "0"
    )

    $started = New-Object System.Collections.Generic.List[System.Diagnostics.Process]
    for ($index = 0; $index -lt $case.Layouts.Count; $index++) {
        $role = if ($index -eq 0) { "host" } else { "client" }
        $arguments = $common + @(
            "-pico-ai-client-id=$index",
            "-pico-local-layout=$($case.Layouts[$index])",
            "-pico-regression-role=$role",
            "-pico-regression-name=$($case.Name)-C$($index + 1)",
            "-logFile", (Join-Path $caseDirectory "client-$index-unity.log")
        )
        $started.Add((Start-Process -FilePath $resolvedExe -ArgumentList $arguments -PassThru -WindowStyle Hidden))
        Start-Sleep -Milliseconds $(if ($index -eq 0) { 700 } else { 250 })
    }

    $deadline = (Get-Date).AddMinutes($TimeoutMinutes)
    while ((Get-Date) -lt $deadline) {
        $done = @(Get-ChildItem -LiteralPath $caseDirectory -Filter "topology-client-*.done" -ErrorAction SilentlyContinue)
        if ($done.Count -ge $case.Layouts.Count) { break }
        if (@($started | Where-Object HasExited).Count -gt 0) { break }
        Start-Sleep -Seconds 1
    }

    $reports = @(Get-ChildItem -LiteralPath $caseDirectory -Filter "topology-client-*.json" -ErrorAction SilentlyContinue | ForEach-Object {
        try { Get-Content -Raw -Encoding UTF8 $_.FullName | ConvertFrom-Json } catch { }
    })
    $logErrors = @(Get-ChildItem -LiteralPath $caseDirectory -Filter "*-unity.log" -ErrorAction SilentlyContinue |
        Select-String -Pattern "Exception:|NullReferenceException|ArgumentException|MissingReferenceException")
    $passed = $reports.Count -eq $case.Layouts.Count -and
        @($reports | Where-Object result -ne "PASS").Count -eq 0 -and $logErrors.Count -eq 0
    $caseSummaries.Add([ordered]@{
        name = $case.Name
        layouts = $case.Layouts
        result = if ($passed) { "PASS" } else { "FAIL" }
        reports = $reports
        logErrors = @($logErrors | ForEach-Object Line)
    })
    foreach ($process in $started) {
        if (!$process.HasExited) { Stop-Process -Id $process.Id -Force -ErrorAction SilentlyContinue }
    }
}

$allPassed = @($caseSummaries | Where-Object result -ne "PASS").Count -eq 0
$summary = [ordered]@{
    runId = $runId
    executable = $resolvedExe
    stage = $Stage
    result = if ($allPassed) { "PASS" } else { "FAIL" }
    cases = $caseSummaries
}
$summary | ConvertTo-Json -Depth 12 | Set-Content -Encoding UTF8 (Join-Path $rootDirectory "report.json")

$lines = foreach ($case in $caseSummaries) {
    "- $($case.name): $($case.result) [$($case.layouts -join ', ')]"
    foreach ($report in @($case.reports)) {
        "  - client $($report.clientInstance + 1) ($($report.layout)): $($report.result), $(@($report.passedChecks).Count) passed / $(@($report.failedChecks).Count) failed"
        foreach ($failure in @($report.failedChecks)) { "    - FAIL: $failure" }
    }
    foreach ($errorLine in @($case.logErrors)) { "  - LOG: $errorLine" }
}
$markdown = @"
# Local + Online Topology Regression

Stage: $Stage  
Result: $($summary.result)

$($lines -join "`n")

Artifacts: $rootDirectory
"@
$markdown | Set-Content -Encoding UTF8 (Join-Path $rootDirectory "report.md")
Write-Host $markdown
if (!$allPassed) { exit 1 }
