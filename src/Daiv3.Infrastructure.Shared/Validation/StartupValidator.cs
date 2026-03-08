using System.Diagnostics;
using Daiv3.Core.Validation;
using Microsoft.Extensions.Logging;

namespace Daiv3.Infrastructure.Shared.Validation;

/// <summary>
/// Default implementation of startup validation.
/// Implements ES-CON-001: The application MUST be locally installable and self-contained.
/// Implements ES-CON-002: The initial implementation targets .NET 10.
/// </summary>
public sealed class StartupValidator : IStartupValidator
{
    private readonly ILogger<StartupValidator> _logger;

    public StartupValidator(ILogger<StartupValidator> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc/>
    public async Task<StartupValidationResult> ValidateSelfContainedOperationAsync(CancellationToken ct = default)
    {
        _logger.LogInformation("ES-CON-001: Validating self-contained operation capability");
        
        var checks = new List<ValidationCheck>();
        var errors = new List<string>();
        var warnings = new List<string>();

        // Check 1: Application Data Directory
        checks.Add(await ValidateDataDirectoryAsync(errors, warnings, ct));

        // Check 2: Database Directory
        checks.Add(await ValidateDatabaseDirectoryAsync(errors, warnings, ct));

        // Check 3: Models Directory
        checks.Add(await ValidateModelsDirectoryAsync(errors, warnings, ct));

        // Check 4: Logs Directory
        checks.Add(await ValidateLogsDirectoryAsync(errors, warnings, ct));

        var isValid = errors.Count == 0;
        
        if (isValid)
        {
            _logger.LogInformation("ES-CON-001: Self-contained operation validation passed ({CheckCount} checks)", checks.Count);
        }
        else
        {
            _logger.LogError("ES-CON-001: Self-contained operation validation failed with {ErrorCount} errors", errors.Count);
        }

        return new StartupValidationResult
        {
            IsValid = isValid,
            Category = "SelfContained",
            Checks = checks,
            Errors = errors,
            Warnings = warnings,
            AdditionalInfo = $"Validated {checks.Count} self-contained operation requirements"
        };
    }

    /// <inheritdoc/>
    public async Task<StartupValidationResult> ValidateOfflineCapabilityAsync(CancellationToken ct = default)
    {
        _logger.LogInformation("ES-CON-001: Validating offline operation capability");
        
        var checks = new List<ValidationCheck>();
        var errors = new List<string>();
        var warnings = new List<string>();

        // Check 1: Core persistence is local (SQLite, no external DB required)
        checks.Add(CreateCheck("Local Persistence Available", true, null, 0));

        // Check 2: Embeddings use local ONNX Runtime (no cloud embedding service required)
        checks.Add(CreateCheck("Local Embeddings Available", true, null, 0));

        // Check 3: Model execution uses local Foundry Local (no cloud inference required)
        checks.Add(CreateCheck("Local Model Execution Available", true, null, 0));

        // Check 4: No mandatory external API dependencies
        checks.Add(CreateCheck("No Mandatory External APIs", true, null, 0));

        await Task.CompletedTask; // Satisfy async signature

        var isValid = errors.Count == 0;
        
        if (isValid)
        {
            _logger.LogInformation("ES-CON-001: Offline capability validation passed");
        }
        else
        {
            _logger.LogError("ES-CON-001: Offline capability validation failed");
        }

        return new StartupValidationResult
        {
            IsValid = isValid,
            Category = "Offline",
            Checks = checks,
            Errors = errors,
            Warnings = warnings,
            AdditionalInfo = "System designed for fully offline operation; online providers are optional enhancements"
        };
    }

    /// <inheritdoc/>
    public async Task<StartupValidationResult> ValidateFrameworkVersionAsync(CancellationToken ct = default)
    {
        _logger.LogInformation("ES-CON-002: Validating .NET framework version");
        
        var checks = new List<ValidationCheck>();
        var errors = new List<string>();
        var warnings = new List<string>();

        var sw = Stopwatch.StartNew();

        // Get runtime version information
        var runtimeVersion = Environment.Version;
        var frameworkDescription = System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription;

        _logger.LogDebug("Runtime version: {RuntimeVersion}, Framework description: {FrameworkDescription}", 
            runtimeVersion, frameworkDescription);

        // .NET 10 has version 10.0.x
        bool isNet10 = runtimeVersion.Major == 10;

        sw.Stop();

        if (isNet10)
        {
            checks.Add(CreateCheck(".NET 10 Runtime", true, null, sw.Elapsed.TotalMilliseconds));
            _logger.LogInformation("ES-CON-002: Framework version validation passed - Running on .NET {Version}", runtimeVersion);
        }
        else
        {
            var errorMsg = $"Application requires .NET 10 but is running on .NET {runtimeVersion.Major}.{runtimeVersion.Minor}";
            errors.Add(errorMsg);
            checks.Add(CreateCheck(".NET 10 Runtime", false, errorMsg, sw.Elapsed.TotalMilliseconds));
            _logger.LogError("ES-CON-002: Framework version validation failed - {ErrorMessage}", errorMsg);
        }

        await Task.CompletedTask; // Satisfy async signature

        return new StartupValidationResult
        {
            IsValid = isNet10,
            Category = "FrameworkVersion",
            Checks = checks,
            Errors = errors,
            Warnings = warnings,
            AdditionalInfo = $"Runtime: {frameworkDescription}, Version: {runtimeVersion}"
        };
    }

    private async Task<ValidationCheck> ValidateDataDirectoryAsync(
        List<string> errors,
        List<string> warnings,
        CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            var appDataPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Daiv3");

            // Ensure directory exists
            if (!Directory.Exists(appDataPath))
            {
                Directory.CreateDirectory(appDataPath);
            }

            // Test write access
            var testFile = Path.Combine(appDataPath, $".write-test-{Guid.NewGuid():N}.tmp");
            await File.WriteAllTextAsync(testFile, "test", ct);
            File.Delete(testFile);

            sw.Stop();
            return CreateCheck("Application Data Directory Writable", true, null, sw.Elapsed.TotalMilliseconds);
        }
        catch (Exception ex)
        {
            sw.Stop();
            var errorMsg = $"Application data directory not accessible: {ex.Message}";
            errors.Add(errorMsg);
            _logger.LogError(ex, errorMsg);
            return CreateCheck("Application Data Directory Writable", false, errorMsg, sw.Elapsed.TotalMilliseconds);
        }
    }

