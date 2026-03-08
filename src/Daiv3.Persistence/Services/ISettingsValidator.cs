namespace Daiv3.Persistence.Services;

/// <summary>
/// Service for validating application settings before they are persisted.
/// Implements CT-NFR-002: Settings changes SHOULD be validated and applied safely.
/// </summary>
public interface ISettingsValidator
{
    /// <summary>
    /// Validates a setting value before it is saved.
    /// Throws SettingsValidationException if validation fails.
    /// </summary>
    /// <param name="key">The setting key to validate.</param>
    /// <param name="value">The value to validate.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A validation result containing details.</returns>
    Task<SettingsValidationResult> ValidateAsync(string key, object value, CancellationToken ct = default);

    /// <summary>
    /// Validates multiple settings in a batch operation.
    /// Returns all validation errors without throwing.
    /// </summary>
    /// <param name="settings">Dictionary of setting key-value pairs.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>List of validation results (empty if all valid).</returns>
    Task<IReadOnlyList<SettingsValidationResult>> ValidateBatchAsync(
        IDictionary<string, object> settings, 
        CancellationToken ct = default);
}

/// <summary>
/// Result of a settings validation operation.
/// </summary>
public class SettingsValidationResult
{
    public SettingsValidationResult(string key, bool isValid, string? errorMessage = null, string? warningMessage = null)
    {
        Key = key;
        IsValid = isValid;
        ErrorMessage = errorMessage;
        WarningMessage = warningMessage;
    }

    /// <summary>
    /// The setting key that was validated.
    /// </summary>
    public string Key { get; }

    /// <summary>
    /// Whether the setting value is valid.
    /// </summary>
    public bool IsValid { get; }

    /// <summary>
    /// Error message if validation failed.
    /// </summary>
    public string? ErrorMessage { get; }

    /// <summary>
    /// Warning message if validation passed but there are concerns.
    /// </summary>
    public string? WarningMessage { get; }
}

/// <summary>
/// Exception thrown when settings validation fails.
/// </summary>
public class SettingsValidationException : Exception
{
    public SettingsValidationException(string key, string message)
        : base($"Settings validation failed for key '{key}': {message}")
    {
        Key = key;
    }

    public string Key { get; }
}
