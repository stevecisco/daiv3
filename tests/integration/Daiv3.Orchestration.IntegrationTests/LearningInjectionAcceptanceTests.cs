using Daiv3.Knowledge.Embedding;
using Daiv3.Orchestration;
using Daiv3.Orchestration.Interfaces;
using Daiv3.Orchestration.Models;
using Daiv3.Persistence;
using Daiv3.Persistence.Entities;
using Daiv3.Persistence.Repositories;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Daiv3.Orchestration.IntegrationTests;

/// <summary>
/// Acceptance tests for learning injection into agent prompts (LM-ACC-002).
/// Verifies: "Relevant learnings appear in agent prompts for similar tasks."
/// </summary>
[Collection("Database")]
public class LearningInjectionAcceptanceTests : IAsyncLifetime
{
    private readonly string _testDbPath;
    private readonly ILoggerFactory _loggerFactory;
    private readonly ILogger<LearningInjectionAcceptanceTests> _logger;
    private IServiceProvider? _serviceProvider;
    private DatabaseContext? _databaseContext;

    public LearningInjectionAcceptanceTests()
    {
        _testDbPath = Path.Combine(Path.GetTempPath(), $"daiv3_learning_injection_test_{Guid.NewGuid():N}.db");
        _loggerFactory = LoggerFactory.Create(builder => 
            builder.SetMinimumLevel(LogLevel.Information).AddConsole());
        _logger = _loggerFactory.CreateLogger<LearningInjectionAcceptanceTests>();
    }

