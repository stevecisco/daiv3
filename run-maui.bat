@echo off
setlocal

dotnet run --project "%~dp0src\Daiv3.App.Maui\Daiv3.App.Maui.csproj" --framework net10.0-windows10.0.26100
