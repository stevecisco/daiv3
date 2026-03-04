#!/usr/bin/env pwsh

$lines = @(Get-Content temp/current-warnings-full.txt)
$content = $lines -join "`n"

# Extract all warnings with their codes (handle both CS and IDISP formats)
$warningMatches = [regex]::Matches($content, "warning ([A-Z]+[0-9]{3,4}):")

$warningCounts = @{}
foreach ($match in $warningMatches) {
    $code = $match.Groups[1].Value
    
    if (-not $warningCounts.$code) {
        $warningCounts.$code = 0
    }
    
    $warningCounts.$code++
}

# Sort by count descending
$sorted = $warningCounts.GetEnumerator() | Sort-Object Value -Descending

Write-Host "Warning Distribution by Code (Top 20):" -ForegroundColor Yellow
Write-Host "=" * 50
$sorted | Select-Object -First 20 | Format-Table @{Label="Code";Expression={$_.Key}}, @{Label="Count";Expression={$_.Value}} -AutoSize

Write-Host "`n"
Write-Host "=" * 50
Write-Host "Total unique warning codes: $($warningCounts.Count)"
Write-Host "Total warnings: $(($warningCounts.Values | Measure-Object -Sum).Sum)"

# Categorize by family
Write-Host "`n`nWarning Families:" -ForegroundColor Cyan
$idisp = ($warningCounts.Keys | Where-Object { $_ -match "^IDISP" } | ForEach-Object { $warningCounts[$_] } | Measure-Object -Sum).Sum
$cs = ($warningCounts.Keys | Where-Object { $_ -match "^CS" } | ForEach-Object { $warningCounts[$_] } | Measure-Object -Sum).Sum
$nu = ($warningCounts.Keys | Where-Object { $_ -match "^NU" } | ForEach-Object { $warningCounts[$_] } | Measure-Object -Sum).Sum

Write-Host "IDISP (IDisposable): $idisp"
Write-Host "CS (Compiler): $cs"
Write-Host "NU (NuGet): $nu"
