using Daiv3.Orchestration;
using Daiv3.Orchestration.Interfaces;
using Daiv3.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Daiv3.IntegrationTests.Orchestration;

/// <summary>
/// Integration tests for AST-REQ-003 dynamic task-type agent creation.
/// </summary>
public sealed class DynamicAgentCreationIntegrationTests : IAsyncLifetime
{
    private readonly string _dbPath;
    private readonly ServiceProvider _serviceProvider;

    public DynamicAgentCreationIntegrationTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"daiv3-dynamic-agent-{Guid.NewGuid():N}.db");

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddPersistence(options => options.DatabasePath = _dbPath);
        services.AddOrchestrationServices();

        _serviceProvider = services.BuildServiceProvider();
        _serviceProvider.InitializeDatabaseAsync().GetAwaiter().GetResult();
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        await _serviceProvider.DisposeAsync();

        for (var attempt = 0; attempt < 3; attempt++)
        {
            try
            {
                if (File.Exists(_dbPath))
                {
                    File.Delete(_dbPath);
                }
                break;
            }
            catch (IOException) when (attempt < 2)
            {
                Thread.Sleep(50);
            }
            catch
            {
                break;
            }
        }
    }

    [Fact]
    public async Task GetOrCreateAgentForTaskTypeAsync_AcrossScopes_ReusesPersistedAgent()
    {
        using var firstScope = _serviceProvider.CreateScope();
        var firstManager = firstScope.ServiceProvider.GetRequiredService<IAgentManager>();

        var created = await firstManager.GetOrCreateAgentForTaskTypeAsync("analyze");

        using var secondScope = _serviceProvider.CreateScope();
        var secondManager = secondScope.ServiceProvider.GetRequiredService<IAgentManager>();

        var reused = await secondManager.GetOrCreateAgentForTaskTypeAsync("analyze");

        Assert.Equal(created.Id, reused.Id);
        Assert.Equal(created.Name, reused.Name);
        Assert.Equal("analyze", reused.Config["task_type"]);
        Assert.Equal("dynamic", reused.Config["creation_mode"]);
    }
}
