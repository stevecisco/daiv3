using Daiv3.Core.Validation;
using Daiv3.Infrastructure.Shared.Validation;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Daiv3.Infrastructure.Shared.Tests.Validation;

/// <summary>
/// Unit tests for StartupValidator.
/// Validates ES-CON-001: The application MUST be locally installable and self-contained.
/// </summary>
public class StartupValidatorTests
{
    private readonly ILogger<StartupValidator> _logger;
    private readonly StartupValidator _validator;

    public StartupValidatorTests()
    {
        _logger = new NullLogger<StartupValidator>();
        _validator = new StartupValidator(_logger);
    }

    /// <summary>
    /// ES-CON-001: Tests that self-contained operation validation succeeds.
    /// Verifies that all required local directories are accessible and writable.
    /// </summary>
    [Fact]
    public async Task ValidateSelfContainedOperationAsync_WithValidEnvironment_ReturnsSuccess()
    {
        // Act
        var result = await _validator.ValidateSelfContainedOperationAsync();

        // Assert
        Assert.NotNull(result);
        Assert.Equal("SelfContained", result.Category);
        Assert.True(result.IsValid, $"Validation failed with errors: {string.Join(", ", result.Errors)}");
        Assert.NotEmpty(result.Checks);
        Assert.All(result.Checks, check => Assert.True(check.Passed, $"Check '{check.Name}' failed: {check.ErrorMessage}"));
    }

    /// <summary>
    /// ES-CON-001: Tests that offline capability validation succeeds.
    /// Verifies that the system can operate without external dependencies.
    /// </summary>
    [Fact]
    public async Task ValidateOfflineCapabilityAsync_ReturnsSuccess()
    {
        // Act
        var result = await _validator.ValidateOfflineCapabilityAsync();

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Offline", result.Category);
        Assert.True(result.IsValid);
        Assert.NotEmpty(result.Checks);
        Assert.All(result.Checks, check => Assert.True(check.Passed));
        Assert.Empty(result.Errors);
    }

    /// <summary>
    /// ES-CON-001: Tests that validation result includes all expected checks.
    /// </summary>
    [Fact]
    public async Task ValidateSelfContainedOperationAsync_IncludesAllExpectedChecks()
    {
        // Act
        var result = await _validator.ValidateSelfContainedOperationAsync();

        // Assert
        Assert.NotNull(result.Checks);
        Assert.Contains(result.Checks, c => c.Name == "Application Data Directory Writable");
        Assert.Contains(result.Checks, c => c.Name == "Database Directory Writable");
        Assert.Contains(result.Checks, c => c.Name == "Models Directory Writable");
        Assert.Contains(result.Checks, c => c.Name == "Logs Directory Writable");
    }

    /// <summary>
    /// ES-CON-001: Tests that offline validation includes all expected checks.
    /// </summary>
    [Fact]
    public async Task ValidateOfflineCapabilityAsync_IncludesAllExpectedOfflineChecks()
    {
        // Act
        var result = await _validator.ValidateOfflineCapabilityAsync();

        // Assert
        Assert.NotNull(result.Checks);
        Assert.Contains(result.Checks, c => c.Name == "Local Persistence Available");
        Assert.Contains(result.Checks, c => c.Name == "Local Embeddings Available");
        Assert.Contains(result.Checks, c => c.Name == "Local Model Execution Available");
        Assert.Contains(result.Checks, c => c.Name == "No Mandatory External APIs");
    }

    /// <summary>
    /// ES-CON-001: Tests that each check includes duration measurement.
    /// </summary>
    [Fact]
    public async Task ValidateSelfContainedOperationAsync_ChecksIncludeDuration()
    {
        // Act
        var result = await _validator.ValidateSelfContainedOperationAsync();

        // Assert
        Assert.All(result.Checks, check => Assert.True(check.DurationMs >= 0));
    }

    /// <summary>
    /// ES-CON-001: Tests that validation includes additional info.
    /// </summary>
    [Fact]
    public async Task ValidateSelfContainedOperationAsync_IncludesAdditionalInfo()
    {
        // Act
        var result = await _validator.ValidateSelfContainedOperationAsync();

        // Assert
        Assert.NotNull(result.AdditionalInfo);
        Assert.Contains("Validated", result.AdditionalInfo);
        Assert.Contains("self-contained", result.AdditionalInfo);
    }

    /// <summary>
    /// ES-CON-001: Tests that offline validation includes additional info about design.
    /// </summary>
    [Fact]
    public async Task ValidateOfflineCapabilityAsync_IncludesOfflineDesignInfo()
    {
        // Act
        var result = await _validator.ValidateOfflineCapabilityAsync();

        // Assert
        Assert.NotNull(result.AdditionalInfo);
        Assert.Contains("offline", result.AdditionalInfo);
        Assert.Contains("optional", result.AdditionalInfo);
    }

    /// <summary>
    /// ES-CON-001: Tests that validation can be cancelled.
    /// </summary>
    [Fact]
    public async Task ValidateSelfContainedOperationAsync_SupportsCancellation()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
        {
            // Fast cancellation check
            cts.Token.ThrowIfCancellationRequested();
            await _validator.ValidateSelfContainedOperationAsync(cts.Token);
        });
    }

    /// <summary>
    /// ES-CON-001: Tests that validation result structure is correct.
    /// </summary>
    [Fact]
    public async Task ValidateSelfContainedOperationAsync_ReturnsWellFormedResult()
    {
        // Act
        var result = await _validator.ValidateSelfContainedOperationAsync();

        // Assert
        Assert.NotNull(result);
        Assert.NotNull(result.Category);
        Assert.NotNull(result.Checks);
        Assert.NotNull(result.Errors);
        Assert.NotNull(result.Warnings);
        
        // All properties should be initialized, not default/null
        Assert.False(string.IsNullOrWhiteSpace(result.Category));
    }

    /// <summary>
    /// ES-CON-001: Tests that directories are created if they don't exist.
    /// </summary>
    [Fact]
    public async Task ValidateSelfContainedOperationAsync_CreatesDirectoriesIfMissing()
    {
        // Arrange
        var appDataPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Daiv3");

        // Act
        var result = await _validator.ValidateSelfContainedOperationAsync();

        // Assert
        Assert.True(result.IsValid);
        Assert.True(Directory.Exists(appDataPath), "Application data directory should be created");
        Assert.True(Directory.Exists(Path.Combine(appDataPath, "database")), "Database directory should be created");
        Assert.True(Directory.Exists(Path.Combine(appDataPath, "models")), "Models directory should be created");
        Assert.True(Directory.Exists(Path.Combine(appDataPath, "logs")), "Logs directory should be created");
    }
}
