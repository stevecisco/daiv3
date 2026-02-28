@echo off
setlocal

echo ================================================================================
echo Running Full Test Suite (Canonical)
echo ================================================================================
echo.

REM Canonical full-suite command. Do not pipe to Select-String/grep when validating totals.
REM This preserves the final aggregate "Test summary" line for consistent reporting.
dotnet test "%~dp0Daiv3.FoundryLocal.slnx" --nologo --verbosity minimal --logger "console;verbosity=minimal" %*

echo.
echo ================================================================================
echo Test Run Complete
echo ================================================================================
