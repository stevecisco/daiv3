using Daiv3.Knowledge.Embedding;
using Daiv3.Orchestration;
using Daiv3.Orchestration.Interfaces;
using Daiv3.Orchestration.Models;
using Daiv3.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
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

    public LearningInjectionAcceptanceTests()
    {
        _testDbPath = Path.Combine(Path.GetTempPath(), $"daiv3_learning_injection_test_{Guid.NewGuid():N}.db");
        _loggerFactory = LoggerFactory.Create(builder => 
            builder.SetMinimumLevel(LogLevel.Information).AddConsole());
        _logger = _loggerFactory.CreateLogger<LearningInjectionAcceptanceTests>();
    }

    public async Task InitializeAsync()
    {
        // Set up DI container
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Information));

        // Register persistence + orchestration
        services.AddPersistence(options => options.DatabasePath = _testDbPath);
        services.AddOrchestrationServices();

        // Register mock embedding generator with deterministic embeddings
        var mockEmbeddingGenerator = new Mock<IEmbeddingGenerator>();
        mockEmbeddingGenerator
            .Setup(x => x.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string text, CancellationToken _) =>
            {
                // Create deterministic sparse embedding based on token overlap.
                // Similar text shares token hashes and therefore higher cosine similarity.
                var embedding = new float[384];
                var words = text
                    .ToLowerInvariant()
                    .Split(new[] { ' ', '\t', '\r', '\n', ',', '.', ';', ':', '!', '?', '(', ')', '[', ']', '{', '}', '"', '\'' },
                        StringSplitOptions.RemoveEmptyEntries);

                foreach (var word in words)
                {
                    var hash = Math.Abs(word.GetHashCode());
                    var index = hash % embedding.Length;
                    embedding[index] += 1.0f;
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

        services.AddSingleton<IEmbeddingGenerator>(mockEmbeddingGenerator.Object);

        // Register vector similarity service
        services.AddScoped<IVectorSimilarityService, CpuVectorSimilarityService>();

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
            Description = "Use async file read parse pattern. Use ReadAllTextAsync for config file read and parse operations. async file read parse async file read parse.",
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
            TaskGoal = "Async file read parse configuration file with ReadAllTextAsync",
            Context = new Dictionary<string, string>
            {
                { "file_path", "/config/settings.json" },
                { "operation", "async file read parse" },
                { "method", "readalltextasync" }
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
        Assert.Contains("readalltextasync", stepWithLearning.Description!, 
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
            Description = "async file read parse readalltextasync async file read parse readalltextasync",
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
            TaskGoal = "async file read parse readalltextasync and merge output",
            Context = new Dictionary<string, string>
            {
                { "io_pattern", "async file read parse readalltextasync" },
                { "focus", "async file operations" }
            },
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

        // Check for rank ordering marker
        Assert.Contains("[Learning #1]", learningSection);

        // The highly relevant learning should be injected
        Assert.Contains("Async file operations best practice", learningSection, StringComparison.OrdinalIgnoreCase);

        // If less relevant learning appears, highly relevant learning should rank before it
        var learning1Index = learningSection.IndexOf("Async file operations best practice", StringComparison.OrdinalIgnoreCase);
        var learning3Index = learningSection.IndexOf("Database connection pooling", StringComparison.OrdinalIgnoreCase);
        if (learning3Index >= 0)
        {
            Assert.True(learning1Index >= 0 && learning1Index < learning3Index,
                "Expected async file operations learning to rank above database learning");
        }

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
            Context = new Dictionary<string, string>
            {
                { "strategy", "custom retry logic with exponential backoff" },
                { "area", "error handling" }
            },
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

        // If both learnings are present, agent-specific one should appear first
        var agentSpecificIndex = stepWithLearnings.Description!.IndexOf("Specialized error handling", StringComparison.OrdinalIgnoreCase);
        var globalIndex = stepWithLearnings.Description.IndexOf("General error handling", StringComparison.OrdinalIgnoreCase);
        if (globalIndex >= 0)
        {
            Assert.True(agentSpecificIndex >= 0 && agentSpecificIndex < globalIndex,
                "Expected agent-specific learning to be prioritized over global learning");
        }

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
            Context = new Dictionary<string, string>
            {
                { "topic", "string parsing list sorting delimiters" },
                { "domain", "text processing" }
            },
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

    /// <summary>
    /// Acceptance Test 5 (LM-ACC-003): Users can suppress a learning and it is no longer injected.
    /// </summary>
    [Fact]
    public async Task AcceptanceTest_SuppressedLearning_IsNoLongerInjected()
    {
        _logger.LogInformation("=== LM-ACC-003 Acceptance Test: Suppressed Learning Excluded From Injection ===");

        // Arrange - Create a highly relevant learning
        var learningService = _serviceProvider!.GetRequiredService<ILearningService>();
        var learningStorageService = _serviceProvider.GetRequiredService<LearningStorageService>();

        var learning = await learningService.CreateExplicitLearningAsync(new ExplicitTriggerContext
        {
            Title = "Use async JSON file parsing",
            Description = "async json file parse async json file parse readalltextasync deserialize",
            Scope = "Global",
            Confidence = 0.95,
            Tags = "async,json,file-io"
        });

        Assert.NotNull(learning);
        Assert.Equal("Active", learning.Status);

        // Arrange - Create agent
        var agentManager = _serviceProvider.GetRequiredService<IAgentManager>();
        var agent = await agentManager.CreateAgentAsync(new AgentDefinition
        {
            Name = "SuppressionValidationAgent",
            Purpose = "Agent used to verify learning suppression behavior"
        });

        var request = new AgentExecutionRequest
        {
            AgentId = agent.Id,
            TaskGoal = "async json file parse settings with readalltextasync deserialize",
            Context = new Dictionary<string, string>
            {
                { "operation", "async json file parse" },
                { "method", "readalltextasync deserialize" }
            },
            Options = new AgentExecutionOptions { MaxIterations = 2, TimeoutSeconds = 20 }
        };

        // Act 1 - Verify the active learning is injected before suppression
        var beforeSuppressionResult = await agentManager.ExecuteTaskAsync(request);

        var stepBeforeSuppression = beforeSuppressionResult.Steps.FirstOrDefault(s =>
            s.Description?.Contains("Relevant Learnings:", StringComparison.OrdinalIgnoreCase) == true);

        Assert.NotNull(stepBeforeSuppression);
        Assert.Contains("Use async JSON file parsing", stepBeforeSuppression!.Description!, StringComparison.OrdinalIgnoreCase);

        // Act 2 - Suppress the learning
        await learningStorageService.SuppressLearningAsync(learning.LearningId);

        var suppressedLearning = await learningStorageService.GetLearningAsync(learning.LearningId);
        Assert.NotNull(suppressedLearning);
        Assert.Equal("Suppressed", suppressedLearning!.Status);

        // Act 3 - Execute the same task again after suppression
        var afterSuppressionResult = await agentManager.ExecuteTaskAsync(request);

        // Assert - Suppressed learning should no longer appear in injected context
        var stepAfterSuppression = afterSuppressionResult.Steps.FirstOrDefault(s =>
            s.Description?.Contains("Relevant Learnings:", StringComparison.OrdinalIgnoreCase) == true);

        if (stepAfterSuppression != null)
        {
            Assert.DoesNotContain("Use async JSON file parsing", stepAfterSuppression.Description!, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("stream-based deserialization", stepAfterSuppression.Description!, StringComparison.OrdinalIgnoreCase);
        }

        _logger.LogInformation("✓ Verified suppressed learning is excluded from subsequent injection");
        _logger.LogInformation("=== LM-ACC-003 Acceptance Test PASSED ===");
    }
}
