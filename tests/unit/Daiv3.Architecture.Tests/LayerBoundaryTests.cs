using System.Reflection;
using Xunit;

namespace Daiv3.Architecture.Tests;

/// <summary>
/// Tests that validate layered architecture constraints (ARCH-REQ-001).
/// Ensures no violations of unidirectional dependency rules.
/// </summary>
public class LayerBoundaryTests
{
    /// <summary>
    /// Tests that Persistence Layer has no dependencies on higher layers.
    /// </summary>
    [Fact]
    public void PersistenceLayer_HasNoUpwardDependencies()
    {
        var persistenceAssemblies = new[]
        {
            typeof(Daiv3.Persistence.Class1).Assembly,
            typeof(Daiv3.Infrastructure.Shared.Class1).Assembly,
            typeof(Daiv3.Core.Class1).Assembly,
        };

        var forbiddenNamespaces = new[]
        {
            "Daiv3.Knowledge",
            "Daiv3.ModelExecution",
            "Daiv3.Orchestration",
            "Daiv3.App"  // Presentation layer
        };

        foreach (var assembly in persistenceAssemblies)
        {
            var referencedAssemblies = assembly.GetReferencedAssemblies();
            var violatingReferences = referencedAssemblies
                .Where(r => forbiddenNamespaces.Any(ns => r.Name.StartsWith(ns)))
                .ToList();

            Assert.Empty(violatingReferences);
        }
    }

    /// <summary>
    /// Tests that Knowledge Layer only depends on Persistence Layer.
    /// </summary>
    [Fact]
    public void KnowledgeLayer_OnlyDependsOnPersistenceLayer()
    {
        var knowledgeAssemblies = new[]
        {
            typeof(Daiv3.Knowledge.Class1).Assembly,
            typeof(Daiv3.Knowledge.DocProc.Class1).Assembly,
            typeof(Daiv3.Knowledge.Embedding.Class1).Assembly,
        };

        var forbiddenNamespaces = new[]
        {
            "Daiv3.ModelExecution",
            "Daiv3.Orchestration",
            "Daiv3.App"  // Presentation
        };

        foreach (var assembly in knowledgeAssemblies)
        {
            var referencedAssemblies = assembly.GetReferencedAssemblies();
            var violatingReferences = referencedAssemblies
                .Where(r => forbiddenNamespaces.Any(ns => r.Name.StartsWith(ns)))
                .ToList();

            Assert.Empty(violatingReferences);
        }
    }

    /// <summary>
    /// Tests that Model Execution Layer only depends on Knowledge and Persistence.
    /// </summary>
    [Fact]
    public void ModelExecutionLayer_OnlyDependsOnKnowledgeAndPersistence()
    {
        var modelExecutionAssemblies = new[]
        {
            typeof(Daiv3.ModelExecution.Class1).Assembly,
            typeof(Daiv3.FoundryLocal.Management.FoundryLocalManagementService).Assembly,
            typeof(Daiv3.OnlineProviders.Abstractions.Class1).Assembly,
        };

        var forbiddenNamespaces = new[]
        {
            "Daiv3.Orchestration",
            "Daiv3.App"  // Presentation
        };

        foreach (var assembly in modelExecutionAssemblies)
        {
            var referencedAssemblies = assembly.GetReferencedAssemblies();
            var violatingReferences = referencedAssemblies
                .Where(r => forbiddenNamespaces.Any(ns => r.Name.StartsWith(ns)))
                .ToList();

            Assert.Empty(violatingReferences);
        }
    }

    /// <summary>
    /// Tests that Orchestration Layer only depends on Model Execution, Knowledge, and Persistence.
    /// </summary>
    [Fact]
    public void OrchestrationLayer_OnlyDependsOnLowerLayers()
    {
        var orchestrationAssemblies = new[]
        {
            typeof(Daiv3.Orchestration.Class1).Assembly,
            typeof(Daiv3.Scheduler.Class1).Assembly,
        };

        var forbiddenNamespaces = new[]
        {
            "Daiv3.App"  // Presentation
        };

        foreach (var assembly in orchestrationAssemblies)
        {
            var referencedAssemblies = assembly.GetReferencedAssemblies();
            var violatingReferences = referencedAssemblies
                .Where(r => forbiddenNamespaces.Any(ns => r.Name.StartsWith(ns)))
                .ToList();

            Assert.Empty(violatingReferences);
        }
    }
}

