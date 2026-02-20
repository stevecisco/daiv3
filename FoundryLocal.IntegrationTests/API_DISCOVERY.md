# API Method Discovery Guide

Since the Foundry Local SDK documentation may not always be fully up-to-date with the actual implementation, this guide shows you how to discover the available API methods.

## Method 1: Using Visual Studio IntelliSense

1. Open the project in Visual Studio
2. In any test file, type `manager.` to see all available methods on `FoundryLocalManager`
3. Use Ctrl+Space to trigger IntelliSense
4. Use F12 (Go to Definition) to see method signatures

## Method 2: Using Object Browser (Visual Studio)

1. Build the project: `dotnet build`
2. Open Visual Studio
3. View → Object Browser
4. Browse to `Microsoft.AI.Foundry.Local` assembly
5. Explore `FoundryLocalManager` and related classes

## Method 3: Using ILSpy or dnSpy

1. Build the project
2. Navigate to the NuGet packages folder:
   ```
   %USERPROFILE%\.nuget\packages\microsoft.ai.foundry.local.winml\0.8.2.1\lib\
   ```
3. Open the DLL in ILSpy or dnSpy
4. Browse available classes and methods

## Method 4: Using dotnet CLI with reflection

Create a simple console app to inspect the API:

```csharp
using System.Reflection;
using Microsoft.AI.Foundry.Local;

var assembly = typeof(FoundryLocalManager).Assembly;
var type = typeof(FoundryLocalManager);

Console.WriteLine($"Methods in {type.Name}:");
foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.Instance))
{
    Console.WriteLine($"  {method.Name}({string.Join(", ", method.GetParameters().Select(p => $"{p.ParameterType.Name} {p.Name}"))})");
}
```

## Method 5: Check the Official API Reference

Visit: https://aka.ms/fl-csharp-api-ref

## Common API Patterns in SDK 0.8.0+

Based on the documentation, here are the expected patterns:

### Manager Initialization
```csharp
var config = new Configuration { AppName = "MyApp" };
await FoundryLocalManager.CreateAsync(config, logger);
var manager = FoundryLocalManager.Instance;
```

### Catalog Operations
```csharp
var catalog = await manager.GetCatalogAsync();
var models = await catalog.ListModelsAsync();
var model = await catalog.GetModelAsync(alias: "phi-3");
var loadedModels = await catalog.GetLoadedModelsAsync();
var cachedModels = await catalog.GetCachedModelsAsync();
```

### Model Operations
```csharp
await model.DownloadAsync();
await model.LoadAsync();
await model.UnloadAsync();
var path = await model.GetPathAsync();
var variant = model.SelectedVariant;
model.SelectVariant("variant-name");
```

### Web Server Operations (if available)
```csharp
// Documentation mentions these but actual method names may vary:
await manager.StartWebServerAsync();  // May be different in actual API
await manager.StopWebServerAsync();   // May be different in actual API
```

## Updating Tests Based on Actual API

Once you discover the actual method names:

1. Update `WebServerTests.cs` with correct method names
2. Remove the `Skip` attribute from tests
3. Update the README.md with accurate API examples

## Example: Discovering Web Server Methods

```bash
# Build the project
dotnet build

# List all methods in FoundryLocalManager
dotnet exec --depsfile bin/Debug/net9.0-windows10.0.26100/FoundryLocal.IntegrationTests.deps.json --runtimeconfig bin/Debug/net9.0-windows10.0.26100/FoundryLocal.IntegrationTests.runtimeconfig.json
```

Or use this PowerShell script:

```powershell
$dllPath = "C:\Users\<user>\.nuget\packages\microsoft.ai.foundry.local.winml\0.8.2.1\lib\net9.0-windows10.0.26100\Microsoft.AI.Foundry.Local.dll"
$assembly = [System.Reflection.Assembly]::LoadFrom($dllPath)
$type = $assembly.GetType("Microsoft.AI.Foundry.Local.FoundryLocalManager")
$type.GetMethods() | Where-Object { $_.IsPublic } | ForEach-Object { $_.Name } | Sort-Object | Get-Unique
```
