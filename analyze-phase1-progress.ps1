#!/usr/bin/env pwsh

$lines = @(Get-Content temp/rebuild-after-phase1-partial.txt)
$unitTests = $lines -join "`n"

$idisp001 = @([regex]::Matches($unitTests, "tests[^\n:]*UnitTests[^\n]*\.cs\([^)]+\)[^\n]*: warning IDISP001")).Count
$idisp003 = @([regex]::Matches($unitTests, "tests[^\n:]*UnitTests[^\n]*\.cs\([^)]+\)[^\n]*: warning IDISP003")).Count
$idisp004 = @([regex]::Matches($unitTests, "tests[^\n:]*UnitTests[^\n]*\.cs\([^)]+\)[^\n]*: warning IDISP004")).Count
$idisp006 = @([regex]::Matches($unitTests, "tests[^\n:]*UnitTests[^\n]*\.cs\([^)]+\)[^\n]*: warning IDISP006")).Count
$xunit = @([regex]::Matches($unitTests, "tests[^\n:]*UnitTests[^\n]*\.cs\([^)]+\)[^\n]*: warning xUnit[0-9]{4}")).Count

$total = $idisp001 + $idisp003 + $idisp004 + $idisp006
$target = [math]::Ceiling($total / 2)

Write-Host "IDISP001: $idisp001"
Write-Host "IDISP003: $idisp003"
Write-Host "IDISP004: $idisp004"
Write-Host "IDISP006: $idisp006"
Write-Host "xUnit*: $xunit"
Write-Host ""
Write-Host "Phase 1 Progress:"
Write-Host "Total current: $total"
Write-Host "Target (50% reduction): $target"
Write-Host "Need to reduce: $($total - $target)"