/// <summary>
/// Tests that layer interfaces are properly mockable (ARCH-NFR-001).
/// </summary>
public class LayerInterfaceTests
{
    /// <summary>
    /// Tests that persistence layer exposes mockable interfaces.
    /// </summary>
    [Fact]
    public void PersistenceLayer_ExposesInterfaces()
    {
        // Persistence interfaces
        var interfaceTypes = new[]
        {
            typeof(Daiv3.Persistence.Interfaces.IEntity),
            typeof(Daiv3.Persistence.Interfaces.IRepository<>),
            typeof(Daiv3.Persistence.Interfaces.IDatabaseFactory),
            typeof(Daiv3.Infrastructure.Shared.Hardware.IHardwareDetectionProvider),
        };

        foreach (var interfaceType in interfaceTypes)
        {
            Assert.True(interfaceType.IsInterface, $"{interfaceType.Name} should be an interface");
            Assert.NotEmpty(interfaceType.GetMethods(), $"{interfaceType.Name} should have methods");
        }
    }

    /// <summary>
    /// Tests that knowledge layer exposes mockable interfaces.
    /// </summary>
    [Fact]
    public void KnowledgeLayer_ExposesInterfaces()
    {
        // Knowledge interfaces should exist and be public
        var interfaceTypes = new[]
        {
            typeof(Daiv3.Knowledge.DocProc.Interfaces.IDocumentProcessor),
            typeof(Daiv3.Knowledge.DocProc.Interfaces.ITextChunker),
            typeof(Daiv3.Knowledge.Embedding.Interfaces.IEmbeddingService),
            typeof(Daiv3.Knowledge.Embedding.Interfaces.IVectorSimilarityService),
            typeof(Daiv3.Knowledge.Interfaces.IKnowledgeIndex),
        };

        foreach (var interfaceType in interfaceTypes)
        {
            Assert.True(interfaceType.IsInterface, $"{interfaceType.Name} should be an interface");
            Assert.True(interfaceType.IsPublic, $"{interfaceType.Name} should be public");
        }
    }

    /// <summary>
    /// Tests that model execution layer exposes mockable interfaces.
    /// </summary>
    [Fact]
    public void ModelExecutionLayer_ExposesInterfaces()
    {
        var interfaceTypes = new[]
        {
            typeof(Daiv3.ModelExecution.Interfaces.IModelQueue),
            typeof(Daiv3.ModelExecution.Interfaces.IModelManagementService),
            typeof(Daiv3.ModelExecution.Interfaces.IModelSelector),
            typeof(Daiv3.OnlineProviders.Abstractions.IOnlineProvider),
        };

        foreach (var interfaceType in interfaceTypes)
        {
            Assert.True(interfaceType.IsInterface, $"{interfaceType.Name} should be an interface");
            Assert.True(interfaceType.IsPublic, $"{interfaceType.Name} should be public");
        }
    }

    /// <summary>
    /// Tests that orchestration layer exposes mockable interfaces.
    /// </summary>
    [Fact]
    public void OrchestrationLayer_ExposesInterfaces()
    {
        var interfaceTypes = new[]
        {
            typeof(Daiv3.Orchestration.Interfaces.ITaskOrchestrator),
            typeof(Daiv3.Orchestration.Interfaces.IIntentResolver),
            typeof(Daiv3.Orchestration.Interfaces.IAgentManager),
            typeof(Daiv3.Orchestration.Interfaces.ISkillRegistry),
        };

        foreach (var interfaceType in interfaceTypes)
        {
            Assert.True(interfaceType.IsInterface, $"{interfaceType.Name} should be an interface");
            Assert.True(interfaceType.IsPublic, $"{interfaceType.Name} should be public");
        }
    }
}

/// <summary>
/// Tests that layer configuration follows dependency injection patterns.
/// </summary>
public class LayerConfigurationTests
{
    /// <summary>
    /// Tests that persistence layer can be configured independently.
    /// </summary>
    [Fact]
    public void PersistenceLayer_CanBeConfiguredViaExtensionMethod()
    {
        // Test that AddPersistenceLayer extension method exists
        var extensionMethodType = typeof(Daiv3.Persistence.ServiceCollectionExtensions);
        var method = extensionMethodType.GetMethod(
            "AddPersistenceLayer",
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);

        Assert.NotNull(method);
    }

