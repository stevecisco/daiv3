using Daiv3.ModelExecution;
using Xunit;

namespace Daiv3.UnitTests.ModelExecution;

/// <summary>
/// Unit tests for TaskToModelMappingConfiguration (MQ-REQ-012).
/// </summary>
public class TaskToModelMappingConfigurationTests
{
    [Fact]
    public void GetBestProviderForTaskType_WithMatchingMapping_ReturnsCorrectProvider()
    {
        // Arrange
        var config = new TaskToModelMappingConfiguration
        {
            ProviderMappings = new Dictionary<string, List<TaskToModelMapping>>
            {
                ["openai"] = new()
                {
                    new TaskToModelMapping
                    {
                        ApplicableTaskTypes = new() { "Chat", "Analysis", "Code" },
                        Priority = 10,
                        Enabled = true
                    }
                },
                ["anthropic"] = new()
                {
                    new TaskToModelMapping
                    {
                        ApplicableTaskTypes = new() { "Chat" },
                        Priority = 5,
                        Enabled = true
                    }
                }
            }
        };

        // Act
        var provider = config.GetBestProviderForTaskType("Chat");

        // Assert
        Assert.Equal("openai", provider); // openai has higher priority for Chat
    }

    [Fact]
    public void GetBestProviderForTaskType_WithNoMatch_ReturnsFallback()
    {
        // Arrange
        var config = new TaskToModelMappingConfiguration
        {
            DefaultProviderFallback = "default-provider",
            ProviderMappings = new Dictionary<string, List<TaskToModelMapping>>
            {
                ["openai"] = new()
                {
                    new TaskToModelMapping
                    {
                        ApplicableTaskTypes = new() { "Chat" },
                        Priority = 10,
                        Enabled = true
                    }
                }
            }
        };

        // Act
        var provider = config.GetBestProviderForTaskType("Unknown");

        // Assert
        Assert.Equal("default-provider", provider);
    }

    [Fact]
    public void GetBestProviderForTaskType_WithDisabledMapping_IgnoresIt()
    {
        // Arrange
        var config = new TaskToModelMappingConfiguration
        {
            DefaultProviderFallback = "fallback",
            ProviderMappings = new Dictionary<string, List<TaskToModelMapping>>
            {
                ["openai"] = new()
                {
                    new TaskToModelMapping
                    {
                        ApplicableTaskTypes = new() { "Chat" },
                        Priority = 10,
                        Enabled = false // Disabled
                    }
                }
            }
        };

        // Act
        var provider = config.GetBestProviderForTaskType("Chat");

        // Assert
        Assert.Equal("fallback", provider);
    }

    [Fact]
    public void GetBestProviderForTaskType_RespeetsContextWindowLimit()
    {
        // Arrange
        var config = new TaskToModelMappingConfiguration
        {
            DefaultProviderFallback = "fallback",
            ProviderMappings = new Dictionary<string, List<TaskToModelMapping>>
            {
                ["small-model"] = new()
                {
                    new TaskToModelMapping
                    {
                        ApplicableTaskTypes = new() { "Chat" },
                        Priority = 10,
                        Enabled = true,
                        MaxContextWindowTokens = 2000
                    }
                },
                ["large-model"] = new()
                {
                    new TaskToModelMapping
                    {
                        ApplicableTaskTypes = new() { "Chat" },
                        Priority = 9,
                        Enabled = true,
                        MaxContextWindowTokens = 100000
                    }
                }
            }
        };

        // Act - with request exceeding small model capacity
        var provider = config.GetBestProviderForTaskType("Chat", 5000);

        // Assert
        Assert.Equal("large-model", provider);
    }

    [Fact]
    public void GetBestProviderForTaskType_PrioritizesHigherPriority()
    {
        // Arrange
        var config = new TaskToModelMappingConfiguration
        {
            ProviderMappings = new Dictionary<string, List<TaskToModelMapping>>
            {
                ["provider-low"] = new()
                {
                    new TaskToModelMapping
                    {
                        ApplicableTaskTypes = new() { "Code" },
                        Priority = 1,
                        Enabled = true
                    }
                },
                ["provider-high"] = new()
                {
                    new TaskToModelMapping
                    {
                        ApplicableTaskTypes = new() { "Code" },
                        Priority = 10,
                        Enabled = true
                    }
                }
            }
        };

        // Act
        var provider = config.GetBestProviderForTaskType("Code");

        // Assert
        Assert.Equal("provider-high", provider);
    }

