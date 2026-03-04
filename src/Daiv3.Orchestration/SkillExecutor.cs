using Daiv3.Orchestration.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Diagnostics;

namespace Daiv3.Orchestration;

/// <summary>
/// Executes skills directly or within agent workflows.
/// Handles parameter validation, sandboxing, error handling, and execution logging.
/// </summary>
public class SkillExecutor : ISkillExecutor
{
    private readonly ISkillRegistry _skillRegistry;
    private readonly ILogger<SkillExecutor> _logger;
    private readonly ILogger<SkillResourceMonitor> _resourceMonitorLogger;
    private readonly OrchestrationOptions _options;
    private readonly SkillSandboxConfiguration _sandboxConfig;
    private readonly SkillPermissionValidator _permissionValidator;
    private const int DefaultTimeoutSeconds = 300; // 5 minutes default

    public SkillExecutor(
        ISkillRegistry skillRegistry,
        ILogger<SkillExecutor> logger,
        ILogger<SkillResourceMonitor> resourceMonitorLogger,
        IOptions<OrchestrationOptions> options,
        IOptions<SkillSandboxConfiguration> sandboxOptions,
        SkillPermissionValidator permissionValidator)
    {
        _skillRegistry = skillRegistry ?? throw new ArgumentNullException(nameof(skillRegistry));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _resourceMonitorLogger = resourceMonitorLogger ?? throw new ArgumentNullException(nameof(resourceMonitorLogger));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _sandboxConfig = sandboxOptions?.Value ?? new SkillSandboxConfiguration();
        _permissionValidator = permissionValidator ?? throw new ArgumentNullException(nameof(permissionValidator));
    }

    /// <inheritdoc />
    public async Task<SkillExecutionResult> ExecuteAsync(SkillExecutionRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.SkillName);

        var stopwatch = Stopwatch.StartNew();
        var callerContext = request.CallerContext ?? "Direct";

        _logger.LogInformation(
            "Executing skill '{SkillName}' from {CallerContext} with {ParameterCount} parameters",
            request.SkillName, callerContext, request.Parameters.Count);

        try
        {
            // Validate skill exists
            var skill = _skillRegistry.ResolveSkill(request.SkillName);
            if (skill == null)
            {
                stopwatch.Stop();
                var errorMsg = $"Skill '{request.SkillName}' not found in registry";
                _logger.LogWarning(
                    "Skill execution failed for '{SkillName}' from {CallerContext}: {Error}",
                    request.SkillName, callerContext, errorMsg);

                return new SkillExecutionResult
                {
                    Success = false,
                    ErrorMessage = errorMsg,
                    ElapsedMilliseconds = stopwatch.ElapsedMilliseconds
                };
            }

            // Check sandbox mode
            var sandboxMode = _sandboxConfig.GetEffectiveMode(request.SkillName);

            _logger.LogDebug(
                "Skill '{SkillName}' sandbox mode: {SandboxMode}",
                request.SkillName, sandboxMode);

            // Permission validation (if enabled)
            if (sandboxMode >= SkillSandboxMode.PermissionChecks)
            {
                var permissionCheck = _permissionValidator.ValidatePermissions(skill, request.SkillName);
                if (!permissionCheck.IsAllowed)
                {
                    stopwatch.Stop();
                    _logger.LogWarning(
                        "Skill '{SkillName}' from {CallerContext} blocked by permission check: {Reason}",
                        request.SkillName, callerContext, permissionCheck.DenialReason);

                    return new SkillExecutionResult
                    {
                        Success = false,
                        ErrorMessage = $"Permission denied: {permissionCheck.DenialReason}",
                        ElapsedMilliseconds = stopwatch.ElapsedMilliseconds
                    };
                }
            }

            // Validate parameters
            var paramValidation = ValidateParameters(request.SkillName, request.Parameters);
            if (!paramValidation.IsValid)
            {
                stopwatch.Stop();
                var errorMsg = string.Join("; ", paramValidation.Errors);
                _logger.LogWarning(
                    "Skill execution failed for '{SkillName}' from {CallerContext}: Parameter validation failed - {Error}",
                    request.SkillName, callerContext, errorMsg);

                return new SkillExecutionResult
                {
                    Success = false,
                    ErrorMessage = $"Parameter validation failed: {errorMsg}",
                    ElapsedMilliseconds = stopwatch.ElapsedMilliseconds
                };
            }

            // Log warnings if any
            if (paramValidation.Warnings.Count > 0)
            {
                _logger.LogWarning(
                    "Skill execution for '{SkillName}' from {CallerContext} has warnings: {Warnings}",
                    request.SkillName, callerContext, string.Join("; ", paramValidation.Warnings));
            }

            // Execute skill with timeout
            var timeout = request.TimeoutSeconds ?? DefaultTimeoutSeconds;
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromSeconds(timeout));