    /// <summary>
    /// Tests that knowledge layer can be configured independently.
    /// </summary>
    [Fact]
    public void KnowledgeLayer_CanBeConfiguredViaExtensionMethod()
    {
        var extensionMethodType = typeof(Daiv3.Knowledge.ServiceCollectionExtensions);
        var method = extensionMethodType.GetMethod(
            "AddKnowledgeLayer",
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);

        Assert.NotNull(method);
    }

    /// <summary>
    /// Tests that model execution layer can be configured independently.
    /// </summary>
    [Fact]
    public void ModelExecutionLayer_CanBeConfiguredViaExtensionMethod()
    {
        var extensionMethodType = typeof(Daiv3.ModelExecution.ServiceCollectionExtensions);
        var method = extensionMethodType.GetMethod(
            "AddModelExecutionLayer",
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);

        Assert.NotNull(method);
    }

    /// <summary>
    /// Tests that orchestration layer can be configured independently.
    /// </summary>
    [Fact]
    public void OrchestrationLayer_CanBeConfiguredViaExtensionMethod()
    {
        var extensionMethodType = typeof(Daiv3.Orchestration.ServiceCollectionExtensions);
        var method = extensionMethodType.GetMethod(
            "AddOrchestrationLayer",
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);

        Assert.NotNull(method);
    }
}

/// <summary>
/// Tests for layer interface contracts and data contracts.
/// </summary>
public class InterfaceContractTests
{
    /// <summary>
    /// Tests that IRepository interface follows repository pattern.
    /// </summary>
    [Fact]
    public void IRepository_HasExpectedMethods()
    {
        var repositoryInterface = typeof(Daiv3.Persistence.Interfaces.IRepository<>);
        var methods = repositoryInterface.GetMethods();
        var methodNames = methods.Select(m => m.Name).ToList();

        Assert.Contains("GetByIdAsync", methodNames);
        Assert.Contains("GetAllAsync", methodNames);
        Assert.Contains("AddAsync", methodNames);
        Assert.Contains("UpdateAsync", methodNames);
        Assert.Contains("DeleteAsync", methodNames);
    }

    /// <summary>
    /// Tests that IEmbeddingService interface is properly defined.
    /// </summary>
    [Fact]
    public void IEmbeddingService_HasExpectedMethods()
    {
        var embeddingInterface = typeof(Daiv3.Knowledge.Embedding.Interfaces.IEmbeddingService);
        var methods = embeddingInterface.GetMethods();
        var methodNames = methods.Select(m => m.Name).ToList();

        Assert.Contains("GenerateEmbeddingAsync", methodNames);
        Assert.Contains("GenerateBatchEmbeddingsAsync", methodNames);
    }

    /// <summary>
    /// Tests that IKnowledgeIndex interface supports two-tier search.
    /// </summary>
    [Fact]
    public void IKnowledgeIndex_SupportsTwoTierSearch()
    {
        var indexInterface = typeof(Daiv3.Knowledge.Interfaces.IKnowledgeIndex);
        var methods = indexInterface.GetMethods();
        var methodNames = methods.Select(m => m.Name).ToList();

        Assert.Contains("SearchTier1Async", methodNames);
        Assert.Contains("SearchTier2Async", methodNames);
        Assert.Contains("AddDocumentAsync", methodNames);
        Assert.Contains("RemoveDocumentAsync", methodNames);
    }

    /// <summary>
    /// Tests that IModelQueue supports priority levels.
    /// </summary>
    [Fact]
    public void IModelQueue_SupportsPriorityQueuing()
    {
        var queueInterface = typeof(Daiv3.ModelExecution.Interfaces.IModelQueue);
        var methods = queueInterface.GetMethods();
        var methodNames = methods.Select(m => m.Name).ToList();

        Assert.Contains("EnqueueAsync", methodNames);
        Assert.Contains("ProcessAsync", methodNames);
        Assert.Contains("GetStatusAsync", methodNames);
    }

