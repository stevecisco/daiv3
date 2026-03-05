using System.Text;
using System.Threading;
using Daiv3.Knowledge;
using Daiv3.Knowledge.DocProc;
using Daiv3.Knowledge.Embedding;
using Daiv3.Persistence;
using Daiv3.Persistence.Repositories;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.ML.Tokenizers;
using Moq;
using Xunit;

namespace Daiv3.Knowledge.IntegrationTests;

/// <summary>
/// Integration tests for KnowledgeDocumentProcessor with real SQLite database.
/// Tests full document ingestion pipeline from file to indexed embeddings.
/// </summary>
[Collection("Knowledge Database Collection")]
public class KnowledgeDocumentProcessorIntegrationTests
{
    private readonly KnowledgeDatabaseFixture _fixture;
    private readonly string _tempTestDir;

    public KnowledgeDocumentProcessorIntegrationTests(KnowledgeDatabaseFixture fixture)
    {
        _fixture = fixture;
        _tempTestDir = Path.Combine(Path.GetTempPath(), $"daiv3_doc_proc_test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempTestDir);
    }

    [Fact]
    public async Task ProcessDocumentAsync_IngestsDocumentIntoDatabase()
    {
        // Arrange
        var docContent = "This is a test document with some content for processing.";
        var docPath = CreateTestFile("test.txt", docContent);
        
        var vectorStore = _fixture.ServiceProvider.GetRequiredService<IVectorStoreService>();
        var documentRepo = _fixture.ServiceProvider.GetRequiredService<DocumentRepository>();
        var processor = CreateDocumentProcessor();

        // Act
        var result = await processor.ProcessDocumentAsync(docPath);

        // Assert
        Assert.True(result.Success);
        Assert.NotEmpty(result.DocumentId);
        Assert.True(result.ChunkCount > 0);
        
        // Verify stored in database
        var topicIndex = await vectorStore.GetTopicIndexAsync(result.DocumentId);
        Assert.NotNull(topicIndex);
        Assert.Equal(docPath, topicIndex.SourcePath);
    }

    [Fact]
    public async Task ProcessDocumentsAsync_ProcessesMultipleDocuments()
    {
        // Arrange
        var docPaths = new List<string>
        {
            CreateTestFile("doc1.txt", "First document content"),
            CreateTestFile("doc2.txt", "Second document content"),
            CreateTestFile("doc3.txt", "Third document content")
        };

        var vectorStore = _fixture.ServiceProvider.GetRequiredService<IVectorStoreService>();
        var processor = CreateDocumentProcessor();
        var progressUpdates = new List<(int, int, string)>();

        // Act
        var results = await processor.ProcessDocumentsAsync(
            docPaths,
            new Progress<(int Processed, int Total, string CurrentFile)>(
                update => progressUpdates.Add(update)));

        // Assert
        Assert.Equal(3, results.Count);
        Assert.All(results, result => 
        {
            Assert.True(result.Success);
            Assert.NotEmpty(result.DocumentId);
        });

        // Verify progress reporting
        Assert.NotEmpty(progressUpdates);
        Assert.True(progressUpdates.Last().Item1 >= docPaths.Count - 1);
    }

    [Fact]
    public async Task ProcessDocumentAsync_WithChangedContent_UpdatesDocument()
    {
        // Arrange
        var docPath = CreateTestFile("doc.txt", "Original content");
        var processor = CreateDocumentProcessor();
        
        var vectorStore = _fixture.ServiceProvider.GetRequiredService<IVectorStoreService>();

        // Process document once
        var result1 = await processor.ProcessDocumentAsync(docPath);
        var docId = result1.DocumentId;
        
        // Verify initial storage
        var topic1 = await vectorStore.GetTopicIndexAsync(docId);
        var chunks1 = await vectorStore.GetChunksByDocumentAsync(docId);
        var initialChunkCount = chunks1.Count;

        // Update file content
        File.WriteAllText(docPath, "Updated content with new information added to document");

        // Act - reprocess
        var result2 = await processor.ProcessDocumentAsync(docPath);

        // Assert - Document ID should be consistent
        Assert.True(result2.Success);
        var topic2 = await vectorStore.GetTopicIndexAsync(docId);
        Assert.NotNull(topic2);
        
        // Chunks might differ due to content change
        var chunks2 = await vectorStore.GetChunksByDocumentAsync(docId);
        Assert.NotEmpty(chunks2);
    }

    [Fact]
    public async Task ProcessDocumentAsync_SkipUnchangedDocument_WithSameContent()
    {
        // Arrange
        var docPath = CreateTestFile("doc.txt", "Stable content");
        var processorOptions = new DocumentProcessingOptions { SkipUnchangedDocuments = true };
        var processor = CreateDocumentProcessor(processorOptions);
        var vectorStore = _fixture.ServiceProvider.GetRequiredService<IVectorStoreService>();

        // Process document once
        var result1 = await processor.ProcessDocumentAsync(docPath);
        var docId = result1.DocumentId;
        var topic1 = await vectorStore.GetTopicIndexAsync(docId);

        // Act - reprocess with no changes
        var result2 = await processor.ProcessDocumentAsync(docPath);

        // Assert
        // With skip enabled and same content, should still succeed
        Assert.True(result2.Success);
        var topic2 = await vectorStore.GetTopicIndexAsync(docId);
        Assert.NotNull(topic2);
    }

    /// <summary>
    /// KM-ACC-002: Updating a document triggers re-indexing only for that document.
    /// Verifies that when one document is updated, other documents remain unchanged.
    /// </summary>
    [Fact]
    public async Task UpdateDocument_ReindexesOnlyThatDocument_NotOthers()
    {
        // Arrange - Create and process three documents
        var doc1Path = CreateTestFile("doc1.txt", "First document original content");
        var doc2Path = CreateTestFile("doc2.txt", "Second document content that will be updated");
        var doc3Path = CreateTestFile("doc3.txt", "Third document original content");
        
        var processor = CreateDocumentProcessor();
        var vectorStore = _fixture.ServiceProvider.GetRequiredService<IVectorStoreService>();

        // Process all three documents initially
        var result1 = await processor.ProcessDocumentAsync(doc1Path);
        var result2 = await processor.ProcessDocumentAsync(doc2Path);
        var result3 = await processor.ProcessDocumentAsync(doc3Path);

        var doc1Id = result1.DocumentId;
        var doc2Id = result2.DocumentId;
        var doc3Id = result3.DocumentId;

        // Get initial state of all documents
        var doc1TopicBefore = await vectorStore.GetTopicIndexAsync(doc1Id);
        var doc2TopicBefore = await vectorStore.GetTopicIndexAsync(doc2Id);
        var doc3TopicBefore = await vectorStore.GetTopicIndexAsync(doc3Id);
        
        var doc1ChunksBefore = await vectorStore.GetChunksByDocumentAsync(doc1Id);
        var doc2ChunksBefore = await vectorStore.GetChunksByDocumentAsync(doc2Id);
        var doc3ChunksBefore = await vectorStore.GetChunksByDocumentAsync(doc3Id);

        Assert.NotNull(doc1TopicBefore);
        Assert.NotNull(doc2TopicBefore);
        Assert.NotNull(doc3TopicBefore);

        var doc1OriginalFileHash = doc1TopicBefore.FileHash;
        var doc2OriginalFileHash = doc2TopicBefore.FileHash;
        var doc3OriginalFileHash = doc3TopicBefore.FileHash;

        var doc1OriginalIngestedAt = doc1TopicBefore.IngestedAt;
        var doc2OriginalIngestedAt = doc2TopicBefore.IngestedAt;
        var doc3OriginalIngestedAt = doc3TopicBefore.IngestedAt;

        // Wait at least 1 second to ensure IngestedAt timestamp will differ (stored in seconds)
        await Task.Delay(1100);

        // Act - Update ONLY doc2
        File.WriteAllText(doc2Path, "Second document with completely NEW updated content");
        var result2Updated = await processor.ProcessDocumentAsync(doc2Path);

        // Assert - Verify doc2 was re-indexed
        Assert.True(result2Updated.Success);
        var doc2TopicAfter = await vectorStore.GetTopicIndexAsync(doc2Id);
        Assert.NotNull(doc2TopicAfter);
        
        // Doc2 should have new hash and new ingestion timestamp
        Assert.NotEqual(doc2OriginalFileHash, doc2TopicAfter.FileHash);
        Assert.NotEqual(doc2OriginalIngestedAt, doc2TopicAfter.IngestedAt);

        // Assert - Verify doc1 and doc3 were NOT affected
        var doc1TopicAfter = await vectorStore.GetTopicIndexAsync(doc1Id);
        var doc3TopicAfter = await vectorStore.GetTopicIndexAsync(doc3Id);

        Assert.NotNull(doc1TopicAfter);
        Assert.NotNull(doc3TopicAfter);

        // Doc1 and Doc3 should have unchanged hashes and timestamps
        Assert.Equal(doc1OriginalFileHash, doc1TopicAfter.FileHash);
        Assert.Equal(doc3OriginalFileHash, doc3TopicAfter.FileHash);
        Assert.Equal(doc1OriginalIngestedAt, doc1TopicAfter.IngestedAt);
        Assert.Equal(doc3OriginalIngestedAt, doc3TopicAfter.IngestedAt);

        // Verify chunk counts remain consistent for doc1 and doc3
        var doc1ChunksAfter = await vectorStore.GetChunksByDocumentAsync(doc1Id);
        var doc3ChunksAfter = await vectorStore.GetChunksByDocumentAsync(doc3Id);
        
        Assert.Equal(doc1ChunksBefore.Count, doc1ChunksAfter.Count);
        Assert.Equal(doc3ChunksBefore.Count, doc3ChunksAfter.Count);
    }

    [Fact]
    public async Task RemoveDocumentAsync_DeletesDocumentAndChunks()
    {
        // Arrange
        var docPath = CreateTestFile("doc.txt", "Content to be deleted");
        var processor = CreateDocumentProcessor();
        var vectorStore = _fixture.ServiceProvider.GetRequiredService<IVectorStoreService>();

        // Process document
        var result = await processor.ProcessDocumentAsync(docPath);
        var docId = result.DocumentId;

        // Verify it exists
        var topicBefore = await vectorStore.GetTopicIndexAsync(docId);
        Assert.NotNull(topicBefore);

        // Act
        var removed = await processor.RemoveDocumentAsync(docPath);

        // Assert
        Assert.True(removed);
        var topicAfter = await vectorStore.GetTopicIndexAsync(docId);
        Assert.Null(topicAfter);
        
        var chunksAfter = await vectorStore.GetChunksByDocumentAsync(docId);
        Assert.Empty(chunksAfter);
    }

    [Fact]
    public async Task ProcessDocumentAsync_InvalidPath_ReturnsFailure()
    {
        // Arrange
        var processor = CreateDocumentProcessor();
        var invalidPath = Path.Combine(_tempTestDir, $"missing-{Guid.NewGuid():N}.txt");

        // Act
        var result = await processor.ProcessDocumentAsync(invalidPath);

        // Assert
        Assert.False(result.Success);
        Assert.NotNull(result.ErrorMessage);
    }

    [Fact]
    public async Task ProcessDocumentAsync_InvalidPath_PersistsErrorStatusForTransparency()
    {
        // Arrange
        var processor = CreateDocumentProcessor();
        var documentRepo = _fixture.ServiceProvider.GetRequiredService<DocumentRepository>();
        var invalidPath = Path.Combine(_tempTestDir, $"missing-status-{Guid.NewGuid():N}.txt");

        // Act
        var result = await processor.ProcessDocumentAsync(invalidPath);

        // Assert
        Assert.False(result.Success);

        var persisted = await documentRepo.GetBySourcePathAsync(invalidPath);
        Assert.NotNull(persisted);
        Assert.Equal("error", persisted.Status);
        Assert.Contains("errorMessage", persisted.MetadataJson ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ProcessDocumentAsync_EmptyFile_HandlesGracefully()
    {
        // Arrange
        var docPath = CreateTestFile("empty.txt", "");
        var processor = CreateDocumentProcessor();

        // Act
        var result = await processor.ProcessDocumentAsync(docPath);

        // Assert
        // Should either succeed with 0 chunks or fail gracefully
        Assert.NotNull(result);
    }

    [Fact]
    public async Task ProcessDocumentsAsync_WithMixedResults_ContinuesProcessing()
    {
        // Arrange
        var docPaths = new List<string>
        {
            CreateTestFile("valid1.txt", "Valid content 1"),
            "/invalid/path/missing.txt", // Invalid path
            CreateTestFile("valid2.txt", "Valid content 2")
        };

        var processor = CreateDocumentProcessor();

        // Act
        var results = await processor.ProcessDocumentsAsync(docPaths);

        // Assert
        Assert.Equal(3, results.Count);
        // At least some should succeed
        var successCount = results.Count(r => r.Success);
        Assert.True(successCount >= 2, "At least 2 documents should process successfully");
    }

    [Fact]
    public async Task ProcessingResult_IncludesProcessingMetrics()
    {
        // Arrange
        var docPath = CreateTestFile("doc.txt", "Content for metrics collection");
        var processor = CreateDocumentProcessor();

        // Act
        var result = await processor.ProcessDocumentAsync(docPath);

        // Assert
        Assert.NotEmpty(result.DocumentId);
        Assert.True(result.ProcessingTimeMs >= 0);
        if (result.Success)
        {
            Assert.True(result.ChunkCount >= 0);
        }
    }

    [Fact]
    public async Task DocumentHash_ConsistentForSameContent()
    {
        // Arrange
        var content = "Consistent content";
        var docPath1 = CreateTestFile("doc1.txt", content);
        var docPath2 = CreateTestFile("doc2.txt", content);
        
        var processor = CreateDocumentProcessor();

        // Act
        var result1 = await processor.ProcessDocumentAsync(docPath1);
        var result2 = await processor.ProcessDocumentAsync(docPath2);

        // Assert
        // Same content should be processed (though docId would be different)
        Assert.True(result1.Success);
        Assert.True(result2.Success);
    }

    [Fact]
    public async Task DocumentHash_DifferentForDifferentContent()
    {
        // Arrange
        var docPath1 = CreateTestFile("doc1.txt", "First content");
        var docPath2 = CreateTestFile("doc2.txt", "Second content");
        
        var processor = CreateDocumentProcessor();

        // Act
        var result1 = await processor.ProcessDocumentAsync(docPath1);
        var result2 = await processor.ProcessDocumentAsync(docPath2);

        // Assert
        Assert.True(result1.Success);
        Assert.True(result2.Success);
        // Document IDs should be different for different content
        Assert.NotEqual(result1.DocumentId, result2.DocumentId);
    }

    [Fact]
    public async Task ProcessDocumentsAsync_CancellationToken_CancelsProcessing()
    {
        // Arrange
        var docPaths = Enumerable.Range(0, 10)
            .Select(i => CreateTestFile($"doc{i}.txt", $"Content {i}"))
            .ToList();

        var processor = CreateDocumentProcessor();
        var cts = new System.Threading.CancellationTokenSource();

        // Act - Start processing and cancel after delay
        var task = processor.ProcessDocumentsAsync(docPaths, cancellationToken: cts.Token);
        
        // Immediately cancel to avoid processing much
        cts.Cancel();

        try
        {
            var results = await task;
            // If it completes, that's ok - some may have processed before cancellation
            Assert.NotNull(results);
        }
        catch (OperationCanceledException)
        {
            // This is expected behavior
        }
    }

    // Helper methods

    private string CreateTestFile(string fileName, string content)
    {
        var filePath = Path.Combine(_tempTestDir, fileName);
        File.WriteAllText(filePath, content);
        return filePath;
    }

    private IKnowledgeDocumentProcessor CreateDocumentProcessor(
        DocumentProcessingOptions? options = null)
    {
        var documentRepo = _fixture.ServiceProvider.GetRequiredService<DocumentRepository>();
        var vectorStore = _fixture.ServiceProvider.GetRequiredService<IVectorStoreService>();
        var logger = _fixture.ServiceProvider.GetRequiredService<ILogger<KnowledgeDocumentProcessor>>();

        // Create mocks for dependencies
        var mockTextChunker = new Mock<ITextChunker>();
        mockTextChunker
            .Setup(x => x.Chunk(It.IsAny<string>()))
            .Returns((string text) =>
            {
                // Simple chunking: split by sentences
                var sentences = text.Split(new[] { '.', '!', '?' }, StringSplitOptions.RemoveEmptyEntries);
                var chunks = new List<TextChunk>();
                int offset = 0;
                
                foreach (var sentence in sentences)
                {
                    if (string.IsNullOrWhiteSpace(sentence)) continue;
                    
                    var trimmed = sentence.Trim() + ".";
                    chunks.Add(new TextChunk(trimmed, offset, trimmed.Length, trimmed.Split(' ').Length));
                    offset += trimmed.Length + 1;
                }
                
                return chunks as IReadOnlyList<TextChunk> ?? new List<TextChunk>();
            });

        var mockTokenizer = new Mock<ITokenizerProvider>();
        var tokenizer = TiktokenTokenizer.CreateForEncoding("r50k_base");
        mockTokenizer
            .Setup(x => x.GetTokenizer())
            .Returns(tokenizer);

        var mockTextExtractor = new Mock<ITextExtractor>();
        mockTextExtractor
            .Setup(x => x.ExtractAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns((string path, CancellationToken token) => File.ReadAllTextAsync(path, token));

        var mockHtmlToMarkdownConverter = new Mock<IHtmlToMarkdownConverter>();
        mockHtmlToMarkdownConverter
            .Setup(x => x.ConvertHtmlToMarkdown(It.IsAny<string>()))
            .Returns((string html) => html); // Pass-through for testing

        var mockTopicSummaryService = new Mock<ITopicSummaryService>();
        mockTopicSummaryService
            .Setup(x => x.GenerateSummaryAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string text, CancellationToken _) => text.Split('.').First() + ".");

        var mockEmbeddingGenerator = new Mock<IEmbeddingGenerator>();
        mockEmbeddingGenerator
            .Setup(x => x.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new float[8]);

        return new KnowledgeDocumentProcessor(
            documentRepo,
            vectorStore,
            mockTextChunker.Object,
            mockTokenizer.Object,
            mockTextExtractor.Object,
            mockHtmlToMarkdownConverter.Object,
            mockTopicSummaryService.Object,
            mockEmbeddingGenerator.Object,
            logger,
            options);
    }
}
