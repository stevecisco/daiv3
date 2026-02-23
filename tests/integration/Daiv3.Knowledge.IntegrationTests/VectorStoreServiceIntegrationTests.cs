using Daiv3.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Daiv3.Knowledge.IntegrationTests;

/// <summary>
/// Integration tests for VectorStoreService with a real SQLite database.
/// Tests full CRUD operations and persistence of embeddings.
/// </summary>
[Collection("Knowledge Database Collection")]
public class VectorStoreServiceIntegrationTests
{
    private readonly KnowledgeDatabaseFixture _fixture;

    public VectorStoreServiceIntegrationTests(KnowledgeDatabaseFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task StoreTopicIndex_StoresAndRetrievesFromDatabase()
    {
        // Arrange
        var vectorStore = _fixture.ServiceProvider.GetRequiredService<IVectorStoreService>();
        var docId = "doc-1";
        var summaryText = "Test document summary";
        var embedding = CreateTestEmbedding(384);
        var sourcePath = "/test/document.txt";
        var fileHash = "abc123def456";

        // Act
        var storedId = await vectorStore.StoreTopicIndexAsync(docId, summaryText, embedding, sourcePath, fileHash);
        
        // Assert
        Assert.NotNull(storedId);
        var retrieved = await vectorStore.GetTopicIndexAsync(docId);
        Assert.NotNull(retrieved);
        Assert.Equal(docId, retrieved.DocId);
        Assert.Equal(summaryText, retrieved.SummaryText);
        Assert.Equal(sourcePath, retrieved.SourcePath);
    }

    [Fact]
    public async Task StoreChunk_StoresAndRetrievesFromDatabase()
    {
        // Arrange
        var vectorStore = _fixture.ServiceProvider.GetRequiredService<IVectorStoreService>();
        var docId = "doc-1";
        var chunkText = "This is a chunk of text from the document.";
        var embedding = CreateTestEmbedding(384);
        var chunkOrder = 0;

        // First store a topic index (required for chunks)
        await vectorStore.StoreTopicIndexAsync(docId, "Summary", CreateTestEmbedding(384), "/test/doc.txt", "hash1");

        // Act
        var chunkId = await vectorStore.StoreChunkAsync(docId, chunkText, embedding, chunkOrder);

        // Assert
        Assert.NotNull(chunkId);
        var retrieved = await vectorStore.GetChunkAsync(chunkId);
        Assert.NotNull(retrieved);
        Assert.Equal(chunkText, retrieved.ChunkText);
        Assert.Equal(chunkOrder, retrieved.ChunkOrder);
    }

    [Fact]
    public async Task GetChunksByDocument_RetrievesAllChunksForDocument()
    {
        // Arrange
        var vectorStore = _fixture.ServiceProvider.GetRequiredService<IVectorStoreService>();
        var docId = "doc-1";
        
        // Store topic index
        await vectorStore.StoreTopicIndexAsync(docId, "Summary", CreateTestEmbedding(384), "/test/doc.txt", "hash1");

        // Store multiple chunks
        var chunkIds = new List<string>();
        for (int i = 0; i < 3; i++)
        {
            var chunkId = await vectorStore.StoreChunkAsync(
                docId, 
                $"Chunk {i} content", 
                CreateTestEmbedding(384), 
                i);
            chunkIds.Add(chunkId);
        }

        // Act
        var chunks = await vectorStore.GetChunksByDocumentAsync(docId);

        // Assert
        Assert.NotEmpty(chunks);
        Assert.Equal(3, chunks.Count);
        Assert.All(chunks, chunk => Assert.Equal(docId, chunk.DocId));
    }

    [Fact]
    public async Task DeleteTopicAndChunks_RemovesAllRelatedData()
    {
        // Arrange
        var vectorStore = _fixture.ServiceProvider.GetRequiredService<IVectorStoreService>();
        var docId = "doc-1";
        
        // Store topic and chunks
        await vectorStore.StoreTopicIndexAsync(docId, "Summary", CreateTestEmbedding(384), "/test/doc.txt", "hash1");
        await vectorStore.StoreChunkAsync(docId, "Chunk 1", CreateTestEmbedding(384), 0);
        await vectorStore.StoreChunkAsync(docId, "Chunk 2", CreateTestEmbedding(384), 1);

        // Verify data exists
        var topicBefore = await vectorStore.GetTopicIndexAsync(docId);
        var chunksBefore = await vectorStore.GetChunksByDocumentAsync(docId);
        Assert.NotNull(topicBefore);
        Assert.NotEmpty(chunksBefore);

        // Act
        await vectorStore.DeleteTopicAndChunksAsync(docId);

        // Assert
        var topicAfter = await vectorStore.GetTopicIndexAsync(docId);
        var chunksAfter = await vectorStore.GetChunksByDocumentAsync(docId);
        Assert.Null(topicAfter);
        Assert.Empty(chunksAfter);
    }

    [Fact]
    public async Task TopicIndexExistsAsync_ReturnsTrueWhenExists()
    {
        // Arrange
        var vectorStore = _fixture.ServiceProvider.GetRequiredService<IVectorStoreService>();
        var docId = "doc-1";
        
        // Store topic
        await vectorStore.StoreTopicIndexAsync(docId, "Summary", CreateTestEmbedding(384), "/test/doc.txt", "hash1");

        // Act
        var exists = await vectorStore.TopicIndexExistsAsync(docId);

        // Assert
        Assert.True(exists);
    }

    [Fact]
    public async Task TopicIndexExistsAsync_ReturnsFalseWhenNotExists()
    {
        // Arrange
        var vectorStore = _fixture.ServiceProvider.GetRequiredService<IVectorStoreService>();
        var docId = "nonexistent-doc";

        // Act
        var exists = await vectorStore.TopicIndexExistsAsync(docId);

        // Assert
        Assert.False(exists);
    }

    [Fact]
    public async Task GetTopicIndexCountAsync_ReturnsCorrectCount()
    {
        // Arrange
        var vectorStore = _fixture.ServiceProvider.GetRequiredService<IVectorStoreService>();
        
        // Store multiple topics
        for (int i = 0; i < 5; i++)
        {
            await vectorStore.StoreTopicIndexAsync(
                $"doc-{i}", 
                $"Summary {i}", 
                CreateTestEmbedding(384), 
                $"/test/doc{i}.txt", 
                $"hash{i}");
        }

        // Act
        var count = await vectorStore.GetTopicIndexCountAsync();

        // Assert
        Assert.Equal(5, count);
    }

    [Fact]
    public async Task GetChunkIndexCountAsync_ReturnsCorrectCount()
    {
        // Arrange
        var vectorStore = _fixture.ServiceProvider.GetRequiredService<IVectorStoreService>();
        var docId = "doc-1";
        
        // Store topic
        await vectorStore.StoreTopicIndexAsync(docId, "Summary", CreateTestEmbedding(384), "/test/doc.txt", "hash1");

        // Store chunks
        for (int i = 0; i < 3; i++)
        {
            await vectorStore.StoreChunkAsync(docId, $"Chunk {i}", CreateTestEmbedding(384), i);
        }

        // Act
        var count = await vectorStore.GetChunkIndexCountAsync();

        // Assert
        Assert.Equal(3, count);
    }

    [Fact]
    public async Task StoreMultipleDocuments_EachHasIndependentChunks()
    {
        // Arrange
        var vectorStore = _fixture.ServiceProvider.GetRequiredService<IVectorStoreService>();
        
        // Store doc 1 with chunks
        await vectorStore.StoreTopicIndexAsync("doc-1", "Summary 1", CreateTestEmbedding(384), "/test/doc1.txt", "hash1");
        await vectorStore.StoreChunkAsync("doc-1", "Chunk 1", CreateTestEmbedding(384), 0);
        
        // Store doc 2 with chunks
        await vectorStore.StoreTopicIndexAsync("doc-2", "Summary 2", CreateTestEmbedding(384), "/test/doc2.txt", "hash2");
        await vectorStore.StoreChunkAsync("doc-2", "Chunk 1", CreateTestEmbedding(384), 0);
        await vectorStore.StoreChunkAsync("doc-2", "Chunk 2", CreateTestEmbedding(384), 1);

        // Act
        var doc1Chunks = await vectorStore.GetChunksByDocumentAsync("doc-1");
        var doc2Chunks = await vectorStore.GetChunksByDocumentAsync("doc-2");

        // Assert
        Assert.Single(doc1Chunks);
        Assert.Equal(2, doc2Chunks.Count);
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
