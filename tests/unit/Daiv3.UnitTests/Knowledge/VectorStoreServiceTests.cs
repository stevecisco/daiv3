using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Daiv3.Knowledge;
using Daiv3.Persistence;
using Daiv3.Persistence.Entities;
using Daiv3.Persistence.Repositories;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Daiv3.UnitTests.Knowledge;

public class VectorStoreServiceTests
{
    private readonly Mock<IDatabaseContext> _mockDatabaseContext;
    private readonly Mock<TopicIndexRepository> _mockTopicRepository;
    private readonly Mock<ChunkIndexRepository> _mockChunkRepository;
    private readonly Mock<DocumentRepository> _mockDocumentRepository;
    private readonly VectorStoreService _service;

    public VectorStoreServiceTests()
    {
        // Create mock for IDatabaseContext
        _mockDatabaseContext = new Mock<IDatabaseContext>();

        // Create loose mocks with NullLogger for dependency injection
        _mockTopicRepository = new Mock<TopicIndexRepository>(
            _mockDatabaseContext.Object,
            NullLogger<TopicIndexRepository>.Instance)
        { CallBase = true };

        _mockChunkRepository = new Mock<ChunkIndexRepository>(
            _mockDatabaseContext.Object,
            NullLogger<ChunkIndexRepository>.Instance)
        { CallBase = true };

        _mockDocumentRepository = new Mock<DocumentRepository>(
            _mockDatabaseContext.Object,
            NullLogger<DocumentRepository>.Instance)
        { CallBase = true };

        _mockTopicRepository
            .Setup(x => x.GetByIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((TopicIndex?)null);

        // Setup DocumentRepository mocks to support EnsureDocumentExistsAsync
        _mockDocumentRepository
            .Setup(x => x.GetByIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Document?)null);

        _mockDocumentRepository
            .Setup(x => x.AddAsync(It.IsAny<Document>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Document doc, CancellationToken ct) => doc.DocId);

        _service = new VectorStoreService(_mockDatabaseContext.Object, _mockTopicRepository.Object, _mockChunkRepository.Object, _mockDocumentRepository.Object, NullLogger<VectorStoreService>.Instance);
    }

