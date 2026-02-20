# Foundry Local SDK API Inspector
# This script inspects the Foundry Local SDK assembly to discover available methods

param(
    [string]$BuildOutputPath = ".\bin\Debug\net8.0-windows10.0.26100"
)

$dllPath = Join-Path $BuildOutputPath "Microsoft.AI.Foundry.Local.WinML.dll"

Write-Host "Inspecting Foundry Local SDK..." -ForegroundColor Cyan
Write-Host "DLL Path: $dllPath" -ForegroundColor Gray
Write-Host ""

if (-not (Test-Path $dllPath)) {
    Write-Host "ERROR: DLL not found at: $dllPath" -ForegroundColor Red
    Write-Host "Make sure you have run 'dotnet build' first." -ForegroundColor Yellow
    exit 1
}

try {
    # Change to the build output directory to resolve dependencies
    Push-Location $BuildOutputPath
    
    $assembly = [System.Reflection.Assembly]::LoadFrom((Resolve-Path "Microsoft.AI.Foundry.Local.WinML.dll"))
    
    Write-Host "=== FoundryLocalManager Methods ===" -ForegroundColor Green
    $managerType = $assembly.GetType("Microsoft.AI.Foundry.Local.FoundryLocalManager")
    if ($managerType) {
        $methods = $managerType.GetMethods([System.Reflection.BindingFlags]::Public -bor [System.Reflection.BindingFlags]::Instance -bor [System.Reflection.BindingFlags]::Static) | 
            Where-Object { $_.DeclaringType.Name -eq "FoundryLocalManager" } |
            Sort-Object Name
        
        foreach ($method in $methods) {
            $params = $method.GetParameters() | ForEach-Object { "$($_.ParameterType.Name) $($_.Name)" }
            $paramStr = $params -join ", "
            Write-Host "  $($method.ReturnType.Name) $($method.Name)($paramStr)" -ForegroundColor White
        }
    }
    
    Write-Host ""
    Write-Host "=== Configuration Class Properties ===" -ForegroundColor Green
    $configType = $assembly.GetType("Microsoft.AI.Foundry.Local.Configuration")
    if ($configType) {
        $properties = $configType.GetProperties([System.Reflection.BindingFlags]::Public -bor [System.Reflection.BindingFlags]::Instance) |
            Sort-Object Name
        
        foreach ($prop in $properties) {
            Write-Host "  $($prop.PropertyType.Name) $($prop.Name)" -ForegroundColor White
        }
    }
    
    Write-Host ""
    Write-Host "=== All Public Types in Assembly ===" -ForegroundColor Green
    $types = $assembly.GetTypes() | 
        Where-Object { $_.IsPublic } | 
        Sort-Object Name
    
    foreach ($type in $types) {
        Write-Host "  $($type.FullName)" -ForegroundColor Cyan
    }
    
    Pop-Location
    
    Write-Host ""
    Write-Host "Inspection complete!" -ForegroundColor Green
    
} catch {
    Pop-Location
    Write-Host "ERROR: Failed to load assembly" -ForegroundColor Red
    Write-Host $_.Exception.Message -ForegroundColor Red
    Write-Host $_.Exception.InnerException.Message -ForegroundColor Yellow
    exit 1
}