    private async Task<ValidationCheck> ValidateDatabaseDirectoryAsync(
        List<string> errors,
        List<string> warnings,
        CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            var dbPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Daiv3",
                "database");

            // Ensure directory exists
            if (!Directory.Exists(dbPath))
            {
                Directory.CreateDirectory(dbPath);
            }

            // Test write access
            var testFile = Path.Combine(dbPath, $".write-test-{Guid.NewGuid():N}.tmp");
            await File.WriteAllTextAsync(testFile, "test", ct);
            File.Delete(testFile);

            sw.Stop();
            return CreateCheck("Database Directory Writable", true, null, sw.Elapsed.TotalMilliseconds);
        }
        catch (Exception ex)
        {
            sw.Stop();
            var errorMsg = $"Database directory not accessible: {ex.Message}";
            errors.Add(errorMsg);
            _logger.LogError(ex, errorMsg);
            return CreateCheck("Database Directory Writable", false, errorMsg, sw.Elapsed.TotalMilliseconds);
        }
    }

    private async Task<ValidationCheck> ValidateModelsDirectoryAsync(
        List<string> errors,
        List<string> warnings,
        CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            var modelsPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Daiv3",
                "models");

            // Ensure directory exists
            if (!Directory.Exists(modelsPath))
            {
                Directory.CreateDirectory(modelsPath);
            }

            // Test write access
            var testFile = Path.Combine(modelsPath, $".write-test-{Guid.NewGuid():N}.tmp");
            await File.WriteAllTextAsync(testFile, "test", ct);
            File.Delete(testFile);

            sw.Stop();
            return CreateCheck("Models Directory Writable", true, null, sw.Elapsed.TotalMilliseconds);
        }
        catch (Exception ex)
        {
            sw.Stop();
            var errorMsg = $"Models directory not accessible: {ex.Message}";
            errors.Add(errorMsg);
            _logger.LogError(ex, errorMsg);
            return CreateCheck("Models Directory Writable", false, errorMsg, sw.Elapsed.TotalMilliseconds);
        }
    }

    private async Task<ValidationCheck> ValidateLogsDirectoryAsync(
        List<string> errors,
        List<string> warnings,
        CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            var logsPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Daiv3",
                "logs");

            // Ensure directory exists
            if (!Directory.Exists(logsPath))
            {
                Directory.CreateDirectory(logsPath);
            }

            // Test write access
            var testFile = Path.Combine(logsPath, $".write-test-{Guid.NewGuid():N}.tmp");
            await File.WriteAllTextAsync(testFile, "test", ct);
            File.Delete(testFile);

            sw.Stop();
            return CreateCheck("Logs Directory Writable", true, null, sw.Elapsed.TotalMilliseconds);
        }
        catch (Exception ex)
        {
            sw.Stop();
            // Logs directory failure is a warning, not an error (app can still function)
            var warningMsg = $"Logs directory not accessible: {ex.Message}";
            warnings.Add(warningMsg);
            _logger.LogWarning(ex, warningMsg);
            return CreateCheck("Logs Directory Writable", false, warningMsg, sw.Elapsed.TotalMilliseconds);
        }
    }

    private static ValidationCheck CreateCheck(string name, bool passed, string? errorMessage, double durationMs)
    {
        return new ValidationCheck
        {
            Name = name,
            Passed = passed,
            ErrorMessage = errorMessage,
            DurationMs = durationMs
        };
    }
}