            _logger.LogDebug(
                "Skill '{SkillName}' execution starting with {Timeout}s timeout",
                request.SkillName, timeout);

            // Resource monitoring (if enabled)
            SkillResourceMonitor? monitor = null;
            if (sandboxMode >= SkillSandboxMode.ResourceLimits)
            {
                monitor = new SkillResourceMonitor(
                    _resourceMonitorLogger,
                    _sandboxConfig,
                    request.SkillName,
                    cts);
            }

            try
            {
                var output = await skill.ExecuteAsync(request.Parameters, cts.Token).ConfigureAwait(false);
                stopwatch.Stop();

                var resourceMetrics = monitor?.GetSnapshot();

                _logger.LogInformation(
                    "Skill '{SkillName}' executed successfully from {CallerContext} in {ElapsedMs}ms",
                    request.SkillName, callerContext, stopwatch.ElapsedMilliseconds);

                return new SkillExecutionResult
                {
                    Success = true,
                    Output = output,
                    ElapsedMilliseconds = stopwatch.ElapsedMilliseconds,
                    ResourceMetrics = resourceMetrics
                };
            }
            finally
            {
                monitor?.Dispose();
            }
        }
        catch (OperationCanceledException ex)
        {
            stopwatch.Stop();
            var errorMsg = $"Skill execution timeout after {request.TimeoutSeconds ?? DefaultTimeoutSeconds} seconds";
            _logger.LogError(
                ex,
                "Skill '{SkillName}' from {CallerContext} timed out after {ElapsedMs}ms",
                request.SkillName, callerContext, stopwatch.ElapsedMilliseconds);

            return new SkillExecutionResult
            {
                Success = false,
                ErrorMessage = errorMsg,
                Exception = ex,
                ElapsedMilliseconds = stopwatch.ElapsedMilliseconds
            };
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(
                ex,
                "Skill '{SkillName}' from {CallerContext} failed with exception after {ElapsedMs}ms",
                request.SkillName, callerContext, stopwatch.ElapsedMilliseconds);

            return new SkillExecutionResult
            {
                Success = false,
                ErrorMessage = ex.Message,
                Exception = ex,
                ElapsedMilliseconds = stopwatch.ElapsedMilliseconds
            };
        }
    }

    /// <inheritdoc />
    public bool CanExecute(string skillName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(skillName);
        var skill = _skillRegistry.ResolveSkill(skillName);
        return skill != null;
    }

    /// <inheritdoc />
    public SkillParameterValidationResult ValidateParameters(string skillName, Dictionary<string, object> parameters)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(skillName);
        ArgumentNullException.ThrowIfNull(parameters);

        var result = new SkillParameterValidationResult { IsValid = true };

        var skill = _skillRegistry.ResolveSkill(skillName);
        if (skill == null)
        {
            result.IsValid = false;
            result.Errors.Add($"Skill '{skillName}' not found in registry");
            return result;
        }

        // Get metadata for the skill
        var metadata = _skillRegistry.ListSkills()
            .FirstOrDefault(m => m.Name.Equals(skillName, StringComparison.OrdinalIgnoreCase));

        if (metadata == null)
        {
            result.IsValid = false;
            result.Errors.Add($"Skill metadata for '{skillName}' not found");
            return result;
        }

        // Validate required inputs
        foreach (var input in metadata.Inputs.Where(i => i.Required))
        {
            if (!parameters.ContainsKey(input.Name))
            {
                result.IsValid = false;
                result.Errors.Add($"Required parameter '{input.Name}' ({input.Type}) is missing");
            }
        }

        // Warn about unknown parameters
        var expectedParamNames = metadata.Inputs.Select(i => i.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var paramName in parameters.Keys)
        {
            if (!expectedParamNames.Contains(paramName))
            {
                result.Warnings.Add($"Unknown parameter '{paramName}' - skill may not use this parameter");
            }
        }

        return result;
    }
}
