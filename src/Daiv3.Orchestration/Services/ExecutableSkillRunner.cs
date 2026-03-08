using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Daiv3.Core.Authorization;
using Daiv3.Core.Enums;
using Daiv3.Persistence.Repositories;
using Microsoft.Extensions.Logging;

namespace Daiv3.Orchestration.Services;

/// <summary>
/// Executes approved executable skills with pre-execution validation and output capture.
/// </summary>
public class ExecutableSkillRunner : IExecutableSkillRunner
{
    private readonly IExecutableSkillRepository _repository;
    private readonly IExecutableSkillApprovalService _approvalService;
    private readonly ISkillHashService _hashService;
    private readonly ISkillAuditService _skillAuditService;
    private readonly ILogger<ExecutableSkillRunner> _logger;

    public ExecutableSkillRunner(
        IExecutableSkillRepository repository,
        IExecutableSkillApprovalService approvalService,
        ISkillHashService hashService,
        ISkillAuditService skillAuditService,
        ILogger<ExecutableSkillRunner> logger)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _approvalService = approvalService ?? throw new ArgumentNullException(nameof(approvalService));
        _hashService = hashService ?? throw new ArgumentNullException(nameof(hashService));
        _skillAuditService = skillAuditService ?? throw new ArgumentNullException(nameof(skillAuditService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<SkillValidationResult> ValidateBeforeExecutionAsync(
        string skillId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Validating skill {SkillId} before execution", skillId);

        // Load skill
        var skill = await _repository.GetByIdAsync(skillId, cancellationToken);
        if (skill == null)
        {
            _logger.LogWarning("Skill {SkillId} not found", skillId);
            return SkillValidationResult.Failure("NotFound", $"Skill {skillId} not found");
        }

        // Check approval status
        if (skill.ApprovalStatus != ApprovalStatus.Approved.ToString())
        {
            _logger.LogWarning(
                "Skill {SkillId} cannot be executed: approval status is {ApprovalStatus}",
                skillId,
                skill.ApprovalStatus);

            return SkillValidationResult.Failure(
                "ApprovalRequired",
                $"Skill '{skill.Name}' has approval status '{skill.ApprovalStatus}'. Only 'Approved' skills can be executed. Please request approval from an administrator.");
        }

        // Validate file exists
        if (!File.Exists(skill.FilePath))
        {
            _logger.LogError("Skill {SkillId} file not found at {FilePath}", skillId, skill.FilePath);
            return SkillValidationResult.Failure(
                "FileNotFound",
                $"Skill file not found at {skill.FilePath}");
        }

        // Validate hash integrity
        var hashValid = await _hashService.ValidateHashAsync(skill, cancellationToken);
        if (!hashValid)
        {
            _logger.LogError(
                "Skill {SkillId} failed integrity check: file hash mismatch",
                skillId);

            return SkillValidationResult.Failure(
                "IntegrityFailure",
                $"Skill '{skill.Name}' failed integrity check. The file has been modified since approval. Please request re-approval from an administrator.");
        }

        _logger.LogInformation("Skill {SkillId} passed validation", skillId);
        return SkillValidationResult.Success();
    }

    public async Task<SkillExecutionResult> ExecuteAsync(
        string skillId,
        IDictionary<string, string> parameters,
        SystemPrincipal principal,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Executing skill {SkillId} with {ParameterCount} parameters for principal {PrincipalId}",
            skillId,
            parameters.Count,
            principal.PrincipalId);

        // Pre-execution validation
        var validationResult = await ValidateBeforeExecutionAsync(skillId, cancellationToken);
        if (!validationResult.IsValid)
        {
            await LogAuditSafeAsync(
                skillId,
                SkillAuditEventType.ExecutionDenied,
                principal.PrincipalId,
                new Dictionary<string, string>
                {
                    ["errorCode"] = validationResult.ErrorCode ?? "Unknown",
                    ["reason"] = validationResult.ErrorMessage ?? "Validation failed"
                },
                cancellationToken).ConfigureAwait(false);

            if (string.Equals(validationResult.ErrorCode, "IntegrityFailure", StringComparison.OrdinalIgnoreCase))
            {
                await LogAuditSafeAsync(
                    skillId,
                    SkillAuditEventType.HashMismatch,
                    principal.PrincipalId,
                    new Dictionary<string, string>
                    {
                        ["reason"] = validationResult.ErrorMessage ?? "Skill hash mismatch"
                    },
                    cancellationToken).ConfigureAwait(false);
            }

            _logger.LogError(
                "Skill {SkillId} failed pre-execution validation: {ErrorMessage}",
                skillId,
                validationResult.ErrorMessage);

            return SkillExecutionResult.ErrorResult(
                validationResult.ErrorMessage ?? "Validation failed",
                null,
                null);
        }

        // Load skill
        var skill = await _repository.GetByIdAsync(skillId, cancellationToken);
        if (skill == null)
        {
            // Should not happen after validation, but defensive check
            return SkillExecutionResult.ErrorResult("Skill not found after validation");
        }

        // Execute the skill
        try
        {
            var stopwatch = Stopwatch.StartNew();
            var result = await ExecuteSkillFileAsync(skill.FilePath, parameters, cancellationToken);
            stopwatch.Stop();

            _logger.LogInformation(
                "Skill {SkillId} execution completed in {ElapsedMs}ms with exit code {ExitCode}",
                skillId,
                stopwatch.ElapsedMilliseconds,
                result.ExitCode);

            if (result.Success)
            {
                await LogAuditSafeAsync(
                    skillId,
                    SkillAuditEventType.Executed,
                    principal.PrincipalId,
                    new Dictionary<string, string>
                    {
                        ["exitCode"] = result.ExitCode?.ToString() ?? "0",
                        ["executionTimeMs"] = stopwatch.ElapsedMilliseconds.ToString()
                    },
                    cancellationToken).ConfigureAwait(false);
            }
            else
            {
                await LogAuditSafeAsync(
                    skillId,
                    SkillAuditEventType.ExecutionDenied,
                    principal.PrincipalId,
                    new Dictionary<string, string>
                    {
                        ["exitCode"] = result.ExitCode?.ToString() ?? "-1",
                        ["reason"] = result.ErrorMessage ?? "Execution failed"
                    },
                    cancellationToken).ConfigureAwait(false);
            }

            return result with { ExecutionTimeMs = stopwatch.ElapsedMilliseconds };
        }
        catch (Exception ex)
        {
            await LogAuditSafeAsync(
                skillId,
                SkillAuditEventType.ExecutionDenied,
                principal.PrincipalId,
                new Dictionary<string, string>
                {
                    ["reason"] = ex.Message
                },
                cancellationToken).ConfigureAwait(false);

            _logger.LogError(
                ex,
                "Skill {SkillId} execution threw exception: {Message}",
                skillId,
                ex.Message);

            return SkillExecutionResult.ErrorResult(
                $"Execution failed with exception: {ex.Message}",
                ex.ToString(),
                null);
        }
    }

    private async Task<SkillExecutionResult> ExecuteSkillFileAsync(
        string skillFilePath,
        IDictionary<string, string> parameters,
        CancellationToken cancellationToken)
    {
        // Create temporary directory for execution
        var tempDir = Path.Combine(Path.GetTempPath(), $"daiv3-skill-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        try
        {
            // Create temporary project file
            var projectFile = Path.Combine(tempDir, "SkillRunner.csproj");
            var projectContent = CreateProjectFileContent();
            await File.WriteAllTextAsync(projectFile, projectContent, cancellationToken);

            // Copy skill file to temp directory as Program.cs
            var programFile = Path.Combine(tempDir, "Program.cs");
            File.Copy(skillFilePath, programFile);

            // Build command-line arguments
            var args = BuildCommandLineArguments(parameters);

            // Execute via dotnet run
            var processResult = await RunDotnetProcessAsync(tempDir, args, cancellationToken);

            // Parse output
            if (processResult.ExitCode == 0 && !string.IsNullOrWhiteSpace(processResult.StandardOutput))
            {
                // Try to parse as JSON
                var parsedOutput = TryParseJsonOutput(processResult.StandardOutput);
                return SkillExecutionResult.SuccessResult(
                    parsedOutput ?? processResult.StandardOutput,
                    processResult.StandardOutput,
                    processResult.ExitCode,
                    0); // ExecutionTimeMs will be set by caller
            }
            else
            {
                return SkillExecutionResult.ErrorResult(
                    $"Skill execution failed with exit code {processResult.ExitCode}",
                    processResult.StandardError,
                    processResult.ExitCode);
            }
        }
        finally
        {
            // Clean up temporary directory
            try
            {
                Directory.Delete(tempDir, recursive: true);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to clean up temporary directory {TempDir}", tempDir);
            }
        }
    }

    private string CreateProjectFileContent()
    {
        return @"<Project Sdk=""Microsoft.NET.Sdk"">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>
</Project>";
    }

    private string BuildCommandLineArguments(IDictionary<string, string> parameters)
    {
        if (parameters.Count == 0)
            return string.Empty;

        var args = parameters.Select(kvp => $"--{kvp.Key} \"{kvp.Value}\"");
        return string.Join(" ", args);
    }

    private async Task<ProcessExecutionResult> RunDotnetProcessAsync(
        string workingDirectory,
        string arguments,
        CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = $"run --verbosity quiet -- {arguments}",
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = new Process { StartInfo = startInfo };

        var outputBuilder = new StringBuilder();
        var errorBuilder = new StringBuilder();

        process.OutputDataReceived += (sender, e) =>
        {
            if (e.Data != null)
                outputBuilder.AppendLine(e.Data);
        };

        process.ErrorDataReceived += (sender, e) =>
        {
            if (e.Data != null)
                errorBuilder.AppendLine(e.Data);
        };

        _logger.LogDebug(
            "Starting dotnet process: {FileName} {Arguments}",
            startInfo.FileName,
            startInfo.Arguments);

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        await process.WaitForExitAsync(cancellationToken);

        return new ProcessExecutionResult(
            process.ExitCode,
            outputBuilder.ToString().TrimEnd(),
            errorBuilder.ToString().TrimEnd());
    }

    private string? TryParseJsonOutput(string output)
    {
        try
        {
            // Try to find JSON in the output
            var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            foreach (var line in lines)
            {
                var trimmed = line.Trim();
                if (trimmed.StartsWith("{") || trimmed.StartsWith("["))
                {
                    // Try to parse as JSON
                    var jsonDocument = JsonDocument.Parse(trimmed);
                    return trimmed; // Valid JSON found
                }
            }
        }
        catch (JsonException)
        {
            // Not valid JSON, return null
        }

        return null;
    }

    private record ProcessExecutionResult(
        int ExitCode,
        string StandardOutput,
        string StandardError);

    private async Task LogAuditSafeAsync(
        string skillId,
        SkillAuditEventType eventType,
        string actorId,
        IDictionary<string, string>? metadata,
        CancellationToken cancellationToken)
    {
        try
        {
            await _skillAuditService.LogEventAsync(
                skillId,
                eventType.ToString(),
                actorId,
                metadata,
                cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            // Audit logging must not block skill execution.
            _logger.LogWarning(
                ex,
                "Failed to log audit event {EventType} for skill {SkillId}",
                eventType,
                skillId);
        }
    }
}
