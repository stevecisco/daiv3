@echo off
setlocal

echo ================================================================================
echo Running All Unit and Integration Tests
echo ================================================================================
echo.

REM Run all tests in the solution (all target frameworks)
dotnet test "%~dp0Daiv3.FoundryLocal.slnx" --verbosity normal %*

echo.
echo ================================================================================
echo Test Run Complete
echo ================================================================================
