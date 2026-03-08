using Daiv3.Core;
using Daiv3.Core.Authorization;
using Daiv3.Core.Enums;
using Daiv3.Orchestration;
using Daiv3.Orchestration.Services;
using Daiv3.Persistence;
using Daiv3.Persistence.Entities;
using Daiv3.Persistence.Repositories;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using Xunit;

namespace Daiv3.Orchestration.IntegrationTests;

/// <summary>
/// Integration tests for executable skill execution with real file system and database.
/// </summary>
public class ExecutableSkillExecutionTests : IAsyncLifetime
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IExecutableSkillRunner _skillRunner;
    private readonly IExecutableSkillRepository _skillRepository;
    private readonly IExecutableSkillApprovalService _approvalService;
    private readonly DatabaseContext _databaseContext;
    private readonly string _dbPath;
    private readonly string _testSkillsDirectory;
    private readonly SystemPrincipal _testPrincipal;

    public ExecutableSkillExecutionTests()
    {
        var services = new ServiceCollection();
        services.AddLogging(x => x.AddConsole().SetMinimumLevel(LogLevel.Debug));
        _dbPath = Path.Combine(Path.GetTempPath(), $"daiv3-exec-skill-test-{Guid.NewGuid():N}.db");
        _testSkillsDirectory = Path.Combine(Path.GetTempPath(), $"daiv3-test-skills-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testSkillsDirectory);

        // Register persistence
        services.AddPersistence(options => options.DatabasePath = _dbPath);

        // Register orchestration
        services.AddOrchestrationServices();

        _serviceProvider = services.BuildServiceProvider();
        _skillRunner = _serviceProvider.GetRequiredService<IExecutableSkillRunner>();
        _skillRepository = _serviceProvider.GetRequiredService<IExecutableSkillRepository>();
        _approvalService = _serviceProvider.GetRequiredService<IExecutableSkillApprovalService>();
        _databaseContext = _serviceProvider.GetRequiredService<DatabaseContext>();

        _testPrincipal = SystemPrincipal.CreateUser("test-admin-user", SystemRoles.SkillAdministrator);
    }

    public async Task InitializeAsync()
    {
        // Initialize database
        await _databaseContext.InitializeAsync();
    }

    public async Task DisposeAsync()
    {
        // Cleanup database
        if (_databaseContext is IAsyncDisposable asyncDisposable)
        {
            await asyncDisposable.DisposeAsync();
        }

        if (File.Exists(_dbPath))
        {
            File.Delete(_dbPath);
        }

        // Cleanup test skills directory
        if (Directory.Exists(_testSkillsDirectory))
        {
            Directory.Delete(_testSkillsDirectory, recursive: true);
        }
    }

    #region Test Helpers

    private async Task<ExecutableSkill> CreateAndApproveSkillAsync(string name, string code)
    {
        var filePath = Path.Combine(_testSkillsDirectory, $"{name}.cs");
        await File.WriteAllTextAsync(filePath, code);

        var skill = new ExecutableSkill
        {
            SkillId = Guid.NewGuid().ToString(),
            Name = name,
            FilePath = filePath,
            FileHash = await ComputeHashAsync(filePath),
            ApprovalStatus = ApprovalStatus.PendingApproval.ToString(),
            CreatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            LastModifiedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
        };

        await _skillRepository.AddAsync(skill);

        // Auto-approve for tests
        await _approvalService.ApproveSkillAsync(skill.SkillId, _testPrincipal.PrincipalId);

        return await _skillRepository.GetByIdAsync(skill.SkillId) 
               ?? throw new InvalidOperationException("Failed to retrieve created skill");
    }

    private static async Task<string> ComputeHashAsync(string filePath)
    {
        using var sha256 = System.Security.Cryptography.SHA256.Create();
        await using var stream = File.OpenRead(filePath);
        var hash = await sha256.ComputeHashAsync(stream);
        return Convert.ToHexString(hash);
    }

    #endregion

    [Fact]
    public async Task ExecuteAsync_SimpleHelloWorldSkill_Succeeds()
    {
        // Arrange
        const string code = @"
Console.WriteLine(""Hello from executable skill!"");
";
        var skill = await CreateAndApproveSkillAsync("HelloWorld", code);

        // Act
        var result = await _skillRunner.ExecuteAsync(skill.SkillId, new Dictionary<string, string>(), _testPrincipal);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(0, result.ExitCode);
        Assert.Contains("Hello from executable skill!", result.StandardOutput);
        Assert.Empty(result.StandardError);
        Assert.True(result.ExecutionTimeMs > 0);
    }

    [Fact]
    public async Task ExecuteAsync_SkillWithParameters_ReceivesArgumentsViaCommandLine()
    {
        // Arrange
        const string code = @"
Console.WriteLine($""Received {args.Length} arguments"");
foreach (var arg in args)
{
    Console.WriteLine($""Arg: {arg}"");
}
";
        var skill = await CreateAndApproveSkillAsync("ParameterTest", code);
        var parameters = new Dictionary<string, string>
        {
            ["name"] = "Alice",
            ["count"] = "42"
        };

        // Act
        var result = await _skillRunner.ExecuteAsync(skill.SkillId, parameters, _testPrincipal);

        // Assert
        Assert.True(result.Success);
        Assert.Contains("Received 2 arguments", result.StandardOutput);
        Assert.Contains("--name", result.StandardOutput);
        Assert.Contains("Alice", result.StandardOutput);
        Assert.Contains("--count", result.StandardOutput);
        Assert.Contains("42", result.StandardOutput);
    }

    [Fact]
    public async Task ExecuteAsync_SkillWithJsonOutput_ParsesStructuredResult()
    {
        // Arrange
        const string code = @"
using System.Text.Json;
var result = new { Status = ""success"", Value = 42, Message = ""Computed successfully"" };
Console.WriteLine(JsonSerializer.Serialize(result));
";
        var skill = await CreateAndApproveSkillAsync("JsonOutput", code);

        // Act
        var result = await _skillRunner.ExecuteAsync(skill.SkillId, new Dictionary<string, string>(), _testPrincipal);

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.Output);
        
        var json = JsonSerializer.Deserialize<JsonElement>(result.Output.ToString() ?? "{}");
        Assert.Equal("success", json.GetProperty("Status").GetString());
        Assert.Equal(42, json.GetProperty("Value").GetInt32());
    }

    [Fact]
    public async Task ExecuteAsync_SkillWithNonZeroExitCode_ReturnsError()
    {
        // Arrange
        const string code = @"
Console.WriteLine(""Starting operation..."");
Console.Error.WriteLine(""Fatal error occurred"");
Environment.Exit(1);
";
        var skill = await CreateAndApproveSkillAsync("FailingSkill", code);

        // Act
        var result = await _skillRunner.ExecuteAsync(skill.SkillId, new Dictionary<string, string>(), _testPrincipal);

        // Assert
        Assert.False(result.Success);
        Assert.Equal(1, result.ExitCode);
        Assert.Contains("Fatal error occurred", result.StandardError);
        Assert.NotEmpty(result.ErrorMessage);
    }

    [Fact]
    public async Task ExecuteAsync_SkillWithStderrOutput_CapturesErrorStream()
    {
        // Arrange
        const string code = @"
Console.WriteLine(""Standard output"");
Console.Error.WriteLine(""Warning: something might be wrong"");
Console.Error.WriteLine(""Another error message"");
";
        var skill = await CreateAndApproveSkillAsync("StderrTest", code);

        // Act
        var result = await _skillRunner.ExecuteAsync(skill.SkillId, new Dictionary<string, string>(), _testPrincipal);

        // Assert
        Assert.True(result.Success); // Exit code 0 still means success
        Assert.Contains("Standard output", result.StandardOutput);
        Assert.Contains("Warning: something might be wrong", result.StandardError);
        Assert.Contains("Another error message", result.StandardError);
    }

    [Fact]
    public async Task ExecuteAsync_NotApprovedSkill_ReturnsErrorWithRemediationGuidance()
    {
        // Arrange
        const string code = @"Console.WriteLine(""Hello"");";
        var filePath = Path.Combine(_testSkillsDirectory, "NotApproved.cs");
        await File.WriteAllTextAsync(filePath, code);

        var skill = new ExecutableSkill
        {
            SkillId = Guid.NewGuid().ToString(),
            Name = "NotApproved",
            FilePath = filePath,
            FileHash = await ComputeHashAsync(filePath),
            ApprovalStatus = ApprovalStatus.PendingApproval.ToString(),
            CreatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            LastModifiedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
        };
        await _skillRepository.AddAsync(skill);

        // Act
        var result = await _skillRunner.ExecuteAsync(skill.SkillId, new Dictionary<string, string>(), _testPrincipal);

        // Assert
        Assert.False(result.Success);
        Assert.Contains("approval", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("administrator", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExecuteAsync_TamperedSkill_ReturnsIntegrityErrorWithRemediationGuidance()
    {
        // Arrange
        const string code = @"Console.WriteLine(""Hello"");";
        var skill = await CreateAndApproveSkillAsync("TamperTest", code);

        // Tamper with the file after approval
        await File.WriteAllTextAsync(skill.FilePath, @"Console.WriteLine(""Malicious code"");");

        // Act
        var result = await _skillRunner.ExecuteAsync(skill.SkillId, new Dictionary<string, string>(), _testPrincipal);

        // Assert
        Assert.False(result.Success);
        Assert.Contains("integrity", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("re-approval", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExecuteAsync_SkillNotFound_ReturnsError()
    {
        // Act
        var result = await _skillRunner.ExecuteAsync("non-existent-skill-id", new Dictionary<string, string>(), _testPrincipal);

        // Assert
        Assert.False(result.Success);
        Assert.Contains("not found", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExecuteAsync_FileDeleted_ReturnsFileNotFoundError()
    {
        // Arrange
        const string code = @"Console.WriteLine(""Hello"");";
        var skill = await CreateAndApproveSkillAsync("DeleteTest", code);

        // Delete the file after approval
        File.Delete(skill.FilePath);

        // Act
        var result = await _skillRunner.ExecuteAsync(skill.SkillId, new Dictionary<string, string>(), _testPrincipal);

        // Assert
        Assert.False(result.Success);
        Assert.Contains("not found", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ValidateBeforeExecutionAsync_AllChecksPass_ReturnsSuccess()
    {
        // Arrange
        const string code = @"Console.WriteLine(""Test"");";
        var skill = await CreateAndApproveSkillAsync("ValidationTest", code);

        // Act
        var validation = await _skillRunner.ValidateBeforeExecutionAsync(skill.SkillId);

        // Assert
        Assert.True(validation.IsValid);
        Assert.Null(validation.ErrorMessage);
        Assert.Null(validation.ErrorCode);
    }

    [Fact]
    public async Task ValidateBeforeExecutionAsync_MultipleIssues_ReturnsFirstFailure()
    {
        // Arrange - skill doesn't exist
        // Act
        var validation = await _skillRunner.ValidateBeforeExecutionAsync("non-existent-id");

        // Assert
        Assert.False(validation.IsValid);
        Assert.Equal("NotFound", validation.ErrorCode);
    }
}
