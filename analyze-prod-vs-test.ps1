#!/usr/bin/env pwsh

$lines = @(Get-Content temp/current-warnings-full.txt)

# Separate warnings by category
$prodWarnings = $lines | Where-Object { $_ -match "warning" -and $_ -match "\\src\\" }
$testWarnings = $lines | Where-Object { $_ -match "warning" -and $_ -match "\\tests\\" }

Write-Host "Warning Distribution:" -ForegroundColor Yellow
Write-Host "=" * 50
Write-Host "Production code (src/): $($prodWarnings.Count) warnings"
Write-Host "Test code (tests/): $($testWarnings.Count) warnings"
Write-Host "Total: $(($prodWarnings.Count + $testWarnings.Count))"

# Analyze production warnings by code
$prodContent = $prodWarnings -join "`n"
$prodMatches = [regex]::Matches($prodContent, "warning ([A-Z]+[0-9]{3,4}):")
$prodCounts = @{}
foreach ($match in $prodMatches) {
    $code = $match.Groups[1].Value
    if (-not $prodCounts.$code) { $prodCounts.$code = 0 }
    $prodCounts.$code++
}

Write-Host "`n`nProduction Code - Top 10 Warning Codes:" -ForegroundColor Cyan
$prodCounts.GetEnumerator() | Sort-Object Value -Descending | Select-Object -First 10 | Format-Table @{Label="Code";Expression={$_.Key}}, @{Label="Count";Expression={$_.Value}} -AutoSize

# Analyze test warnings by code
$testContent = $testWarnings -join "`n"
$testMatches = [regex]::Matches($testContent, "warning ([A-Z]+[0-9]{3,4}):")
$testCounts = @{}
foreach ($match in $testMatches) {
    $code = $match.Groups[1].Value
    if (-not $testCounts.$code) { $testCounts.$code = 0 }
    $testCounts.$code++
}

Write-Host "`n`nTest Code - Top 10 Warning Codes:" -ForegroundColor Cyan
$testCounts.GetEnumerator() | Sort-Object Value -Descending | Select-Object -First 10 | Format-Table @{Label="Code";Expression={$_.Key}}, @{Label="Count";Expression={$_.Value}} -AutoSize