    /// <summary>
    /// Tests that execution priority enum exists and has expected values.
    /// </summary>
    [Fact]
    public void ExecutionPriority_HasExpectedValues()
    {
        var priorityEnum = typeof(Daiv3.ModelExecution.Interfaces.ExecutionPriority);
        Assert.True(priorityEnum.IsEnum);

        var values = Enum.GetNames(priorityEnum);
        Assert.Contains("Immediate", values);
        Assert.Contains("Normal", values);
        Assert.Contains("Background", values);
    }
}

/// <summary>
/// Tests for data contract consistency across layers.
/// </summary>
public class DataContractTests
{
    /// <summary>
    /// Tests that ExecutionRequest has required properties.
    /// </summary>
    [Fact]
    public void ExecutionRequest_HasRequiredProperties()
    {
        var requestType = typeof(Daiv3.ModelExecution.Interfaces.ExecutionRequest);
        var properties = requestType.GetProperties()
            .Select(p => p.Name);

        Assert.Contains("Id", properties);
        Assert.Contains("TaskType", properties);
        Assert.Contains("Content", properties);
        Assert.Contains("Context", properties);
    }

    /// <summary>
    /// Tests that ExecutionResult has required properties.
    /// </summary>
    [Fact]
    public void ExecutionResult_HasRequiredProperties()
    {
        var resultType = typeof(Daiv3.ModelExecution.Interfaces.ExecutionResult);
        var properties = resultType.GetProperties()
            .Select(p => p.Name);

        Assert.Contains("RequestId", properties);
        Assert.Contains("Content", properties);
        Assert.Contains("Status", properties);
        Assert.Contains("CompletedAt", properties);
    }

    /// <summary>
    /// Tests that ResolvedTask has required properties.
    /// </summary>
    [Fact]
    public void ResolvedTask_HasRequiredProperties()
    {
        var taskType = typeof(Daiv3.Orchestration.Interfaces.ResolvedTask);
        var properties = taskType.GetProperties()
            .Select(p => p.Name);

        Assert.Contains("TaskType", properties);
        Assert.Contains("Parameters", properties);
        Assert.Contains("ExecutionOrder", properties);
        Assert.Contains("Dependencies", properties);
    }

    /// <summary>
    /// Tests that AvailableModel has required properties.
    /// </summary>
    [Fact]
    public void AvailableModel_HasRequiredProperties()
    {
        var modelType = typeof(Daiv3.ModelExecution.Interfaces.AvailableModel);
        var properties = modelType.GetProperties()
            .Select(p => p.Name);

        Assert.Contains("Id", properties);
        Assert.Contains("Name", properties);
        Assert.Contains("SizeBytes", properties);
        Assert.Contains("SupportedDevices", properties);
    }
}

/// <summary>
/// Tests for async/cancellation patterns across layers.
/// </summary>
public class AsyncPatternTests
{
    /// <summary>
    /// Tests that IRepository methods are async.
    /// </summary>
    [Fact]
    public void IRepository_AllIOMethods_AreAsync()
    {
        var repositoryInterface = typeof(Daiv3.Persistence.Interfaces.IRepository<>);
        var methods = repositoryInterface.GetMethods()
            .Where(m => m.Name.EndsWith("Async"))
            .ToList();

        Assert.NotEmpty(methods);

        foreach (var method in methods)
        {
            var returnType = method.ReturnType;
            Assert.True(
                returnType.IsGenericType && 
                (returnType.GetGenericTypeDefinition() == typeof(Task<>) || 
                 returnType == typeof(Task)),
                $"{method.Name} should return Task or Task<T>");
        }
    }

    /// <summary>
    /// Tests that IEmbeddingService methods support cancellation.
    /// </summary>
    [Fact]
    public void IEmbeddingService_Methods_SupportCancellation()
    {
        var embeddingInterface = typeof(Daiv3.Knowledge.Embedding.Interfaces.IEmbeddingService);
        var asyncMethods = embeddingInterface.GetMethods()
            .Where(m => m.Name.EndsWith("Async"))
            .ToList();

        foreach (var method in asyncMethods)
        {
            var parameters = method.GetParameters();
            var hasCancellationToken = parameters
                .Any(p => p.ParameterType == typeof(CancellationToken));

            Assert.True(hasCancellationToken,
                $"{method.Name} should have CancellationToken parameter");
        }
    }
}
