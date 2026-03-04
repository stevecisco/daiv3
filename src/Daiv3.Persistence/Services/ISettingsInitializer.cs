namespace Daiv3.Persistence.Services;

/// <summary>
/// Service for initializing application settings with default values.
/// Implements CT-REQ-001: The system SHALL store all settings locally.
/// </summary>
public interface ISettingsInitializer
{
    /// <summary>
    /// Initializes all application settings with default values if they don't exist.
    /// Safe to call multiple times - only creates missing settings.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Number of settings initialized.</returns>
    Task<int> InitializeDefaultSettingsAsync(CancellationToken ct = default);

    /// <summary>
    /// Checks if initial settings have been configured.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>True if settings are initialized, false otherwise.</returns>
    Task<bool> AreSettingsInitializedAsync(CancellationToken ct = default);

    /// <summary>
    /// Resets all settings to their default values.
    /// WARNING: This will overwrite all existing settings.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Number of settings reset.</returns>
    Task<int> ResetToDefaultsAsync(CancellationToken ct = default);
}
