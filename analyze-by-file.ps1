#!/usr/bin/env pwsh

$lines = @(Get-Content temp/full-rebuild-fresh.txt)
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

Write-Host "Top test files by IDISP/xUnit warnings:" -ForegroundColor Yellow
$sorted | Select-Object -First 20 | ForEach-Object {
    $total = $_.Value.Values | Measure-Object -Sum | Select-Object -ExpandProperty Sum
    Write-Host "`n$($_.Key): $total warnings"
    $_.Value.GetEnumerator() | Sort-Object Value -Descending | ForEach-Object {
        Write-Host "  $($_.Key): $($_.Value)"
    }
}