    [Fact]
    public void GetBestProviderForTaskType_CaseInsensitive()
    {
        // Arrange
        var config = new TaskToModelMappingConfiguration
        {
            ProviderMappings = new Dictionary<string, List<TaskToModelMapping>>
            {
                ["openai"] = new()
                {
                    new TaskToModelMapping
                    {
                        ApplicableTaskTypes = new() { "Chat" },
                        Priority = 10,
                        Enabled = true
                    }
                }
            }
        };

        // Act
        var provider1 = config.GetBestProviderForTaskType("chat");
        var provider2 = config.GetBestProviderForTaskType("CHAT");
        var provider3 = config.GetBestProviderForTaskType("ChAt");

        // Assert
        Assert.Equal("openai", provider1);
        Assert.Equal("openai", provider2);
        Assert.Equal("openai", provider3);
    }

    [Fact]
    public void Constructor_DefaultValues()
    {
        // Arrange & Act
        var config = new TaskToModelMappingConfiguration();

        // Assert
        Assert.NotNull(config.ProviderMappings);
        Assert.Empty(config.ProviderMappings);
        Assert.Null(config.DefaultProviderFallback);
        Assert.False(config.PreferLowerCost);
        Assert.True(config.AllowParallelProviderExecution);
        Assert.Equal(10, config.MaxConcurrentRequestsPerProvider);
    }

    [Fact]
    public void TaskToModelMapping_DefaultValues()
    {
        // Arrange & Act
        var mapping = new TaskToModelMapping();

        // Assert
        Assert.NotEmpty(mapping.Id);
        Assert.NotNull(mapping.ApplicableTaskTypes);
        Assert.Empty(mapping.ApplicableTaskTypes);
        Assert.Equal(0, mapping.Priority);
        Assert.True(mapping.Enabled);
        Assert.Equal(0, mapping.CostPer1KInputTokens);
        Assert.Equal(4096, mapping.MaxContextWindowTokens);
    }

    [Fact]
    public void GetBestProviderForTaskType_PreferLowerCost_SelectsCheaperProvider()
    {
        // Arrange
        var config = new TaskToModelMappingConfiguration
        {
            PreferLowerCost = true,
            ProviderMappings = new Dictionary<string, List<TaskToModelMapping>>
            {
                ["expensive"] = new()
                {
                    new TaskToModelMapping
                    {
                        ApplicableTaskTypes = new() { "Chat" },
                        Priority = 10, // High priority but expensive
                        CostPer1KInputTokens = 0.002m,
                        Enabled = true
                    }
                },
                ["cheap"] = new()
                {
                    new TaskToModelMapping
                    {
                        ApplicableTaskTypes = new() { "Chat" },
                        Priority = 5, // Lower priority but cheap
                        CostPer1KInputTokens = 0.0001m,
                        Enabled = true
                    }
                }
            }
        };

        // Act
        var provider = config.GetBestProviderForTaskType("Chat");

        // Assert
        Assert.Equal("cheap", provider);
    }

    [Fact]
    public void GetBestProviderForTaskType_MultipleTaskTypes_MatchesCorrectly()
    {
        // Arrange
        var config = new TaskToModelMappingConfiguration
        {
            ProviderMappings = new Dictionary<string, List<TaskToModelMapping>>
            {
                ["gpt-4"] = new()
                {
                    new TaskToModelMapping
                    {
                        ApplicableTaskTypes = new() { "Code", "Analysis" },
                        Priority = 10,
                        Enabled = true
                    }
                },
                ["gpt-3.5"] = new()
                {
                    new TaskToModelMapping
                    {
                        ApplicableTaskTypes = new() { "Chat" },
                        Priority = 5,
                        Enabled = true
                    }
                }
            }
        };

        // Act
        var codeProvider = config.GetBestProviderForTaskType("Code");
        var chatProvider = config.GetBestProviderForTaskType("Chat");

        // Assert
        Assert.Equal("gpt-4", codeProvider);
        Assert.Equal("gpt-3.5", chatProvider);
    }
}
