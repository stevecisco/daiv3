@echo off
setlocal

dotnet run --project "%~dp0src\Daiv3.FoundryLocal.Management.Cli\Daiv3.FoundryLocal.Management.Cli.csproj" --framework net10.0-windows10.0.26100 -- %*
