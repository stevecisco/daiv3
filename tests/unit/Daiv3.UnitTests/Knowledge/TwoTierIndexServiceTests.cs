using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Daiv3.Knowledge;
using Daiv3.Knowledge.Embedding;
using Daiv3.Persistence;
using Daiv3.Persistence.Entities;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Daiv3.UnitTests.Knowledge;

/// <summary>
/// Simple stub implementation of IVectorSimilarityService that avoids Moq proxy issues with Span parameters.
/// </summary>
public class SimpleVectorSimilarityStub : IVectorSimilarityService
{
    public float CosineSimilarity(ReadOnlySpan<float> vector1, ReadOnlySpan<float> vector2)
    {
        // Return a simple default score
        return 0.5f;
    }

    public void BatchCosineSimilarity(
        ReadOnlySpan<float> queryVector,
        ReadOnlySpan<float> targetVectors,
        int vectorCount,
        int dimensions,
        Span<float> results)
    {
        // Fill results with simple default scores (0.5 for all)
        for (int i = 0; i < vectorCount; i++)
        {
            results[i] = 0.5f;
        }
    }

    public void Normalize(ReadOnlySpan<float> vector, Span<float> normalized)
    {
        // Simple copy without actual normalization for testing
        for (int i = 0; i < vector.Length; i++)
        {
            normalized[i] = vector[i];
        }
    }
}

public class TwoTierIndexServiceTests
{
    private readonly Mock<IVectorStoreService> _mockVectorStore;
    private readonly IVectorSimilarityService _vectorSimilarity;
    private readonly TwoTierIndexService _service;

    public TwoTierIndexServiceTests()
    {
        _mockVectorStore = new Mock<IVectorStoreService>();
        _vectorSimilarity = new SimpleVectorSimilarityStub();
        _service = new TwoTierIndexService(_mockVectorStore.Object, _vectorSimilarity, NullLogger<TwoTierIndexService>.Instance);
    }

