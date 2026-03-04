#!/usr/bin/env pwsh

$lines = @(Get-Content temp/current-warnings-full.txt)

# Find IDISP025 warnings
$idisp025 = $lines | Where-Object { $_ -match "warning IDISP025:" }

Write-Host "IDISP025 Warnings (Class should be sealed): $($idisp025.Count)" -ForegroundColor Yellow
Write-Host "=" * 70

# Extract file paths and counts
$fileMatches = @{}
foreach ($line in $idisp025) {
    if ($line -match "([^\\]+\.cs)\(\d+,\d+\):\s+warning IDISP025") {
        $file = $matches[1]
        if (-not $fileMatches.$file) { $fileMatches.$file = 0 }
        $fileMatches.$file++
    }
}

$fileMatches.GetEnumerator() | Sort-Object Value -Descending | ForEach-Object {
    Write-Host "$($_.Key): $($_.Value) occurrences"
}
