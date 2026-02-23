@echo off
setlocal

echo Running all unit tests...
echo.

dotnet test "%~dp0tests\unit\Daiv3.UnitTests\Daiv3.UnitTests.csproj" --framework net10.0-windows10.0.26100 --verbosity normal %*
