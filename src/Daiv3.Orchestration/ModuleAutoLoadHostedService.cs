using Daiv3.Orchestration.Configuration;
using Daiv3.Orchestration.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Daiv3.Orchestration;

/// <summary>
/// Auto-discovers skills and agents from configuration files at startup.
/// </summary>
public sealed class ModuleAutoLoadHostedService : IHostedService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ModuleAutoLoadHostedService> _logger;
    private readonly OrchestrationOptions _options;

    public ModuleAutoLoadHostedService(
        IServiceScopeFactory scopeFactory,
        ILogger<ModuleAutoLoadHostedService> logger,
        IOptions<OrchestrationOptions> options)
    {
        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (!_options.EnableModuleAutoDiscovery)
        {
            _logger.LogInformation("Module auto-discovery disabled");
            return;
        }

        using var scope = _scopeFactory.CreateScope();
        await LoadSkillsAsync(scope.ServiceProvider, cancellationToken).ConfigureAwait(false);
        await LoadAgentsAsync(scope.ServiceProvider, cancellationToken).ConfigureAwait(false);
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    private async Task LoadSkillsAsync(IServiceProvider serviceProvider, CancellationToken ct)
    {
        var path = _options.SkillConfigAutoLoadPath;

        if (!File.Exists(path) && !Directory.Exists(path))
        {
            _logger.LogDebug("Skill auto-load path does not exist: {Path}", path);
            return;
        }

        var loader = serviceProvider.GetRequiredService<SkillConfigFileLoader>();
        var registry = serviceProvider.GetRequiredService<ISkillRegistry>();

        var batch = await loader.LoadSkillBatchAsync(path, _options.ModuleAutoLoadRecursive, ct).ConfigureAwait(false);

        if (batch.Skills.Count == 0)
        {
            _logger.LogInformation("No skill configurations found at {Path}", path);
            return;
        }

        var loadedCount = 0;
        foreach (var config in batch.Skills)
        {
            var validation = loader.ValidateConfiguration(config);
            if (!validation.IsValid)
            {
                _logger.LogWarning(
                    "Skipping invalid skill configuration '{SkillName}': {Errors}",
                    config.Name,
                    string.Join("; ", validation.Errors));
                continue;
            }

            if (validation.Warnings.Count > 0)
            {
                _logger.LogWarning(
                    "Skill configuration '{SkillName}' has warnings: {Warnings}",
                    config.Name,
                    string.Join("; ", validation.Warnings));
            }

            var metadata = loader.ToSkillMetadata(config);
            var runtimeSkill = new ConfiguredSkill(metadata, config.Config);
            registry.RegisterSkill(runtimeSkill, metadata.Source);
            loadedCount++;
        }

        _logger.LogInformation("Auto-loaded {LoadedCount} skill(s) from {Path}", loadedCount, path);
    }

    private async Task LoadAgentsAsync(IServiceProvider serviceProvider, CancellationToken ct)
    {
        var path = _options.AgentConfigAutoLoadPath;

        if (!File.Exists(path) && !Directory.Exists(path))
        {
            _logger.LogDebug("Agent auto-load path does not exist: {Path}", path);
            return;
        }

        var loader = serviceProvider.GetRequiredService<AgentConfigFileLoader>();
        var agentManager = serviceProvider.GetRequiredService<IAgentManager>();

        var batch = await loader.LoadAgentBatchAsync(path, _options.ModuleAutoLoadRecursive, ct).ConfigureAwait(false);

        if (batch.Agents.Count == 0)
        {
            _logger.LogInformation("No agent configurations found at {Path}", path);
            return;
        }

        var createdCount = 0;
        foreach (var config in batch.Agents)
        {
            var validation = loader.ValidateConfiguration(config);
            if (!validation.IsValid)
            {
                _logger.LogWarning(
                    "Skipping invalid agent configuration '{AgentName}': {Errors}",
                    config.Name,
                    string.Join("; ", validation.Errors));
                continue;
            }

            if (validation.Warnings.Count > 0)
            {
                _logger.LogWarning(
                    "Agent configuration '{AgentName}' has warnings: {Warnings}",
                    config.Name,
                    string.Join("; ", validation.Warnings));
            }

            var definition = loader.ToAgentDefinition(config);

            try
            {
                await agentManager.CreateAgentAsync(definition, ct).ConfigureAwait(false);
                createdCount++;
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("already exists", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogInformation(
                    "Agent '{AgentName}' already exists, skipping auto-load create",
                    config.Name);
            }
        }

        _logger.LogInformation("Auto-created {CreatedCount} agent(s) from {Path}", createdCount, path);
    }
}