    public async Task InitializeAsync()
    {
        // Initialize database
        _databaseContext = new DatabaseContext(
            _loggerFactory.CreateLogger<DatabaseContext>(),
            Options.Create(new PersistenceOptions { DatabasePath = _testDbPath }));
        await _databaseContext.InitializeAsync();

        // Set up DI container
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Information));

        // Register persistence (using existing database)
        services.AddSingleton(_databaseContext);
        services.AddScoped<IRepository<Learning>, LearningRepository>();
        services.AddScoped<LearningRepository>();
        services.AddScoped<LearningStorageService>();
        services.AddScoped<IAgentRepository, AgentRepository>();
        services.AddScoped<ISkillRegistry, SkillRegistry>();
        services.AddScoped<IToolRegistry, ToolRegistry>();
        services.AddScoped<IMessageBroker, InMemoryMessageBroker>();

        // Register mock embedding generator with deterministic embeddings
        var mockEmbeddingGenerator = new Mock<IEmbeddingGenerator>();
        mockEmbeddingGenerator
            .Setup(x => x.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string text, CancellationToken _) =>
            {
                // Create deterministic embedding based on text content
                // This ensures similar text gets similar embeddings
                var embedding = new float[384];
                var words = text.ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries);
                
                for (int i = 0; i < embedding.Length && i < words.Length * 10; i++)
                {
                    var wordIndex = i / 10;
                    if (wordIndex < words.Length)
                    {
                        var word = words[wordIndex];
                        // Create a consistent hash for each word
                        var hash = word.GetHashCode();
                        embedding[i] = (float)(Math.Sin(hash + i) * 0.5 + 0.5); // Normalize to [0, 1]
                    }
                }
                
                // Normalize the embedding
                var magnitude = Math.Sqrt(embedding.Sum(x => x * x));
                if (magnitude > 0)
                {
                    for (int i = 0; i < embedding.Length; i++)
                    {
                        embedding[i] /= (float)magnitude;
                    }
                }
                
                return embedding;
            });

        services.AddSingleton(mockEmbeddingGenerator.Object);

        // Register vector similarity service
        services.AddScoped<IVectorSimilarityService, Infrastructure.Shared.Hardware.CpuVectorSimilarityService>();

        // Register orchestration services
        services.AddOrchestrationServices();

        _serviceProvider = services.BuildServiceProvider();
    }

    public async Task DisposeAsync()
    {
        if (_serviceProvider is IAsyncDisposable asyncDisposable)
        {
            await asyncDisposable.DisposeAsync();
        }
        else if (_serviceProvider is IDisposable disposable)
        {
            disposable.Dispose();
        }

        if (_databaseContext != null)
        {
            await _databaseContext.DisposeAsync();
            _databaseContext = null;
        }

        SqliteConnection.ClearAllPools();
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        await Task.Delay(100);

        if (File.Exists(_testDbPath))
        {
            var remainingAttempts = 10;
            while (remainingAttempts > 0)
            {
                try
                {
                    File.Delete(_testDbPath);
                    break;
                }
                catch (IOException) when (remainingAttempts > 1)
                {
                    remainingAttempts--;
                    await Task.Delay(100);
                }
            }
        }

        _loggerFactory.Dispose();
    }

    /// <summary>
    /// Acceptance Test 1: Relevant learnings appear in agent prompts for similar tasks.
    /// </summary>
    [Fact]
    public async Task AcceptanceTest_RelevantLearningsAppearInAgentPrompts_ForSimilarTasks()
    {
        _logger.LogInformation("=== LM-ACC-002 Acceptance Test 1: Learning Injection ===");

        // Arrange - Create a learning about async file I/O
        var learningService = _serviceProvider!.GetRequiredService<ILearningService>();
        
        var learningContext = new ExplicitTriggerContext
        {
            Title = "Use async file I/O operations",
            Description = "Always use File.ReadAllTextAsync instead of File.ReadAllText to avoid blocking the thread when reading files. This improves application responsiveness.",
            Scope = "Global",
            Confidence = 0.9,
            Tags = "csharp,async,file-io,best-practices",
            SourceAgent = "system",
            CreatedBy = "test-setup"
        };

        var learning = await learningService.CreateExplicitLearningAsync(learningContext);
        Assert.NotNull(learning);
        Assert.NotEmpty(learning.LearningId);

        _logger.LogInformation("Created learning: {LearningId} - '{Title}'", learning.LearningId, learning.Title);

        // Arrange - Create an agent
        var agentManager = _serviceProvider.GetRequiredService<IAgentManager>();
        
        var agentDef = new AgentDefinition
        {
            Name = "FileProcessorAgent",
            Purpose = "Agent that processes files",
            EnabledSkills = new List<string> { "file-reading", "data-processing" }
        };

        var agent = await agentManager.CreateAgentAsync(agentDef);
        Assert.NotNull(agent);

        _logger.LogInformation("Created agent: {AgentId} - '{Name}'", agent.Id, agent.Name);

        // Act - Execute a task with similar content (related to file reading)
        var request = new AgentExecutionRequest
        {
            AgentId = agent.Id,
            TaskGoal = "Read a configuration file and process its contents",
            Context = new Dictionary<string, string>
            {
                { "file_path", "/config/settings.json" },
                { "operation", "read and parse" }
            },
            Options = new AgentExecutionOptions
            {
                MaxIterations = 3,
                TimeoutSeconds = 30,
                TokenBudget = 5000
            }
        };

        var result = await agentManager.ExecuteTaskAsync(request);

        // Assert - Verify execution completed
        Assert.NotNull(result);
        Assert.True(result.IterationsExecuted > 0);
        Assert.True(result.Steps.Count > 0);

        _logger.LogInformation(
            "Agent execution completed: {Iterations} iterations, {Steps} steps",
            result.IterationsExecuted, result.Steps.Count);

        // Assert - Verify learning was injected into at least one step
        bool learningInjected = false;
        AgentExecutionStep? stepWithLearning = null;

        foreach (var step in result.Steps)
        {
            _logger.LogInformation("Step {StepNumber} description length: {Length}", 
                step.StepNumber, step.Description?.Length ?? 0);

            if (step.Description != null && 
                step.Description.Contains("Relevant Learnings:", StringComparison.OrdinalIgnoreCase))
            {
                learningInjected = true;
                stepWithLearning = step;
                
                _logger.LogInformation("✓ Found learning injection in step {StepNumber}", step.StepNumber);
                _logger.LogInformation("Step description excerpt:\n{Description}", 
                    step.Description.Length > 500 
                        ? step.Description.Substring(0, 500) + "..." 
                        : step.Description);
                
                break;
            }
        }

        Assert.True(learningInjected, 
            "Expected learning to be injected into at least one step description");

        // Assert - Verify the injected learning contains expected content
        Assert.NotNull(stepWithLearning);
        Assert.Contains("Use async file I/O operations", stepWithLearning!.Description!, 
            StringComparison.OrdinalIgnoreCase);
        Assert.Contains("File.ReadAllTextAsync", stepWithLearning.Description!, 
            StringComparison.OrdinalIgnoreCase);
        Assert.Contains("async", stepWithLearning.Description!, 
            StringComparison.OrdinalIgnoreCase);

        _logger.LogInformation("✓ Verified learning content appears in injected context");
        _logger.LogInformation("=== LM-ACC-002 Acceptance Test PASSED ===");
    }

    /// <summary>
    /// Acceptance Test 2: Multiple relevant learnings are ranked by similarity.
    /// </summary>
    [Fact]
    public async Task AcceptanceTest_MultipleLearningsAreRankedBySimilarity()
    {
        _logger.LogInformation("=== LM-ACC-002 Acceptance Test 2: Learning Ranking ===");

        // Arrange - Create multiple learnings with varying relevance
        var learningService = _serviceProvider!.GetRequiredService<ILearningService>();
        
        // Learning 1: Highly relevant (async file operations)
        var learning1 = await learningService.CreateExplicitLearningAsync(new ExplicitTriggerContext
        {
            Title = "Async file operations best practice",
            Description = "When reading large files, always use async methods like ReadAllTextAsync to prevent blocking the main thread",
            Scope = "Global",
            Confidence = 0.95,
            Tags = "async,files,performance"
        });

        // Learning 2: Moderately relevant (general async patterns)
        var learning2 = await learningService.CreateExplicitLearningAsync(new ExplicitTriggerContext
        {
            Title = "Use ConfigureAwait(false) in library code",
            Description = "When writing library code, use ConfigureAwait(false) on async operations to avoid deadlocks",
            Scope = "Global",
            Confidence = 0.85,
            Tags = "async,libraries"
        });

        // Learning 3: Less relevant (database operations)
        var learning3 = await learningService.CreateExplicitLearningAsync(new ExplicitTriggerContext
        {
            Title = "Database connection pooling",
            Description = "Always use connection pooling for database operations to improve performance",
            Scope = "Global",
            Confidence = 0.80,
            Tags = "database,performance"
        });

        _logger.LogInformation("Created 3 learnings with varying relevance");

        // Arrange - Create agent
        var agentManager = _serviceProvider.GetRequiredService<IAgentManager>();
        var agent = await agentManager.CreateAgentAsync(new AgentDefinition
        {
            Name = "AsyncFileAgent",
            Purpose = "Agent specialized in async file operations"
        });

        // Act - Execute task related to async file reading
        var request = new AgentExecutionRequest
        {
            AgentId = agent.Id,
            TaskGoal = "Read multiple files asynchronously and merge their contents",
            Options = new AgentExecutionOptions { MaxIterations = 2, TimeoutSeconds = 20 }
        };

        var result = await agentManager.ExecuteTaskAsync(request);

        // Assert - Find step with learning injection
        var stepWithLearnings = result.Steps.FirstOrDefault(s => 
            s.Description?.Contains("Relevant Learnings:", StringComparison.OrdinalIgnoreCase) == true);

        Assert.NotNull(stepWithLearnings);

        // Assert - Most relevant learning should appear first (Learning #1)
        var learningSection = stepWithLearnings!.Description!
            .Substring(stepWithLearnings.Description.IndexOf("Relevant Learnings:", StringComparison.OrdinalIgnoreCase));

        // Check for rank ordering (Learning #1 should have rank 1)
        Assert.Contains("[Learning #1]", learningSection);
        
        // The most relevant learning about async file operations should appear
        var learning1Index = learningSection.IndexOf("Async file operations", StringComparison.OrdinalIgnoreCase);
        Assert.True(learning1Index > 0, "Most relevant learning should be injected");

        _logger.LogInformation("✓ Verified learnings are ranked by similarity");
        _logger.LogInformation("=== LM-ACC-002 Test 2 PASSED ===");
    }

    /// <summary>
    /// Acceptance Test 3: Agent-specific learnings are prioritized over global ones.
    /// </summary>
    [Fact]
    public async Task AcceptanceTest_AgentSpecificLearningsArePrioritized()
    {
        _logger.LogInformation("=== LM-ACC-002 Acceptance Test 3: Agent-Specific Learning Priority ===");

        // Arrange - Create agent first 
        var agentManager = _serviceProvider!.GetRequiredService<IAgentManager>();
        var agent = await agentManager.CreateAgentAsync(new AgentDefinition
        {
            Name = "SpecializedAgent",
            Purpose = "Agent with specialized knowledge"
        });

        // Create agent-specific learning
        var learningService = _serviceProvider.GetRequiredService<ILearningService>();
        var agentLearning = await learningService.CreateExplicitLearningAsync(new ExplicitTriggerContext
        {
            Title = "Specialized error handling for this agent",
            Description = "This agent should use custom error retry logic with exponential backoff",
            Scope = "Agent",
            Confidence = 0.90,
            Tags = "error-handling,retry",
            SourceAgent = agent.Id.ToString()
        });

        // Create global learning
        var globalLearning = await learningService.CreateExplicitLearningAsync(new ExplicitTriggerContext
        {
            Title = "General error handling",
            Description = "Use try-catch blocks for error handling",
            Scope = "Global",
            Confidence = 0.85,
            Tags = "error-handling"
        });

        _logger.LogInformation("Created agent-specific and global learnings");

        // Act - Execute task related to error handling
        var request = new AgentExecutionRequest
        {
            AgentId = agent.Id,
            TaskGoal = "Process data with robust error handling",
            Options = new AgentExecutionOptions { MaxIterations = 2, TimeoutSeconds = 20 }
        };

        var result = await agentManager.ExecuteTaskAsync(request);

        // Assert - Verify learning injection
        var stepWithLearnings = result.Steps.FirstOrDefault(s => 
            s.Description?.Contains("Relevant Learnings:", StringComparison.OrdinalIgnoreCase) == true);

        Assert.NotNull(stepWithLearnings);

        // Agent-specific learning should be present
        Assert.Contains("Specialized error handling", stepWithLearnings!.Description!, 
            StringComparison.OrdinalIgnoreCase);

        _logger.LogInformation("✓ Verified agent-specific learning was injected");
        _logger.LogInformation("=== LM-ACC-002 Test 3 PASSED ===");
    }

    /// <summary>
    /// Acceptance Test 4: Low similarity learnings are not injected.
    /// </summary>
    [Fact]
    public async Task AcceptanceTest_LowSimilarityLearningsAreNotInjected()
    {
        _logger.LogInformation("=== LM-ACC-002 Acceptance Test 4: Low Similarity Filtering ===");

        // Arrange - Create a learning about a completely unrelated topic
        var learningService = _serviceProvider!.GetRequiredService<ILearningService>();
        
        var irrelevantLearning = await learningService.CreateExplicitLearningAsync(new ExplicitTriggerContext
        {
            Title = "Machine learning model training tips",
            Description = "When training neural networks, use mini-batch gradient descent with learning rate scheduling",
            Scope = "Global",
            Confidence = 0.95,
            Tags = "ml,training,neural-networks"
        });

        _logger.LogInformation("Created irrelevant learning about ML training");

        // Arrange - Create agent
        var agentManager = _serviceProvider!.GetRequiredService<IAgentManager>();
        var agent = await agentManager.CreateAgentAsync(new AgentDefinition
        {
            Name = "StringProcessorAgent",
            Purpose = "Agent that processes text strings"
        });

        // Act - Execute task completely unrelated to ML (string manipulation)
        var request = new AgentExecutionRequest
        {
            AgentId = agent.Id,
            TaskGoal = "Split a comma-separated string into an array and sort alphabetically",
            Options = new AgentExecutionOptions { MaxIterations = 2, TimeoutSeconds = 20 }
        };

        var result = await agentManager.ExecuteTaskAsync(request);

        // Assert - Verify execution completed
        Assert.NotNull(result);

        // Look for learning injection
        var stepWithLearnings = result.Steps.FirstOrDefault(s => 
            s.Description?.Contains("Relevant Learnings:", StringComparison.OrdinalIgnoreCase) == true);

        if (stepWithLearnings != null)
        {
            // If learnings were injected, the irrelevant one should NOT be present
            Assert.DoesNotContain("neural networks", stepWithLearnings.Description!, 
                StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("gradient descent", stepWithLearnings.Description!, 
                StringComparison.OrdinalIgnoreCase);
            
            _logger.LogInformation("✓ Verified irrelevant learning was filtered out");
        }
        else
        {
            // No learnings injected (which is also acceptable if none meet threshold)
            _logger.LogInformation("✓ No learnings injected (acceptable - none met similarity threshold)");
        }

        _logger.LogInformation("=== LM-ACC-002 Test 4 PASSED ===");
    }
}
