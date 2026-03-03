using Daiv3.Persistence;
using Daiv3.Persistence.Entities;
using Daiv3.Persistence.Repositories;
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

    private string GetUniquePath(string basePath) => $"{basePath}_{Guid.NewGuid():N}";
    private string GetUniqueHash() => Guid.NewGuid().ToString()[..16];

    [Fact]
    public async Task StoreTopicIndex_StoresAndRetrievesFromDatabase()
    {
        // Arrange
        var vectorStore = _fixture.ServiceProvider.GetRequiredService<IVectorStoreService>();
        var documentRepository = _fixture.ServiceProvider.GetRequiredService<DocumentRepository>();
        var docId = $"doc-{Guid.NewGuid():N}";
        var summaryText = "Test document summary";
        var embedding = CreateTestEmbedding(384);
        var sourcePath = GetUniquePath("/test/document.txt");
        var fileHash = GetUniqueHash();

        // Create document first (required for foreign key constraint)
        var document = new Document
        {
            DocId = docId,
            SourcePath = sourcePath,
            FileHash = fileHash,
            Format = ".txt",
            SizeBytes = 1000,
            LastModified = System.DateTime.UtcNow.ToFileTime(),
            Status = "indexed",
            CreatedAt = System.DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            MetadataJson = null
        };
        await documentRepository.AddAsync(document);

        // Act
        var storedId = await vectorStore.StoreTopicIndexAsync(docId, summaryText, embedding, sourcePath, fileHash);
        
        // Assert
        Assert.NotNull(storedId);
        var retrieved = await vectorStore.GetTopicIndexAsync(docId);
        Assert.NotNull(retrieved);
        Assert.Equal(docId, retrieved.DocId);
        Assert.Equal(summaryText, retrieved.SummaryText);
        Assert.Equal(sourcePath, retrieved.SourcePath);
        Assert.Equal(384, retrieved.EmbeddingDimensions);
        Assert.NotEmpty(retrieved.EmbeddingBlob);
    }

    [Fact]
    public async Task StoreChunk_StoresAndRetrievesFromDatabase()
    {
        // Arrange
        var vectorStore = _fixture.ServiceProvider.GetRequiredService<IVectorStoreService>();
        var documentRepository = _fixture.ServiceProvider.GetRequiredService<DocumentRepository>();
        var docId = $"doc-{Guid.NewGuid():N}";
        var chunkText = "This is a chunk of text from the document.";
        var embedding = CreateTestEmbedding(384);
        var chunkOrder = 0;

        // Create document first
        var sourcePath = GetUniquePath("/test/doc2.txt");
        var fileHash = GetUniqueHash();
        var document = new Document
        {
            DocId = docId,
            SourcePath = sourcePath,
            FileHash = fileHash,
            Format = ".txt",
            SizeBytes = 1000,
            LastModified = System.DateTime.UtcNow.ToFileTime(),
            Status = "indexed",
            CreatedAt = System.DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            MetadataJson = null
        };
        await documentRepository.AddAsync(document);

        // First store a topic index (required for chunks)
        await vectorStore.StoreTopicIndexAsync(docId, "Summary", CreateTestEmbedding(384), sourcePath, fileHash);

        // Act
        var chunkId = await vectorStore.StoreChunkAsync(docId, chunkText, embedding, chunkOrder);

        // Assert
        Assert.NotNull(chunkId);
        var retrieved = await vectorStore.GetChunkAsync(chunkId);
        Assert.NotNull(retrieved);
        Assert.Equal(chunkText, retrieved.ChunkText);
        Assert.Equal(chunkOrder, retrieved.ChunkOrder);
        Assert.Equal(384, retrieved.EmbeddingDimensions);
        Assert.NotEmpty(retrieved.EmbeddingBlob);
    }

    [Fact]
    public async Task GetChunksByDocument_RetrievesAllChunksForDocument()
    {
        // Arrange
        var vectorStore = _fixture.ServiceProvider.GetRequiredService<IVectorStoreService>();
        var documentRepository = _fixture.ServiceProvider.GetRequiredService<DocumentRepository>();
        var docId = $"doc-{Guid.NewGuid():N}";
        var sourcePath = GetUniquePath("/test/doc3.txt");
        var fileHash = GetUniqueHash();
        
        // Create document first
        await CreateTestDocumentAsync(documentRepository, docId, sourcePath, fileHash);
        
        // Store topic index
        await vectorStore.StoreTopicIndexAsync(docId, "Summary", CreateTestEmbedding(384), sourcePath, fileHash);

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
        var documentRepository = _fixture.ServiceProvider.GetRequiredService<DocumentRepository>();
        var docId = $"doc-{Guid.NewGuid():N}";
        var sourcePath = GetUniquePath("/test/doc4.txt");
        var fileHash = GetUniqueHash();
        
        // Create document first
        await CreateTestDocumentAsync(documentRepository, docId, sourcePath, fileHash);
        
        // Store topic and chunks
        await vectorStore.StoreTopicIndexAsync(docId, "Summary", CreateTestEmbedding(384), sourcePath, fileHash);
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
        var documentRepository = _fixture.ServiceProvider.GetRequiredService<DocumentRepository>();
        var docId = $"doc-{Guid.NewGuid():N}";
        var sourcePath = GetUniquePath("/test/doc5.txt");
        var fileHash = GetUniqueHash();
        
        // Create document first
        await CreateTestDocumentAsync(documentRepository, docId, sourcePath, fileHash);
        
        // Store topic
        await vectorStore.StoreTopicIndexAsync(docId, "Summary", CreateTestEmbedding(384), sourcePath, fileHash);

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
    public async Task StoreMultipleDocuments_EachHasIndependentChunks()
    {
        // Arrange
        var vectorStore = _fixture.ServiceProvider.GetRequiredService<IVectorStoreService>();
        var documentRepository = _fixture.ServiceProvider.GetRequiredService<DocumentRepository>();
        
        var docId1 = $"doc-multi-1-{Guid.NewGuid():N}";
        var sourcePath1 = GetUniquePath("/test/doc-multi1.txt");
        var fileHash1 = GetUniqueHash();
        
        var docId2 = $"doc-multi-2-{Guid.NewGuid():N}";
        var sourcePath2 = GetUniquePath("/test/doc-multi2.txt");
        var fileHash2 = GetUniqueHash();
        
        // Store doc 1 with chunks
        await CreateTestDocumentAsync(documentRepository, docId1, sourcePath1, fileHash1);
        await vectorStore.StoreTopicIndexAsync(docId1, "Summary 1", CreateTestEmbedding(384), sourcePath1, fileHash1);
        await vectorStore.StoreChunkAsync(docId1, "Chunk 1", CreateTestEmbedding(384), 0);
        
        // Store doc 2 with chunks
        await CreateTestDocumentAsync(documentRepository, docId2, sourcePath2, fileHash2);
        await vectorStore.StoreTopicIndexAsync(docId2, "Summary 2", CreateTestEmbedding(384), sourcePath2, fileHash2);
        await vectorStore.StoreChunkAsync(docId2, "Chunk 1", CreateTestEmbedding(384), 0);
        await vectorStore.StoreChunkAsync(docId2, "Chunk 2", CreateTestEmbedding(384), 1);

        // Act
        var doc1Chunks = await vectorStore.GetChunksByDocumentAsync(docId1);
        var doc2Chunks = await vectorStore.GetChunksByDocumentAsync(docId2);

        // Assert
        Assert.Single(doc1Chunks);
        Assert.Equal(2, doc2Chunks.Count);
    }

    [Fact]
    public async Task StoreTopicIndex_WhenCalledTwiceForSameDocument_MaintainsSingleTier1Vector()
    {
        // Arrange
        var vectorStore = _fixture.ServiceProvider.GetRequiredService<IVectorStoreService>();
        var documentRepository = _fixture.ServiceProvider.GetRequiredService<DocumentRepository>();
        var docId = $"doc-upsert-{Guid.NewGuid():N}";
        var sourcePath = GetUniquePath("/test/doc-upsert.txt");
        var fileHash = GetUniqueHash();
        var initialCount = await vectorStore.GetTopicIndexCountAsync();

        await CreateTestDocumentAsync(documentRepository, docId, sourcePath, fileHash);

        // Act - first write
        await vectorStore.StoreTopicIndexAsync(
            docId,
            "Initial summary",
            CreateTestEmbedding(384),
            sourcePath,
            fileHash);

        // Act - second write for same docId should update, not create another row
        await vectorStore.StoreTopicIndexAsync(
            docId,
            "Updated summary",
            CreateTestEmbedding(384),
            sourcePath,
            fileHash);

        // Assert
        var topicCount = await vectorStore.GetTopicIndexCountAsync();
        var topic = await vectorStore.GetTopicIndexAsync(docId);

        Assert.Equal(initialCount + 1, topicCount);
        Assert.NotNull(topic);
        Assert.Equal("Updated summary", topic.SummaryText);
        Assert.Equal(docId, topic.DocId);
    }

    // Helper method to create test documents
    private async Task CreateTestDocumentAsync(DocumentRepository repository, string docId, string sourcePath, string fileHash)
    {
        var document = new Document
        {
            DocId = docId,
            SourcePath = sourcePath,
            FileHash = fileHash,
            Format = ".txt",
            SizeBytes = 1000,
            LastModified = System.DateTime.UtcNow.ToFileTime(),
            Status = "indexed",
            CreatedAt = System.DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            MetadataJson = null
        };
        await repository.AddAsync(document);
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