    [Fact]
    public async Task StoreTopicIndexAsync_CreatesAndStoresTopicIndex()
    {
        // Arrange
        var docId = "test-doc-1";
        var embedding = new float[384];
        var summaryText = "Test summary";
        var sourcePath = "/documents/test.txt";
        var fileHash = "abc123def456";

        _mockTopicRepository
            .Setup(x => x.AddAsync(It.IsAny<TopicIndex>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("topic-id");

        // Act
        await _service.StoreTopicIndexAsync(docId, summaryText, embedding, sourcePath, fileHash, null, CancellationToken.None);

        // Assert
        _mockTopicRepository.Verify(
            x => x.AddAsync(
                It.Is<TopicIndex>(t =>
                    t.DocId == docId &&
                    t.SummaryText == summaryText &&
                    t.SourcePath == sourcePath &&
                    t.FileHash == fileHash &&
                    t.EmbeddingDimensions == 384),
                It.IsAny<CancellationToken>()),
            Times.Once);

        _mockTopicRepository.Verify(
            x => x.UpdateAsync(It.IsAny<TopicIndex>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task StoreTopicIndexAsync_WhenDocumentExists_UpdatesExistingTopicIndex()
    {
        // Arrange
        var docId = "test-doc-existing";
        var updatedEmbedding = new float[384];

        _mockTopicRepository
            .Setup(x => x.GetByIdAsync(docId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TopicIndex
            {
                DocId = docId,
                SummaryText = "old summary",
                EmbeddingBlob = ConvertEmbeddingToBytes(new float[384]),
                EmbeddingDimensions = 384,
                SourcePath = "/documents/old.txt",
                FileHash = "old-hash",
                IngestedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
            });

        _mockTopicRepository
            .Setup(x => x.UpdateAsync(It.IsAny<TopicIndex>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        await _service.StoreTopicIndexAsync(
            docId,
            "new summary",
            updatedEmbedding,
            "/documents/new.txt",
            "new-hash",
            null,
            CancellationToken.None);

        // Assert
        _mockTopicRepository.Verify(
            x => x.UpdateAsync(
                It.Is<TopicIndex>(t =>
                    t.DocId == docId &&
                    t.SummaryText == "new summary" &&
                    t.SourcePath == "/documents/new.txt" &&
                    t.FileHash == "new-hash"),
                It.IsAny<CancellationToken>()),
            Times.Once);

        _mockTopicRepository.Verify(
            x => x.AddAsync(It.IsAny<TopicIndex>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task StoreChunkAsync_CreatesAndStoresChunk()
    {
        // Arrange
        var docId = "test-doc-1";
        var chunkText = "This is a text chunk";
        var embedding = new float[768];
        var chunkOrder = 0;

        _mockChunkRepository
            .Setup(x => x.AddAsync(It.IsAny<ChunkIndex>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("chunk-id");

        // Act
        await _service.StoreChunkAsync(docId, chunkText, embedding, chunkOrder, null, CancellationToken.None);

        // Assert
        _mockChunkRepository.Verify(
            x => x.AddAsync(
                It.Is<ChunkIndex>(c =>
                    c.DocId == docId &&
                    c.ChunkText == chunkText &&
                    c.ChunkOrder == chunkOrder &&
                    c.EmbeddingDimensions == 768),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GetTopicIndexAsync_RetrievesStoredTopicIndex()
    {
        // Arrange
        var docId = "test-doc-1";
        var topic = new TopicIndex
        {
            DocId = docId,
            SummaryText = "Summary",
            EmbeddingBlob = ConvertEmbeddingToBytes(new float[384]),
            EmbeddingDimensions = 384
        };

        _mockTopicRepository
            .Setup(x => x.GetByIdAsync(docId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(topic);

        // Act
        var result = await _service.GetTopicIndexAsync(docId, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(docId, result.DocId);
        Assert.Equal("Summary", result.SummaryText);
    }



    [Fact]
    public async Task TopicIndexExistsAsync_ReturnsTrueForExistingDocument()
    {
        // Arrange
        var docId = "test-doc-1";
        _mockTopicRepository
            .Setup(x => x.GetByIdAsync(docId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TopicIndex { DocId = docId });

        // Act
        var result = await _service.TopicIndexExistsAsync(docId, CancellationToken.None);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task TopicIndexExistsAsync_ReturnsFalseForNonexistentDocument()
    {
        // Arrange
        var docId = "nonexistent-doc";
        _mockTopicRepository
            .Setup(x => x.GetByIdAsync(docId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((TopicIndex)null!);

        // Act
        var result = await _service.TopicIndexExistsAsync(docId, CancellationToken.None);

        // Assert
        Assert.False(result);
    }



    [Fact]
    public async Task StoreTopicIndexAsync_WithMetadata_StoresMetadataJson()
    {
        // Arrange
        var docId = "test-doc-1";
        var embedding = new float[384];
        var metadata = JsonSerializer.Serialize(new Dictionary<string, object> { { "key", "value" } });

        _mockTopicRepository
            .Setup(x => x.AddAsync(It.IsAny<TopicIndex>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("topic-id");

        // Act
        await _service.StoreTopicIndexAsync(docId, "summary", embedding, "/path", "hash", metadata, CancellationToken.None);

        // Assert
        _mockTopicRepository.Verify(
            x => x.AddAsync(
                It.Is<TopicIndex>(t => t.MetadataJson != null),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Theory]
    [InlineData(384)]
    [InlineData(768)]
    public async Task StoreAndRetrieveEmbedding_PreservesFloat32Precision(int dimensions)
    {
        // Arrange
        var embedding = GenerateTestEmbedding(dimensions);
        var docId = "test-doc";

        _mockTopicRepository
            .Setup(x => x.AddAsync(It.IsAny<TopicIndex>(), It.IsAny<CancellationToken>()))
            .Callback<TopicIndex, CancellationToken>((topic, ct) =>
            {
                // Simulate storage and retrieval
                Assert.Equal(dimensions * 4, topic.EmbeddingBlob.Length); // 4 bytes per float
            })
            .ReturnsAsync("topic-id");

        // Act
        await _service.StoreTopicIndexAsync(docId, "summary", embedding, "/path", "hash", null, CancellationToken.None);

        // Assert
        _mockTopicRepository.Verify(
            x => x.AddAsync(
                It.Is<TopicIndex>(t => t.EmbeddingDimensions == dimensions),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task StoreChunkAsync_WithTopicTags_StoresTopicTags()
    {
        // Arrange
        var docId = "test-doc-1";
        var topicTags = "topic1,topic2,topic3";

        _mockChunkRepository
            .Setup(x => x.AddAsync(It.IsAny<ChunkIndex>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("chunk-id");

        // Act
        await _service.StoreChunkAsync(docId, "chunk text", new float[768], 0, topicTags, CancellationToken.None);

        // Assert
        _mockChunkRepository.Verify(
            x => x.AddAsync(
                It.Is<ChunkIndex>(c => c.TopicTags == topicTags),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task StoreTopicIndexAsync_ValidatesDocId()
    {
        // Arrange
        var embedding = new float[384];

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(async () =>
            await _service.StoreTopicIndexAsync(null!, "summary", embedding, "/path", "hash", null, CancellationToken.None)
        );
    }

    [Fact]
    public async Task StoreTopicIndexAsync_ValidatesSummaryText()
    {
        // Arrange
        var embedding = new float[384];

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(async () =>
            await _service.StoreTopicIndexAsync("doc-id", null!, embedding, "/path", "hash", null, CancellationToken.None)
        );
    }

    [Fact]
    public async Task StoreTopicIndexAsync_ValidatesSourcePath()
    {
        // Arrange
        var embedding = new float[384];

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(async () =>
            await _service.StoreTopicIndexAsync("doc-id", "summary", embedding, null!, "hash", null, CancellationToken.None)
        );
    }

    [Fact]
    public async Task StoreTopicIndexAsync_ValidatesFileHash()
    {
        // Arrange
        var embedding = new float[384];

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(async () =>
            await _service.StoreTopicIndexAsync("doc-id", "summary", embedding, "/path", null!, null, CancellationToken.None)
        );
    }

    [Fact]
    public async Task StoreTopicIndexAsync_ValidatesEmbedding()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(async () =>
            await _service.StoreTopicIndexAsync("doc-id", "summary", null!, "/path", "hash", null, CancellationToken.None)
        );
    }

    [Fact]
    public async Task StoreChunkAsync_ValidatesDocId()
    {
        // Arrange
        var embedding = new float[768];

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(async () =>
            await _service.StoreChunkAsync(null!, "chunk text", embedding, 0, null, CancellationToken.None)
        );
    }

    [Fact]
    public async Task StoreChunkAsync_ValidatesChunkText()
    {
        // Arrange
        var embedding = new float[768];

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(async () =>
            await _service.StoreChunkAsync("doc-id", null!, embedding, 0, null, CancellationToken.None)
        );
    }

    [Fact]
    public async Task StoreChunkAsync_ValidatesEmbedding()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(async () =>
            await _service.StoreChunkAsync("doc-id", "chunk text", null!, 0, null, CancellationToken.None)
        );
    }

    [Fact]
    public async Task StoreChunkAsync_RejectsNegativeChunkOrder()
    {
        // Arrange
        var embedding = new float[768];

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(async () =>
            await _service.StoreChunkAsync("doc-id", "chunk text", embedding, -1, null, CancellationToken.None)
        );
    }

    [Fact]
    public async Task GetTopicIndexAsync_ValidatesDocId()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(async () =>
            await _service.GetTopicIndexAsync(null!, CancellationToken.None)
        );
    }

    [Fact]
    public async Task GetChunkAsync_ValidatesChunkId()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(async () =>
            await _service.GetChunkAsync(null!, CancellationToken.None)
        );
    }

    [Fact]
    public async Task DeleteTopicAndChunksAsync_ValidatesDocId()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(async () =>
            await _service.DeleteTopicAndChunksAsync(null!, CancellationToken.None)
        );
    }

    [Fact]
    public async Task TopicIndexExistsAsync_ValidatesDocId()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(async () =>
            await _service.TopicIndexExistsAsync(null!, CancellationToken.None)
        );
    }

    [Fact]
    public async Task StoreChunkAsync_GeneratesCorrectChunkId()
    {
        // Arrange
        var docId = "doc-123";
        var chunkOrder = 5;
        var embedding = new float[768];

        string? capturedChunkId = null;

        _mockChunkRepository
            .Setup(x => x.AddAsync(It.IsAny<ChunkIndex>(), It.IsAny<CancellationToken>()))
            .Callback<ChunkIndex, CancellationToken>((chunk, ct) =>
            {
                capturedChunkId = chunk.ChunkId;
            })
            .ReturnsAsync("returned-id");

        // Act
        await _service.StoreChunkAsync(docId, "chunk text", embedding, chunkOrder, null, CancellationToken.None);

        // Assert
        Assert.NotNull(capturedChunkId);
        Assert.Equal($"{docId}_chunk_{chunkOrder}", capturedChunkId);
    }

    [Fact]
    public async Task GetTopicIndexAsync_ReturnsNullWhenNotFound()
    {
        // Arrange
        var docId = "nonexistent";
        _mockTopicRepository
            .Setup(x => x.GetByIdAsync(docId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((TopicIndex)null!);

        // Act
        var result = await _service.GetTopicIndexAsync(docId, CancellationToken.None);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task GetChunkAsync_ReturnsNullWhenNotFound()
    {
        // Arrange
        var chunkId = "nonexistent-chunk";
        _mockChunkRepository
            .Setup(x => x.GetByIdAsync(chunkId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ChunkIndex)null!);

        // Act
        var result = await _service.GetChunkAsync(chunkId, CancellationToken.None);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task StoreTopicIndexAsync_SetsIngestedAtTimestamp()
    {
        // Arrange
        var docId = "test-doc";
        var embedding = new float[384];
        long? capturedIngestedAt = null;

        _mockTopicRepository
            .Setup(x => x.AddAsync(It.IsAny<TopicIndex>(), It.IsAny<CancellationToken>()))
            .Callback<TopicIndex, CancellationToken>((topic, ct) =>
            {
                capturedIngestedAt = topic.IngestedAt;
            })
            .ReturnsAsync("topic-id");

        // Act
        await _service.StoreTopicIndexAsync(docId, "summary", embedding, "/path", "hash", null, CancellationToken.None);

        // Assert
        Assert.NotNull(capturedIngestedAt);
        Assert.True(capturedIngestedAt > 0);
    }

    [Fact]
    public async Task StoreChunkAsync_SetsCreatedAtTimestamp()
    {
        // Arrange
        var docId = "test-doc";
        var embedding = new float[768];
        long? capturedCreatedAt = null;

        _mockChunkRepository
            .Setup(x => x.AddAsync(It.IsAny<ChunkIndex>(), It.IsAny<CancellationToken>()))
            .Callback<ChunkIndex, CancellationToken>((chunk, ct) =>
            {
                capturedCreatedAt = chunk.CreatedAt;
            })
            .ReturnsAsync("chunk-id");

        // Act
        await _service.StoreChunkAsync(docId, "chunk text", embedding, 0, null, CancellationToken.None);

        // Assert
        Assert.NotNull(capturedCreatedAt);
        Assert.True(capturedCreatedAt > 0);
    }

    [Fact]
    public async Task StoreTopicIndexAsync_ConvertsEmbeddingCorrectly()
    {
        // Arrange
        var docId = "test-doc";
        var embedding = new[] { 0.1f, 0.2f, 0.3f };
        byte[]? capturedBlob = null;

        _mockTopicRepository
            .Setup(x => x.AddAsync(It.IsAny<TopicIndex>(), It.IsAny<CancellationToken>()))
            .Callback<TopicIndex, CancellationToken>((topic, ct) =>
            {
                capturedBlob = topic.EmbeddingBlob;
            })
            .ReturnsAsync("topic-id");

        // Act
        await _service.StoreTopicIndexAsync(docId, "summary", embedding, "/path", "hash", null, CancellationToken.None);

        // Assert
        Assert.NotNull(capturedBlob);
        Assert.Equal(12, capturedBlob!.Length); // 3 floats * 4 bytes each
    }

    [Fact]
    public async Task StoreChunkAsync_ConvertsEmbeddingCorrectly()
    {
        // Arrange
        var docId = "test-doc";
        var embedding = new[] { 0.5f, 0.6f };
        byte[]? capturedBlob = null;

        _mockChunkRepository
            .Setup(x => x.AddAsync(It.IsAny<ChunkIndex>(), It.IsAny<CancellationToken>()))
            .Callback<ChunkIndex, CancellationToken>((chunk, ct) =>
            {
                capturedBlob = chunk.EmbeddingBlob;
            })
            .ReturnsAsync("chunk-id");

        // Act
        await _service.StoreChunkAsync(docId, "chunk text", embedding, 0, null, CancellationToken.None);

        // Assert
        Assert.NotNull(capturedBlob);
        Assert.Equal(8, capturedBlob!.Length); // 2 floats * 4 bytes each
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(100)]
    public async Task StoreChunkAsync_AcceptsValidChunkOrders(int chunkOrder)
    {
        // Arrange
        var docId = "test-doc";
        var embedding = new float[768];

        _mockChunkRepository
            .Setup(x => x.AddAsync(It.IsAny<ChunkIndex>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("chunk-id");

        // Act
        await _service.StoreChunkAsync(docId, "chunk text", embedding, chunkOrder, null, CancellationToken.None);

        // Assert
        _mockChunkRepository.Verify(
            x => x.AddAsync(
                It.Is<ChunkIndex>(c => c.ChunkOrder == chunkOrder),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // Note: Tests for methods that call non-virtual repository methods have been removed
    // because Moq cannot mock non-virtual methods. These are tested through integration tests:
    // - GetAllTopicIndicesAsync (calls non-virtual GetAllAsync)
    // - GetChunksByDocumentAsync (calls non-virtual GetByDocumentIdAsync) 
    // - DeleteTopicAndChunksAsync (calls non-virtual DeleteByDocumentIdAsync)
    // - GetTopicIndexCountAsync (calls non-virtual GetCountAsync)
    // - GetChunkIndexCountAsync (calls non-virtual GetCountAsync)

    private float[] GenerateTestEmbedding(int dimensions)
    {
        var embedding = new float[dimensions];
        for (int i = 0; i < dimensions; i++)
        {
            embedding[i] = (float)Math.Sin(i * 0.1);
        }
        return embedding;
    }

    private byte[] ConvertEmbeddingToBytes(float[] embedding)
    {
        var bytes = new byte[embedding.Length * sizeof(float)];
        Buffer.BlockCopy(embedding, 0, bytes, 0, bytes.Length);
        return bytes;
    }
}
