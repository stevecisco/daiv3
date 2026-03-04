#!/usr/bin/env pwsh

$lines = @(Get-Content temp/rebuild-after-phase1-partial.txt)
$unitTests = $lines -join "`n"

# Extract test files and their warning counts 
$fileMatches = [regex]::Matches($unitTests, "tests[^\n:]*UnitTests([^\n]*\.cs)\([^)]+\)[^\n]*: warning (IDISP00[1346]|xUnit[0-9]{4})")

$fileWarnings = @{}
foreach ($match in $fileMatches) {
    $file = $match.Groups[1].Value
    $code = $match.Groups[2].Value
    $key = $file
    
    if (-not $fileWarnings.$key) {
        $fileWarnings.$key = @{}
    }
    
    if (-not $fileWarnings.$key.$code) {
        $fileWarnings.$key.$code = 0
    }
    
    $fileWarnings.$key.$code++
}

# Sort by total warnings
$sorted = $fileWarnings.GetEnumerator() | Sort-Object { $_.Value.Values | Measure-Object -Sum | Select-Object -ExpandProperty Sum } -Descending

Write-Host "Top test files by IDISP/xUnit warnings (after Phase 1 partial):" -ForegroundColor Yellow
$sorted | Select-Object -First 20 | ForEach-Object {
    $total = $_.Value.Values | Measure-Object -Sum | Select-Object -ExpandProperty Sum
    Write-Host "`n$($_.Key): $total warnings"
    $_.Value.GetEnumerator() | Sort-Object Value -Descending | ForEach-Object {
        Write-Host "  $($_.Key): $($_.Value)"
    }
}

# Calculate how many more we need
$totalRemaining = $fileWarnings.Values | ForEach-Object { $_.Values | Measure-Object -Sum | Select-Object -ExpandProperty Sum } | Measure-Object -Sum | Select-Object -ExpandProperty Sum
Write-Host "`n`nTotal IDISP/xUnit warnings remaining: $totalRemaining" -ForegroundColor Cyan
Write-Host "Target (50% of 1044 = 522): Need to suppress $(1044 - 522) = 522 total"
Write-Host "So far suppressed: $(1044 - $totalRemaining)"
Write-Host "Still need: $(522 - (1044 - $totalRemaining))" -ForegroundColor Yellow
