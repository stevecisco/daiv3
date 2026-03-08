namespace Daiv3.Core.Validation;

/// <summary>
/// Validates that the system can operate in self-contained mode at startup.
/// Implements ES-CON-001: The application MUST be locally installable and self-contained.
/// Implements ES-CON-002: The initial implementation targets .NET 10.
/// </summary>
public interface IStartupValidator
{
    /// <summary>
    /// Validates that all core system components are available for self-contained operation.
    /// This includes verifying local storage paths, database accessibility, and absence of
    /// mandatory external dependencies.
    /// </summary>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Validation result indicating success or failure with details</returns>
    Task<StartupValidationResult> ValidateSelfContainedOperationAsync(CancellationToken ct = default);

    /// <summary>
    /// Validates that the system can operate in fully offline mode.
    /// Checks that no mandatory online services are required.
    /// </summary>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Validation result indicating offline capability</returns>
    Task<StartupValidationResult> ValidateOfflineCapabilityAsync(CancellationToken ct = default);

    /// <summary>
    /// Validates that the application is running on .NET 10.
    /// Implements ES-CON-002: The initial implementation targets .NET 10.
    /// </summary>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Validation result indicating framework version compliance</returns>
    Task<StartupValidationResult> ValidateFrameworkVersionAsync(CancellationToken ct = default);
}
