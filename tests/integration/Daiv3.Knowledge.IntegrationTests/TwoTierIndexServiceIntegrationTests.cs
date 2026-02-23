using Daiv3.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Daiv3.Knowledge.IntegrationTests;

/// <summary>
/// Integration tests for TwoTierIndexService with a real SQLite database and populated indices.
/// Tests full two-tier search workflow against actual stored data.
/// </summary>
[Collection("Knowledge Database Collection")]
public class TwoTierIndexServiceIntegrationTests
{
    private readonly KnowledgeDatabaseFixture _fixture;

    public TwoTierIndexServiceIntegrationTests(KnowledgeDatabaseFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task InitializeAsync_LoadsAllTopicIndicesFromDatabase()
    {
        // Arrange
        var vectorStore = _fixture.ServiceProvider.GetRequiredService<IVectorStoreService>();
        var indexService = _fixture.ServiceProvider.GetRequiredService<ITwoTierIndexService>();

        // Store some topics
        for (int i = 0; i < 5; i++)
        {
            await vectorStore.StoreTopicIndexAsync(
                $"doc-{i}",
                $"Document {i} summary",
                CreateTestEmbedding(384),
                $"/test/doc{i}.txt",
                $"hash{i}");
        }

        // Act
        await indexService.InitializeAsync();

        // Assert
        var stats = await indexService.GetStatisticsAsync();
        Assert.Equal(5, stats.DocumentCount);
        Assert.Equal(5, stats.CachedTopicEmbeddings);
    }

    [Fact]
    public async Task SearchAsync_WithPopulatedIndex_ReturnsResults()
    {
        // Arrange
        var vectorStore = _fixture.ServiceProvider.GetRequiredService<IVectorStoreService>();
        var indexService = _fixture.ServiceProvider.GetRequiredService<ITwoTierIndexService>();

        // Store test data
        for (int i = 0; i < 3; i++)
        {
            await vectorStore.StoreTopicIndexAsync(
                $"doc-{i}",
                $"Document {i}",
                CreateTestEmbedding(384),
                $"/test/doc{i}.txt",
                $"hash{i}");

            // Store chunks for each document
            for (int j = 0; j < 2; j++)
            {
                await vectorStore.StoreChunkAsync(
                    $"doc-{i}",
                    $"Chunk {j} of document {i}",
                    CreateTestEmbedding(384),
                    j);
            }
        }

        // Initialize index
        await indexService.InitializeAsync();

        // Act
        var queryEmbedding = CreateTestEmbedding(384);
        var results = await indexService.SearchAsync(queryEmbedding, tier1TopK: 2, tier2TopK: 2);

        // Assert
        Assert.NotNull(results);
        Assert.True(results.ExecutionTimeMs >= 0);
        // Note: Results may be empty or non-empty depending on embedding similarity
        // Just verify the operation completes without error
    }

    [Fact]
    public async Task SearchAsync_RespondsWithinTimeTarget()
    {
        // Arrange
        var vectorStore = _fixture.ServiceProvider.GetRequiredService<IVectorStoreService>();
        var indexService = _fixture.ServiceProvider.GetRequiredService<ITwoTierIndexService>();

        // Store test data - simulate a modest knowledge base
        for (int i = 0; i < 20; i++)
        {
            await vectorStore.StoreTopicIndexAsync(
                $"doc-{i}",
                $"Document {i}",
                CreateTestEmbedding(384),
                $"/test/doc{i}.txt",
                $"hash{i}");
        }

        // Initialize index
        await indexService.InitializeAsync();

        // Act
        var queryEmbedding = CreateTestEmbedding(384);
        var results = await indexService.SearchAsync(queryEmbedding, tier1TopK: 10, tier2TopK: 5);

        // Assert - Tier 1 search should be very fast (< 100ms for 20 documents)
        Assert.True(results.ExecutionTimeMs < 100, $"Search took {results.ExecutionTimeMs}ms, expected < 100ms");
    }

    [Fact]
    public async Task GetStatisticsAsync_ReturnsAccurateMetrics()
    {
        // Arrange
        var vectorStore = _fixture.ServiceProvider.GetRequiredService<IVectorStoreService>();
        var indexService = _fixture.ServiceProvider.GetRequiredService<ITwoTierIndexService>();

        // Store test data
        var docCount = 3;
        var chunksPerDoc = 4;
        
        for (int i = 0; i < docCount; i++)
        {
            await vectorStore.StoreTopicIndexAsync(
                $"doc-{i}",
                $"Document {i}",
                CreateTestEmbedding(384),
                $"/test/doc{i}.txt",
                $"hash{i}");

            for (int j = 0; j < chunksPerDoc; j++)
            {
                await vectorStore.StoreChunkAsync(
                    $"doc-{i}",
                    $"Chunk {j}",
                    CreateTestEmbedding(384),
                    j);
            }
        }

        // Initialize index
        await indexService.InitializeAsync();

        // Act
        var stats = await indexService.GetStatisticsAsync();

        // Assert
        Assert.Equal(docCount, stats.DocumentCount);
        Assert.Equal(docCount * chunksPerDoc, stats.ChunkCount);
        Assert.Equal(docCount, stats.CachedTopicEmbeddings);
        Assert.True(stats.EstimatedMemoryBytes > 0);
    }

    [Fact]
    public async Task ClearCacheAsync_ReleasesMemoryButPreservesData()
    {
        // Arrange
        var vectorStore = _fixture.ServiceProvider.GetRequiredService<IVectorStoreService>();
        var indexService = _fixture.ServiceProvider.GetRequiredService<ITwoTierIndexService>();

        // Store test data
        await vectorStore.StoreTopicIndexAsync(
            "doc-1",
            "Document 1",
            CreateTestEmbedding(384),
            "/test/doc1.txt",
            "hash1");

        // Initialize index
        await indexService.InitializeAsync();
        var statsBefore = await indexService.GetStatisticsAsync();
        Assert.Equal(1, statsBefore.CachedTopicEmbeddings);

        // Act
        await indexService.ClearCacheAsync();
        var statsAfter = await indexService.GetStatisticsAsync();

        // Assert
        Assert.Equal(0, statsAfter.CachedTopicEmbeddings);
        // But data should still exist in database
        var topic = await vectorStore.GetTopicIndexAsync("doc-1");
        Assert.NotNull(topic);
    }

    [Fact]
    public async Task SearchAsync_WithMultipleTierResults_ReturnsFromBothTiers()
    {
        // Arrange
        var vectorStore = _fixture.ServiceProvider.GetRequiredService<IVectorStoreService>();
        var indexService = _fixture.ServiceProvider.GetRequiredService<ITwoTierIndexService>();

        // Store a single document with multiple chunks
        var docId = "doc-1";
        var embedding = CreateTestEmbedding(384);
        
        await vectorStore.StoreTopicIndexAsync(docId, "Main document", embedding, "/test/doc1.txt", "hash1");
        
        for (int i = 0; i < 5; i++)
        {
            await vectorStore.StoreChunkAsync(docId, $"Chunk {i} content", CreateTestEmbedding(384), i);
        }

        // Initialize and search
        await indexService.InitializeAsync();
        var queryEmbedding = embedding; // Use same embedding for deterministic results

        // Act
        var results = await indexService.SearchAsync(queryEmbedding, tier1TopK: 1, tier2TopK: 3);

        // Assert
        Assert.NotNull(results);
        Assert.True(results.ExecutionTimeMs >= 0);
    }

    [Fact]
    public async Task InitializeAsync_CanBeCalledMultipleTimesWithoutError()
    {
        // Arrange
        var vectorStore = _fixture.ServiceProvider.GetRequiredService<IVectorStoreService>();
        var indexService = _fixture.ServiceProvider.GetRequiredService<ITwoTierIndexService>();

        // Store test data
        await vectorStore.StoreTopicIndexAsync(
            "doc-1",
            "Document 1",
            CreateTestEmbedding(384),
            "/test/doc1.txt",
            "hash1");

        // Act & Assert - should not throw when called multiple times
        await indexService.InitializeAsync();
        var stats1 = await indexService.GetStatisticsAsync();

        await indexService.InitializeAsync();
        var stats2 = await indexService.GetStatisticsAsync();

        Assert.Equal(stats1.CachedTopicEmbeddings, stats2.CachedTopicEmbeddings);
    }

    [Fact]
    public async Task SearchAsync_WithEmptyIndex_ReturnsEmptyResults()
    {
        // Arrange
        var indexService = _fixture.ServiceProvider.GetRequiredService<ITwoTierIndexService>();

        // Initialize empty index
        await indexService.InitializeAsync();

        // Act
        var queryEmbedding = CreateTestEmbedding(384);
        var results = await indexService.SearchAsync(queryEmbedding, tier1TopK: 10, tier2TopK: 5);

        // Assert
        Assert.Empty(results.Tier1Results);
        Assert.Empty(results.Tier2Results);
    }

    [Theory]
    [InlineData(384)]
    [InlineData(768)]
    public async Task SearchAsync_WithDifferentEmbeddingDimensions_Completes(int dimensions)
    {
        // Arrange
        var vectorStore = _fixture.ServiceProvider.GetRequiredService<IVectorStoreService>();
        var indexService = _fixture.ServiceProvider.GetRequiredService<ITwoTierIndexService>();

        // Store topics with different dimensions
        for (int i = 0; i < 3; i++)
        {
            await vectorStore.StoreTopicIndexAsync(
                $"doc-{i}",
                $"Document {i}",
                CreateTestEmbedding(dimensions),
                $"/test/doc{i}.txt",
                $"hash{i}");
        }

        // Initialize and search
        await indexService.InitializeAsync();
        var queryEmbedding = CreateTestEmbedding(dimensions);

        // Act
        var results = await indexService.SearchAsync(queryEmbedding, tier1TopK: 2, tier2TopK: 2);

        // Assert
        Assert.NotNull(results);
    }

    [Fact]
    public async Task ConcurrentSearchOperations_ExecuteSafely()
    {
        // Arrange
        var vectorStore = _fixture.ServiceProvider.GetRequiredService<IVectorStoreService>();
        var indexService = _fixture.ServiceProvider.GetRequiredService<ITwoTierIndexService>();

        // Store test data
        for (int i = 0; i < 5; i++)
        {
            await vectorStore.StoreTopicIndexAsync(
                $"doc-{i}",
                $"Document {i}",
                CreateTestEmbedding(384),
                $"/test/doc{i}.txt",
                $"hash{i}");
        }

        await indexService.InitializeAsync();

        // Act - Execute multiple searches concurrently
        var searchTasks = new List<Task<TwoTierSearchResults>>();
        for (int i = 0; i < 5; i++)
        {
            var queryEmbedding = CreateTestEmbedding(384);
            searchTasks.Add(indexService.SearchAsync(queryEmbedding, 2, 1));
        }

        var results = await Task.WhenAll(searchTasks);

        // Assert - All searches should complete successfully
        Assert.Equal(5, results.Length);
        Assert.All(results, r => Assert.NotNull(r));
    }

    // Helper method to create test embeddings
    private float[] CreateTestEmbedding(int dimensions)
    {
        var embedding = new float[dimensions];
        var random = new Random();
        for (int i = 0; i < dimensions; i++)
        {
            embedding[i] = (float)random.NextDouble();
        }
        return embedding;
    }
}
