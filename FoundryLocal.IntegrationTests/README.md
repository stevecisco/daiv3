# Foundry Local SDK Integration Tests

This project contains comprehensive integration tests for the **Azure Foundry Local SDK** (C# WinML version 0.8.2.1).

## Overview

These tests validate the core functionality of the Foundry Local SDK, including:
- Manager initialization and configuration
- Model catalog operations
- Model lifecycle management (download, load, unload)
- Web server operations
- Configuration management

## Project Structure

```
FoundryLocal.IntegrationTests/
├── FoundryLocal.IntegrationTests.csproj   # Project file with SDK dependencies
├── nuget.config                            # NuGet package sources configuration
├── ExcludeExtraLibs.props                  # Build optimization to reduce package size
├── FoundryLocalManagerTests.cs             # Core manager initialization tests
├── ModelCatalogTests.cs                    # Catalog and model listing tests
├── ModelLifecycleTests.cs                  # Model download/load/unload tests
├── WebServerTests.cs                       # REST web server tests
└── ConfigurationTests.cs                   # Configuration and customization tests
```

## Dependencies

The project uses the following key packages:
- **Microsoft.AI.Foundry.Local.WinML** (0.8.2.1) - Foundry Local SDK for Windows
- **Microsoft.Extensions.Logging** (9.0.10) - Logging infrastructure
- **OpenAI** (2.5.0) - OpenAI SDK for inference integration
- **xUnit** (2.9.3) - Test framework

## Prerequisites

- .NET 9.0 SDK with Windows 10.0.26100 target
- Windows OS (for WinML package)
- Azure Foundry Local runtime installed (or the SDK will manage it)

## Configuration

### NuGet Package Sources

The project requires two NuGet sources configured in `nuget.config`:
1. **nuget.org** - Standard NuGet packages
2. **ORT** (ONNX Runtime) - Foundry Local packages from Azure DevOps

### Build Optimization

The `ExcludeExtraLibs.props` file excludes large CUDA and QNN execution provider libraries to reduce package size by ~1GB. These libraries will be downloaded at runtime by WinML if compatible hardware is detected.

## Running the Tests

### Run all tests:
```bash
dotnet test
```

### Run specific test class:
```bash
dotnet test --filter "FullyQualifiedName~FoundryLocal.IntegrationTests.FoundryLocalManagerTests"
```

### Run with verbose logging:
```bash
dotnet test --logger "console;verbosity=detailed"
```

## Test Categories

### 1. FoundryLocalManagerTests
Tests core SDK initialization and basic operations:
- `CreateAsync_ShouldInitializeFoundryLocalManager` - Verify manager creation
- `GetCatalogAsync_ShouldReturnCatalog` - Test catalog access
- `ListModelsAsync_ShouldReturnModelsList` - Verify model enumeration
- `Configuration_WithCustomSettings_ShouldApplySettings` - Test custom config

### 2. ModelCatalogTests
Tests model catalog operations:
- `ListModelsAsync_ShouldReturnNonEmptyList` - List available models
- `GetModelAsync_WithValidAlias_ShouldReturnModel` - Get model by alias
- `GetLoadedModelsAsync_ShouldReturnLoadedModelsList` - List loaded models
- `GetCachedModelsAsync_ShouldReturnCachedModelsList` - List cached models

### 3. ModelLifecycleTests
Tests model lifecycle operations (some tests skipped by default as they require specific models):
- `DownloadAsync_WithValidModel_ShouldDownloadModel` - Download model
- `LoadAsync_WithDownloadedModel_ShouldLoadModel` - Load model into memory
- `UnloadAsync_WithLoadedModel_ShouldUnloadModel` - Unload model
- `GetPathAsync_WithCachedModel_ShouldReturnModelPath` - Get model file path
- `SelectVariant_WithModel_ShouldSelectVariant` - Select model variant

### 4. WebServerTests
Tests optional REST web server functionality:
- `StartWebServerAsync_ShouldStartSuccessfully` - Start web server
- `StopWebServerAsync_AfterStart_ShouldStopSuccessfully` - Stop web server
- `StartWebServerAsync_WithCustomUrl_ShouldUseCustomUrl` - Custom URL configuration

### 5. ConfigurationTests
Tests configuration options and customization:
- `Configuration_WithCustomAppDataDir_ShouldUseCustomPath` - Custom app data directory
- `Configuration_WithCustomModelCacheDir_ShouldUseCustomPath` - Custom model cache
- `Configuration_WithCustomLogsDir_ShouldUseCustomPath` - Custom logs directory
- `Configuration_WithDifferentLogLevels_ShouldApplyLogLevel` - Log level configuration
- `Configuration_WithAllCustomSettings_ShouldApplyAllSettings` - Full custom config

## Key Features Tested

### SDK Initialization
```csharp
var config = new Configuration
{
    AppName = "MyApp",
    LogLevel = Microsoft.AI.Foundry.Local.LogLevel.Information,
};

await FoundryLocalManager.CreateAsync(config, logger);
var manager = FoundryLocalManager.Instance;
```

### Model Catalog Access
```csharp
var catalog = await manager.GetCatalogAsync();
var models = await catalog.ListModelsAsync();
Console.WriteLine($"Models available: {models.Count()}");
```

### Advanced Configuration
```csharp
var config = new Configuration
{
    AppName = "app-name",
    LogLevel = Microsoft.AI.Foundry.Local.LogLevel.Information,
    Web = new Configuration.WebService
    {
        Urls = "http://127.0.0.1:55588"
    },
    AppDataDir = "./foundry_local_data",
    ModelCacheDir = "{AppDataDir}/model_cache",
    LogsDir = "{AppDataDir}/logs"
};
```

## Notes

- Some tests are marked with `[Fact(Skip = "...")]` as they require specific models to be downloaded or loaded first
- Tests use `IAsyncLifetime` for proper async setup and cleanup
- Logging is configured to help diagnose issues during test execution
- Web server tests ensure proper cleanup with `StopWebServerAsync` in `DisposeAsync`

## API Changes in SDK 0.8.0+

The tests reflect the new object-oriented API introduced in SDK version 0.8.0:

| Operation | Old API (< 0.8.0) | New API (0.8.0+) |
|-----------|-------------------|------------------|
| Get Manager | `mgr = FoundryLocalManager()` | `await FoundryLocalManager.CreateAsync(config, logger); var mgr = FoundryLocalManager.Instance;` |
| List Models | `mgr.ListCatalogModelsAsync()` | `catalog.ListModelsAsync()` |
| Download | `mgr.DownloadModelAsync("id")` | `model.DownloadAsync()` |
| Load | `mgr.LoadModelAsync("id")` | `model.LoadAsync()` |
| Start Service | `mgr.StartServiceAsync()` | `mgr.StartWebServerAsync()` |

## References

- [Foundry Local SDK Reference](https://learn.microsoft.com/en-us/azure/ai-foundry/foundry-local/reference/reference-sdk?view=foundry-classic&tabs=windows&pivots=programming-language-csharp)
- [Foundry Local C# SDK Samples](https://aka.ms/foundrylocalSDK)
- [Foundry Local C# SDK API Reference](https://aka.ms/fl-csharp-api-ref)

## License

This test project is for integration testing purposes following Microsoft documentation examples.