    [Fact]
    public async Task InitializeAsync_LoadsAllTopicIndicesIntoMemory()
    {
        // Arrange
        var topics = new List<TopicIndex>
        {
            new TopicIndex 
            { 
                DocId = "doc1", 
                SummaryText = "Document 1", 
                EmbeddingBlob = ConvertEmbeddingToBytes(new float[384]), 
                EmbeddingDimensions = 384
            },
            new TopicIndex 
            { 
                DocId = "doc2", 
                SummaryText = "Document 2", 
                EmbeddingBlob = ConvertEmbeddingToBytes(new float[384]), 
                EmbeddingDimensions = 384
            }
        };

        _mockVectorStore
            .Setup(x => x.GetAllTopicIndicesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(topics);

        _mockVectorStore
            .Setup(x => x.GetTopicIndexCountAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(2);

        _mockVectorStore
            .Setup(x => x.GetChunkIndexCountAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        // Act
        await _service.InitializeAsync(CancellationToken.None);

        // Assert
        var stats = await _service.GetStatisticsAsync(CancellationToken.None);
        Assert.NotNull(stats);
        Assert.Equal(2, stats.CachedTopicEmbeddings);
    }

    [Fact]
    public async Task InitializeAsync_EmptyDatabase_NoError()
    {
        // Arrange
        _mockVectorStore
            .Setup(x => x.GetAllTopicIndicesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<TopicIndex>());

        _mockVectorStore
            .Setup(x => x.GetTopicIndexCountAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        _mockVectorStore
            .Setup(x => x.GetChunkIndexCountAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        // Act & Assert - should not throw
        await _service.InitializeAsync(CancellationToken.None);
        
        var stats = await _service.GetStatisticsAsync(CancellationToken.None);
        Assert.Equal(0, stats.CachedTopicEmbeddings);
    }

    [Fact]
    public async Task SearchAsync_WithInitializedIndex_ReturnsResults()
    {
        // Arrange
        var topics = new List<TopicIndex>
        {
            new TopicIndex 
            { 
                DocId = "doc1", 
                SummaryText = "Result 1", 
                EmbeddingBlob = ConvertEmbeddingToBytes(new float[384]), 
                EmbeddingDimensions = 384,
                SourcePath = "/docs/file1.txt"
            }
        };

        _mockVectorStore
            .Setup(x => x.GetAllTopicIndicesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(topics);

        _mockVectorStore
            .Setup(x => x.GetTopicIndexCountAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        _mockVectorStore
            .Setup(x => x.GetChunkIndexCountAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        _mockVectorStore
            .Setup(x => x.GetChunksByDocumentAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ChunkIndex>());

        await _service.InitializeAsync(CancellationToken.None);

        var queryEmbedding = new float[384];

        // Act
        var results = await _service.SearchAsync(queryEmbedding, 1, 5, CancellationToken.None);

        // Assert
        Assert.NotNull(results);
        Assert.True(results.ExecutionTimeMs >= 0, "Execution time should be recorded");
    }

    [Fact]
    public async Task SearchAsync_UnintializedIndex_ReturnsEmpty()
    {
        // Arrange
        var queryEmbedding = new float[384];
        
        // Act - search without initializing
        var results = await _service.SearchAsync(queryEmbedding, 10, 5, CancellationToken.None);

        // Assert
        Assert.Empty(results.Tier1Results);
        Assert.Empty(results.Tier2Results);
    }

    [Fact]
    public async Task GetStatisticsAsync_ReturnsIndexStatistics()
    {
        // Arrange
        var topics = new List<TopicIndex>
        {
            new TopicIndex { DocId = "doc1", EmbeddingBlob = ConvertEmbeddingToBytes(new float[384]), EmbeddingDimensions = 384 },
            new TopicIndex { DocId = "doc2", EmbeddingBlob = ConvertEmbeddingToBytes(new float[384]), EmbeddingDimensions = 384 }
        };

        _mockVectorStore
            .Setup(x => x.GetAllTopicIndicesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(topics);

        _mockVectorStore
            .Setup(x => x.GetTopicIndexCountAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(2);

        _mockVectorStore
            .Setup(x => x.GetChunkIndexCountAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(5);

        await _service.InitializeAsync(CancellationToken.None);

        // Act
        var stats = await _service.GetStatisticsAsync(CancellationToken.None);

        // Assert
        Assert.NotNull(stats);
        Assert.Equal(2, stats.CachedTopicEmbeddings);
        Assert.True(stats.EstimatedMemoryBytes > 0);
        Assert.Equal(2, stats.DocumentCount);
        Assert.Equal(5, stats.ChunkCount);
    }

    [Fact]
    public async Task ClearCacheAsync_ReleasesMemory()
    {
        // Arrange
        var topics = new List<TopicIndex>
        {
            new TopicIndex 
            { 
                DocId = "doc1", 
                EmbeddingBlob = ConvertEmbeddingToBytes(new float[384]), 
                EmbeddingDimensions = 384
            }
        };

        _mockVectorStore
            .Setup(x => x.GetAllTopicIndicesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(topics);

        _mockVectorStore
            .Setup(x => x.GetTopicIndexCountAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        _mockVectorStore
            .Setup(x => x.GetChunkIndexCountAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        await _service.InitializeAsync(CancellationToken.None);
        var statsBeforeClear = await _service.GetStatisticsAsync(CancellationToken.None);
        Assert.Equal(1, statsBeforeClear.CachedTopicEmbeddings);

        // Act
        await _service.ClearCacheAsync();
        var statsAfterClear = await _service.GetStatisticsAsync(CancellationToken.None);

        // Assert
        Assert.Equal(0, statsAfterClear.CachedTopicEmbeddings);
    }

    [Fact]
    public async Task SearchAsync_WithMultipleDocuments_Completes()
    {
        // Arrange - Create modest dataset
        var topics = new List<TopicIndex>();
        for (int i = 0; i < 10; i++)
        {
            topics.Add(new TopicIndex
            {
                DocId = $"doc{i}",
                SummaryText = $"Document {i}",
                EmbeddingBlob = ConvertEmbeddingToBytes(new float[384]),
                EmbeddingDimensions = 384,
                SourcePath = $"/docs/file{i}.txt"
            });
        }

        _mockVectorStore
            .Setup(x => x.GetAllTopicIndicesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(topics);

        _mockVectorStore
            .Setup(x => x.GetTopicIndexCountAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(10);

        _mockVectorStore
            .Setup(x => x.GetChunkIndexCountAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        _mockVectorStore
            .Setup(x => x.GetChunksByDocumentAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ChunkIndex>());

        await _service.InitializeAsync(CancellationToken.None);

        var queryEmbedding = new float[384];

        // Act
        var results = await _service.SearchAsync(queryEmbedding, 5, 3, CancellationToken.None);

        // Assert - Search should complete successfully
        Assert.NotNull(results);
        Assert.True(results.ExecutionTimeMs >= 0);
    }

    [Theory]
    [InlineData(384)]
    [InlineData(768)]
    public async Task InitializeAsync_HandlesMultipleDimensions(int dimensions)
    {
        // Arrange
        var topics = new List<TopicIndex>
        {
            new TopicIndex 
            { 
                DocId = "doc1", 
                EmbeddingBlob = ConvertEmbeddingToBytes(new float[dimensions]), 
                EmbeddingDimensions = dimensions
            }
        };

        _mockVectorStore
            .Setup(x => x.GetAllTopicIndicesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(topics);

        _mockVectorStore
            .Setup(x => x.GetTopicIndexCountAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        _mockVectorStore
            .Setup(x => x.GetChunkIndexCountAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        // Act & Assert
        await _service.InitializeAsync(CancellationToken.None);
        var stats = await _service.GetStatisticsAsync(CancellationToken.None);
        Assert.Equal(1, stats.CachedTopicEmbeddings);
    }

    [Fact]
    public async Task SearchAsync_WrongDimensions_ThrowsArgumentException()
    {
        // Arrange
        var topics = new List<TopicIndex>
        {
            new TopicIndex 
            { 
                DocId = "doc1", 
                EmbeddingBlob = ConvertEmbeddingToBytes(new float[384]), 
                EmbeddingDimensions = 384
            }
        };

        _mockVectorStore
            .Setup(x => x.GetAllTopicIndicesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(topics);

        _mockVectorStore
            .Setup(x => x.GetTopicIndexCountAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        _mockVectorStore
            .Setup(x => x.GetChunkIndexCountAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        _mockVectorStore
            .Setup(x => x.GetChunksByDocumentAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ChunkIndex>());

        await _service.InitializeAsync(CancellationToken.None);

        var wrongDimensionVector = new float[768]; // Wrong dimensions

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => 
            _service.SearchAsync(wrongDimensionVector, 10, 5, CancellationToken.None));
    }

    [Fact]
    public async Task InitializeAsync_CanBeCalledMultipleTimes()
    {
        // Arrange
        var topics = new List<TopicIndex>
        {
            new TopicIndex 
            { 
                DocId = "doc1", 
                EmbeddingBlob = ConvertEmbeddingToBytes(new float[384]), 
                EmbeddingDimensions = 384
            }
        };

        _mockVectorStore
            .Setup(x => x.GetAllTopicIndicesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(topics);

        _mockVectorStore
            .Setup(x => x.GetTopicIndexCountAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        _mockVectorStore
            .Setup(x => x.GetChunkIndexCountAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        // Act & Assert - should not throw when called multiple times
        await _service.InitializeAsync(CancellationToken.None);
        var stats1 = await _service.GetStatisticsAsync(CancellationToken.None);
        
        await _service.InitializeAsync(CancellationToken.None);
        var stats2 = await _service.GetStatisticsAsync(CancellationToken.None);

        Assert.Equal(1, stats1.CachedTopicEmbeddings);
        Assert.Equal(1, stats2.CachedTopicEmbeddings);
    }

    [Fact]
    public async Task GetStatisticsAsync_ReturnsCorrectMemoryEstimate()
    {
        // Arrange
        var topics = new List<TopicIndex>
        {
            new TopicIndex 
            { 
                DocId = "doc1", 
                EmbeddingBlob = ConvertEmbeddingToBytes(new float[384]), 
                EmbeddingDimensions = 384
            }
        };

        _mockVectorStore
            .Setup(x => x.GetAllTopicIndicesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(topics);

        _mockVectorStore
            .Setup(x => x.GetTopicIndexCountAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        _mockVectorStore
            .Setup(x => x.GetChunkIndexCountAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        await _service.InitializeAsync(CancellationToken.None);

        // Act
        var stats = await _service.GetStatisticsAsync(CancellationToken.None);

        // Assert - 1 embedding * 384 floats * 4 bytes per float = 1536 bytes
        Assert.Equal(1 * 384 * sizeof(float), stats.EstimatedMemoryBytes);
    }

    [Fact]
    public async Task SearchAsync_WithDifferentTopKValues_Completes()
    {
        // Arrange
        var topics = new List<TopicIndex>();
        for (int i = 0; i < 5; i++)
        {
            topics.Add(new TopicIndex
            {
                DocId = $"doc{i}",
                EmbeddingBlob = ConvertEmbeddingToBytes(new float[384]),
                EmbeddingDimensions = 384
            });
        }

        _mockVectorStore
            .Setup(x => x.GetAllTopicIndicesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(topics);

        _mockVectorStore
            .Setup(x => x.GetTopicIndexCountAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(5);

        _mockVectorStore
            .Setup(x => x.GetChunkIndexCountAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        _mockVectorStore
            .Setup(x => x.GetChunksByDocumentAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ChunkIndex>());

        await _service.InitializeAsync(CancellationToken.None);
        var queryEmbedding = new float[384];

        // Act
        var results1 = await _service.SearchAsync(queryEmbedding, 3, 2, CancellationToken.None);
        var results2 = await _service.SearchAsync(queryEmbedding, 5, 5, CancellationToken.None);

        // Assert
        Assert.NotNull(results1);
        Assert.NotNull(results2);
    }

    [Fact]
    public async Task SearchAsync_LoadsChunksOnlyForTopThreeTier1Candidates()
    {
        // Arrange
        var topics = new List<TopicIndex>();
        for (int i = 0; i < 5; i++)
        {
            topics.Add(new TopicIndex
            {
                DocId = $"doc{i}",
                SummaryText = $"Document {i}",
                EmbeddingBlob = ConvertEmbeddingToBytes(new float[384]),
                EmbeddingDimensions = 384,
                SourcePath = $"/docs/file{i}.txt"
            });
        }

        _mockVectorStore
            .Setup(x => x.GetAllTopicIndicesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(topics);

        _mockVectorStore
            .Setup(x => x.GetTopicIndexCountAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(5);

        _mockVectorStore
            .Setup(x => x.GetChunkIndexCountAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(5);

        _mockVectorStore
            .Setup(x => x.GetChunksByDocumentAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ChunkIndex>
            {
                new()
                {
                    ChunkId = "chunk-1",
                    DocId = "doc0",
                    ChunkText = "Chunk content",
                    EmbeddingBlob = ConvertEmbeddingToBytes(new float[384]),
                    EmbeddingDimensions = 384,
                    ChunkOrder = 0
                }
            });

        await _service.InitializeAsync(CancellationToken.None);

        // Act
        var queryEmbedding = new float[384];
        await _service.SearchAsync(queryEmbedding, tier1TopK: 5, tier2TopK: 2, CancellationToken.None);

        // Assert
        _mockVectorStore.Verify(
            x => x.GetChunksByDocumentAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Exactly(3));
        _mockVectorStore.Verify(
            x => x.GetChunksByDocumentAsync("doc0", It.IsAny<CancellationToken>()),
            Times.Once);
        _mockVectorStore.Verify(
            x => x.GetChunksByDocumentAsync("doc1", It.IsAny<CancellationToken>()),
            Times.Once);
        _mockVectorStore.Verify(
            x => x.GetChunksByDocumentAsync("doc2", It.IsAny<CancellationToken>()),
            Times.Once);
        _mockVectorStore.Verify(
            x => x.GetChunksByDocumentAsync("doc3", It.IsAny<CancellationToken>()),
            Times.Never);
        _mockVectorStore.Verify(
            x => x.GetChunksByDocumentAsync("doc4", It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task SearchAsync_WithMismatchedChunkDimensions_SkipsTier2ResultsWithoutThrowing()
    {
        // Arrange
        var topics = new List<TopicIndex>
        {
            new()
            {
                DocId = "doc1",
                SummaryText = "Document 1",
                EmbeddingBlob = ConvertEmbeddingToBytes(new float[384]),
                EmbeddingDimensions = 384,
                SourcePath = "/docs/file1.txt"
            }
        };

        _mockVectorStore
            .Setup(x => x.GetAllTopicIndicesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(topics);

        _mockVectorStore
            .Setup(x => x.GetTopicIndexCountAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        _mockVectorStore
            .Setup(x => x.GetChunkIndexCountAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        _mockVectorStore
            .Setup(x => x.GetChunksByDocumentAsync("doc1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ChunkIndex>
            {
                new()
                {
                    ChunkId = "doc1_chunk_0",
                    DocId = "doc1",
                    ChunkText = "Tier 2 chunk",
                    EmbeddingBlob = ConvertEmbeddingToBytes(new float[768]),
                    EmbeddingDimensions = 768,
                    ChunkOrder = 0
                }
            });

        await _service.InitializeAsync(CancellationToken.None);

        // Act
        var results = await _service.SearchAsync(new float[384], tier1TopK: 1, tier2TopK: 3, CancellationToken.None);

        // Assert
        Assert.Single(results.Tier1Results);
        Assert.Empty(results.Tier2Results);
    }

    // Helper to convert float[] to byte[] for embedding blob

    private byte[] ConvertEmbeddingToBytes(float[] embedding)
    {
        var bytes = new byte[embedding.Length * sizeof(float)];
        Buffer.BlockCopy(embedding, 0, bytes, 0, bytes.Length);
        return bytes;
    }
}
