namespace Daiv3.Core.Validation;

/// <summary>
/// Result of a startup validation check.
/// Used by ES-CON-001 to verify self-contained operation capability.
/// </summary>
public sealed class StartupValidationResult
{
    /// <summary>
    /// Gets whether the validation passed.
    /// </summary>
    public bool IsValid { get; init; }

    /// <summary>
    /// Gets the validation category (e.g., "SelfContained", "Offline").
    /// </summary>
    public string Category { get; init; } = string.Empty;

    /// <summary>
    /// Gets a list of validation checks that were performed.
    /// </summary>
    public IReadOnlyList<ValidationCheck> Checks { get; init; } = Array.Empty<ValidationCheck>();

    /// <summary>
    /// Gets a list of validation errors encountered.
    /// </summary>
    public IReadOnlyList<string> Errors { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Gets a list of validation warnings (non-critical issues).
    /// </summary>
    public IReadOnlyList<string> Warnings { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Gets additional information about the validation.
    /// </summary>
    public string? AdditionalInfo { get; init; }
}

/// <summary>
/// Represents a single validation check within a startup validation.
/// </summary>
public sealed class ValidationCheck
{
    /// <summary>
    /// Gets the name of the check (e.g., "Database Directory Writable").
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// Gets whether the check passed.
    /// </summary>
    public bool Passed { get; init; }

    /// <summary>
    /// Gets the error message if the check failed.
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// Gets the duration of the check in milliseconds.
    /// </summary>
    public double DurationMs { get; init; }
}
