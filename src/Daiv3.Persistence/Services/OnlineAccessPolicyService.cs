using Daiv3.Core.Settings;
using Daiv3.ModelExecution.Interfaces;
using Daiv3.ModelExecution.Models;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace Daiv3.Persistence.Services;

/// <summary>
/// Enforces configured online access policies based on application settings.
/// </summary>
/// <remarks>
/// Implements ES-REQ-002: "The system SHALL provide a configurable online fallback path
/// that requires explicit user configuration or per-call confirmation."
/// 
/// Reads the `online_access_mode` and `online_providers_enabled` settings from persistence
/// and enforces the configured policy for online provider access.
/// </remarks>
public class OnlineAccessPolicyService : IOnlineAccessPolicy
{
    private readonly ISettingsService _settingsService;
    private readonly ILogger<OnlineAccessPolicyService> _logger;

    public OnlineAccessPolicyService(
        ISettingsService settingsService,
        ILogger<OnlineAccessPolicyService> logger)
    {
        _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<OnlineAccessDecision> IsOnlineAccessAllowedAsync(
        ExecutionRequest request,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        // Check if any online providers are enabled
        var providersEnabled = await AreOnlineProvidersEnabledAsync(ct);
        if (!providersEnabled)
        {
            _logger.LogDebug(
                "Online access denied for request {RequestId}: No online providers enabled",
                request.Id);
            
            return OnlineAccessDecision.Denied(
                "No online providers are enabled in application settings",
                "providers_disabled");
        }

        // Get the configured online access mode
        var accessMode = await GetOnlineAccessModeAsync(ct);

        _logger.LogDebug(
            "Evaluating online access policy for request {RequestId}: mode={AccessMode}",
            request.Id, accessMode);

        return accessMode.ToLowerInvariant() switch
        {
            "never" => HandleNeverMode(request),
            "ask" => HandleAskMode(request),
            "auto_within_budget" => HandleAutoWithinBudgetMode(request),
            "per_task" => HandlePerTaskMode(request),
            _ => HandleUnknownMode(request, accessMode)
        };
    }

    public async Task<string> GetOnlineAccessModeAsync(CancellationToken ct = default)
    {
        var mode = await _settingsService.GetSettingValueAsync<string>(
            ApplicationSettings.Providers.OnlineAccessMode,
            ct);

        return mode ?? ApplicationSettings.Defaults.OnlineAccessMode;
    }

    public async Task<bool> AreOnlineProvidersEnabledAsync(CancellationToken ct = default)
    {
        var providersJson = await _settingsService.GetSettingValueAsync<string>(
            ApplicationSettings.Providers.OnlineProvidersEnabled,
            ct);

        if (string.IsNullOrWhiteSpace(providersJson))
        {
            return false;
        }

        try
        {
            var providers = JsonSerializer.Deserialize<string[]>(providersJson);
            return providers != null && providers.Length > 0;
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex,
                "Failed to parse online_providers_enabled setting: {ProvidersJson}",
                providersJson);
            return false;
        }
    }

    private OnlineAccessDecision HandleNeverMode(ExecutionRequest request)
    {
        _logger.LogInformation(
            "Online access denied for request {RequestId}: Access mode is 'never'",
            request.Id);

        return OnlineAccessDecision.Denied(
            "Online access is disabled (mode: never). Only local models are allowed.",
            "never");
    }

    private OnlineAccessDecision HandleAskMode(ExecutionRequest request)
    {
        _logger.LogInformation(
            "Online access requires confirmation for request {RequestId}: Access mode is 'ask'",
            request.Id);

        return OnlineAccessDecision.AllowedWithConfirmation(
            "User confirmation required for online access (mode: ask)",
            "ask");
    }

    private OnlineAccessDecision HandleAutoWithinBudgetMode(ExecutionRequest request)
    {
        // Note: Token budget checking is handled by OnlineProviderRouter
        // This policy just indicates that online access is allowed subject to budget checks

        _logger.LogDebug(
            "Online access allowed for request {RequestId}: Access mode is 'auto_within_budget' " +
            "(subject to token budget enforcement)",
            request.Id);

        return OnlineAccessDecision.AllowedWithConfirmation(
            "Online access allowed within token budget limits (mode: auto_within_budget)",
            "auto_within_budget");
    }

    private OnlineAccessDecision HandlePerTaskMode(ExecutionRequest request)
    {
        // Note: Per-task confirmation tracking would require additional state management
        // For now, treat this as requiring confirmation (similar to "ask" mode)
        // Future enhancement: Track confirmations by task ID

        _logger.LogInformation(
            "Online access requires per-task confirmation for request {RequestId}: Access mode is 'per_task'",
            request.Id);

        return OnlineAccessDecision.AllowedWithConfirmation(
            "User confirmation required once per task (mode: per_task)",
            "per_task");
    }

    private OnlineAccessDecision HandleUnknownMode(ExecutionRequest request, string accessMode)
    {
        _logger.LogWarning(
            "Unknown online access mode '{AccessMode}' for request {RequestId}, defaulting to 'ask'",
            accessMode, request.Id);

        return OnlineAccessDecision.AllowedWithConfirmation(
            $"Unknown access mode '{accessMode}', defaulting to require confirmation",
            accessMode);
    }
}
