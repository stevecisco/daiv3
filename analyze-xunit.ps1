#!/usr/bin/env pwsh

$lines = @(Get-Content temp/rebuild-phase1-complete.txt)
$unitTests = $lines -join "`n"

# Extract xUnit warnings
$xunitMatches = [regex]::Matches($unitTests, "tests[^\n:]*UnitTests([^\n]*\.cs)\([^)]+\)[^\n]*: warning (xUnit[0-9]{4})")

$xunitWarnings = @{}
foreach ($match in $xunitMatches) {
    $file = $match.Groups[1].Value
    $code = $match.Groups[2].Value
    $key = $file
    
    if (-not $xunitWarnings.$key) {
        $xunitWarnings.$key = @{}
    }
    
    if (-not $xunitWarnings.$key.$code) {
        $xunitWarnings.$key.$code = 0
    }
    
    $xunitWarnings.$key.$code++
}

# Sort by total warnings
$sorted = $xunitWarnings.GetEnumerator() | Sort-Object { $_.Value.Values | Measure-Object -Sum | Select-Object -ExpandProperty Sum } -Descending

Write-Host "xUnit warnings in UnitTests:" -ForegroundColor Yellow
$sorted | ForEach-Object {
    $total = $_.Value.Values | Measure-Object -Sum | Select-Object -ExpandProperty Sum
    Write-Host "`n$($_.Key): $total warnings"
    $_.Value.GetEnumerator() | Sort-Object Value -Descending | ForEach-Object {
        Write-Host "  $($_.Key): $($_.Value)"
    }
}

# Summary
$totalXunit = $xunitWarnings.Values | ForEach-Object { $_.Values | Measure-Object -Sum | Select-Object -ExpandProperty Sum } | Measure-Object -Sum | Select-Object -ExpandProperty Sum
Write-Host "`n`nTotal xUnit warnings: $totalXunit" -ForegroundColor Cyan
