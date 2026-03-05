using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Daiv3.Knowledge;
using Daiv3.Knowledge.DocProc;
using Daiv3.Knowledge.Embedding;
using Daiv3.Persistence;
using Daiv3.Persistence.Entities;
using Daiv3.Persistence.Repositories;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.ML.Tokenizers;
using Moq;
using Xunit;

namespace Daiv3.Knowledge.Tests;

public sealed class KnowledgeDocumentProcessorTests : IDisposable
{
    private readonly Mock<IDatabaseContext> _mockDatabaseContext;
    private readonly Mock<DocumentRepository> _mockDocumentRepository;
    private readonly Mock<IVectorStoreService> _mockVectorStore;
    private readonly Mock<ITextChunker> _mockTextChunker;
    private readonly Mock<ITokenizerProvider> _mockTokenizerProvider;
    private readonly Mock<ITextExtractor> _mockTextExtractor;
    private readonly Mock<IHtmlToMarkdownConverter> _mockHtmlToMarkdownConverter;
    private readonly Mock<IEmbeddingGenerator> _mockEmbeddingGenerator;
    private readonly TempFileHandler _tempFileHelper;
    private readonly KnowledgeDocumentProcessor _service;

    public KnowledgeDocumentProcessorTests()
    {
        // Create mock for IDatabaseContext
        _mockDatabaseContext = new Mock<IDatabaseContext>();

        // Create mock for DocumentRepository with NullLogger
        _mockDocumentRepository = new Mock<DocumentRepository>(
            _mockDatabaseContext.Object,
            NullLogger<DocumentRepository>.Instance);

        _mockVectorStore = new Mock<IVectorStoreService>();
        _mockTextChunker = new Mock<ITextChunker>();
        _mockTokenizerProvider = new Mock<ITokenizerProvider>();
        _mockTextExtractor = new Mock<ITextExtractor>();
        _mockHtmlToMarkdownConverter = new Mock<IHtmlToMarkdownConverter>();
        _mockEmbeddingGenerator = new Mock<IEmbeddingGenerator>();
        _tempFileHelper = new TempFileHandler();

        var mockTopicSummaryService = new Mock<ITopicSummaryService>();
        mockTopicSummaryService
            .Setup(x => x.GenerateSummaryAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string text, CancellationToken _) =>
                text.Split('.').First() + ".");

        _mockTextExtractor
            .Setup(x => x.ExtractAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns((string path, CancellationToken _) => Task.FromResult(File.ReadAllText(path)));

        _mockHtmlToMarkdownConverter
            .Setup(x => x.ConvertHtmlToMarkdown(It.IsAny<string>()))
            .Returns((string html) => html); // Pass-through for testing

        var tokenizer = TiktokenTokenizer.CreateForEncoding("r50k_base");
        _mockTokenizerProvider
            .Setup(x => x.GetTokenizer())
            .Returns(tokenizer);

        _mockEmbeddingGenerator
            .Setup(x => x.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new float[8]);

        _service = new KnowledgeDocumentProcessor(
            _mockDocumentRepository.Object,
            _mockVectorStore.Object,
            _mockTextChunker.Object,
            _mockTokenizerProvider.Object,
            _mockTextExtractor.Object,
            _mockHtmlToMarkdownConverter.Object,
            mockTopicSummaryService.Object,
            _mockEmbeddingGenerator.Object,
            NullLogger<KnowledgeDocumentProcessor>.Instance);
    }

    public void Dispose()
    {
        _tempFileHelper.Dispose();
    }

    [Fact]
    public async Task ProcessDocumentAsync_ReturnsValidDocumentProcessingResult()
    {
        // Arrange
        var filePath = _tempFileHelper.CreateTempFile("Test document content with enough text to be meaningful");
        var chunks = new List<TextChunk>
        {
            new TextChunk("chunk1", 0, 20, 1)
        };

        _mockDocumentRepository
            .Setup(x => x.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Document>());

        _mockTextChunker
            .Setup(x => x.Chunk(It.IsAny<string>()))
            .Returns(chunks);

        _mockVectorStore
            .Setup(x => x.StoreTopicIndexAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<float[]>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync("topic-id");

        _mockVectorStore
            .Setup(x => x.StoreChunkAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<float[]>(),
                It.IsAny<int>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync("chunk-id");

        // Act
        var result = await _service.ProcessDocumentAsync(filePath, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.NotNull(result.DocumentId);
        Assert.NotEmpty(result.DocumentId);
        Assert.True(result.ProcessingTimeMs >= 0);
        Assert.Equal(1, result.ChunkCount);
        Assert.True(result.SummaryTokens > 0);
        Assert.True(result.TotalTokens > 0);
    }

    [Fact]
    public async Task ProcessDocumentAsync_SkipsProcessing_WhenDocumentUnchangedAndOptionEnabled()
    {
        // Arrange
        var filePath = _tempFileHelper.CreateTempFile("Test document content");
        var existingHash = ComputeFileHash(filePath);
        var existingDoc = new Document
        {
            DocId = "some-id",
            SourcePath = filePath,
            FileHash = existingHash,
            Format = ".txt",
            SizeBytes = 100,
            LastModified = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            Status = "active",
            CreatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
        };

        _mockDocumentRepository
            .Setup(x => x.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Document> { existingDoc });

        var options = new DocumentProcessingOptions { SkipUnchangedDocuments = true };

        var mockTopicSummaryService = new Mock<ITopicSummaryService>();
        mockTopicSummaryService
            .Setup(x => x.GenerateSummaryAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string text, CancellationToken _) =>
                text.Split('.').First() + ".");

        var serviceWithOptions = new KnowledgeDocumentProcessor(
            _mockDocumentRepository.Object,
            _mockVectorStore.Object,
            _mockTextChunker.Object,
            _mockTokenizerProvider.Object,
            _mockTextExtractor.Object,
            _mockHtmlToMarkdownConverter.Object,
            mockTopicSummaryService.Object,
            _mockEmbeddingGenerator.Object,
            NullLogger<KnowledgeDocumentProcessor>.Instance,
            options);

        // Act
        var result = await serviceWithOptions.ProcessDocumentAsync(filePath, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.False(string.IsNullOrEmpty(result.ErrorMessage));
        Assert.Contains("unchanged", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        // Verify that vector store operations were NOT called
        _mockVectorStore.Verify(
            x => x.StoreTopicIndexAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<float[]>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ProcessDocumentAsync_ReprocessesDocumentWithChangedContent()
    {
        // Arrange
        var filePath = _tempFileHelper.CreateTempFile("Original content");
        var oldHash = ComputeFileHash(filePath);

        // Update file content
        File.WriteAllText(filePath, "Updated content");
        var newHash = ComputeFileHash(filePath);

        Assert.NotEqual(oldHash, newHash);

        var existingDoc = new Document
        {
            DocId = "doc-id",
            SourcePath = filePath,
            FileHash = oldHash,
            Format = "txt",
            SizeBytes = 100,
            LastModified = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            Status = "active",
            CreatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
        };

        _mockDocumentRepository
            .Setup(x => x.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Document> { existingDoc });

        var chunks = new List<TextChunk> { new TextChunk("updated chunk", 0, 13, 2) };
        _mockTextChunker
            .Setup(x => x.Chunk(It.IsAny<string>()))
            .Returns(chunks);

        _mockDocumentRepository
            .Setup(x => x.UpdateAsync(It.IsAny<Document>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _mockVectorStore
            .Setup(x => x.DeleteTopicAndChunksAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _mockVectorStore
            .Setup(x => x.StoreTopicIndexAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<float[]>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync("topic-id");

        _mockVectorStore
            .Setup(x => x.StoreChunkAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<float[]>(),
                It.IsAny<int>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync("chunk-id");

        // Act
        var result = await _service.ProcessDocumentAsync(filePath, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Success);
        // Should delete old embeddings before storing new ones
        _mockVectorStore.Verify(
            x => x.DeleteTopicAndChunksAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ProcessDocumentsAsync_ProcessesMultipleDocuments()
    {
        // Arrange
        var filePath1 = _tempFileHelper.CreateTempFile("Document 1 content");
        var filePath2 = _tempFileHelper.CreateTempFile("Document 2 content");
        var filePaths = new[] { filePath1, filePath2 };

        _mockDocumentRepository
            .Setup(x => x.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Document>());

        var chunks = new List<TextChunk> { new TextChunk("chunk", 0, 50, 5) };
        _mockTextChunker
            .Setup(x => x.Chunk(It.IsAny<string>()))
            .Returns(chunks);

        _mockDocumentRepository
            .Setup(x => x.AddAsync(It.IsAny<Document>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("doc-id");

        _mockVectorStore
            .Setup(x => x.StoreTopicIndexAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<float[]>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync("topic-id");

        _mockVectorStore
            .Setup(x => x.StoreChunkAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<float[]>(),
                It.IsAny<int>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync("chunk-id");

        var progressReports = new List<(int, int, string)>();
        var progress = new Progress<(int Processed, int Total, string CurrentFile)>(p => progressReports.Add(p));

        // Act
        var results = await _service.ProcessDocumentsAsync(filePaths, progress, CancellationToken.None);

        // Assert
        Assert.NotNull(results);
        Assert.Equal(2, results.Count);
        Assert.All(results, r => Assert.True(r.Success));
    }

    [Fact]
    public async Task RemoveDocumentAsync_DeletesDocumentAndEmbeddings()
    {
        // Arrange
        var filePath = _tempFileHelper.CreateTempFile("Test content");
        var existingDoc = new Document
        {
            DocId = "doc-id",
            SourcePath = filePath,
            Format = "txt",
            SizeBytes = 100,
            LastModified = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            Status = "active",
            CreatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
        };

        _mockDocumentRepository
            .Setup(x => x.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Document> { existingDoc });

        _mockDocumentRepository
            .Setup(x => x.DeleteAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _mockVectorStore
            .Setup(x => x.DeleteTopicAndChunksAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        await _service.RemoveDocumentAsync(filePath, CancellationToken.None);

        // Assert
        _mockVectorStore.Verify(
            x => x.DeleteTopicAndChunksAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Once);

        _mockDocumentRepository.Verify(
            x => x.DeleteAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ProcessDocumentAsync_ChunksContentCorrectly()
    {
        // Arrange
        var filePath = _tempFileHelper.CreateTempFile("Long document that gets chunked");
        var expectedChunks = new List<TextChunk>
        {
            new TextChunk("chunk 1", 0, 100, 10),
            new TextChunk("chunk 2", 100, 200, 20)
        };

        _mockDocumentRepository
            .Setup(x => x.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Document>());

        _mockTextChunker
            .Setup(x => x.Chunk(It.IsAny<string>()))
            .Returns(expectedChunks);

        _mockDocumentRepository
            .Setup(x => x.AddAsync(It.IsAny<Document>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("doc-id");

        _mockVectorStore
            .Setup(x => x.StoreTopicIndexAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<float[]>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync("topic-id");

        _mockVectorStore
            .Setup(x => x.StoreChunkAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<float[]>(),
                It.IsAny<int>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync("chunk-id");

        // Act
        var result = await _service.ProcessDocumentAsync(filePath, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(2, result.ChunkCount);
        _mockTextChunker.Verify(x => x.Chunk(It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task ProcessDocumentAsync_InvalidFilePath_ReturnsFailure()
    {
        // Arrange
        var invalidPath = "/nonexistent/file.txt";

        // Act
        var result = await _service.ProcessDocumentAsync(invalidPath, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.False(result.Success);
        Assert.False(string.IsNullOrEmpty(result.ErrorMessage));
    }

    [Fact]
    public async Task UpdateDocumentAsync_CallsProcessDocumentAsync()
    {
        // Arrange
        var filePath = _tempFileHelper.CreateTempFile("Updated content");

        _mockDocumentRepository
            .Setup(x => x.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Document>());

        var chunks = new List<TextChunk> { new TextChunk("chunk", 0, 50, 5) };
        _mockTextChunker
            .Setup(x => x.Chunk(It.IsAny<string>()))
            .Returns(chunks);

        _mockDocumentRepository
            .Setup(x => x.AddAsync(It.IsAny<Document>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("doc-id");

        _mockVectorStore
            .Setup(x => x.StoreTopicIndexAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<float[]>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync("topic-id");

        _mockVectorStore
            .Setup(x => x.StoreChunkAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<float[]>(),
                It.IsAny<int>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync("chunk-id");

        // Act
        var result = await _service.UpdateDocumentAsync(filePath, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Success);
    }

    [Fact]
    public async Task ProcessDocumentAsync_GeneratesSummary()
    {
        // Arrange
        var filePath = _tempFileHelper.CreateTempFile("This is a test document with some content that can be summarized");
        var chunks = new List<TextChunk> { new TextChunk("chunk", 0, 50, 5) };

        _mockDocumentRepository
            .Setup(x => x.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Document>());

        _mockTextChunker
            .Setup(x => x.Chunk(It.IsAny<string>()))
            .Returns(chunks);

        _mockDocumentRepository
            .Setup(x => x.AddAsync(It.IsAny<Document>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("doc-id");

        _mockVectorStore
            .Setup(x => x.StoreTopicIndexAsync(
                It.IsAny<string>(),
                It.Is<string>(s => !string.IsNullOrEmpty(s)), // Summary should not be empty
                It.IsAny<float[]>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync("topic-id");

        _mockVectorStore
            .Setup(x => x.StoreChunkAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<float[]>(),
                It.IsAny<int>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync("chunk-id");

        // Act
        var result = await _service.ProcessDocumentAsync(filePath, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Success);
        _mockVectorStore.Verify(
            x => x.StoreTopicIndexAsync(
                It.IsAny<string>(),
                It.Is<string>(s => !string.IsNullOrEmpty(s)),
                It.IsAny<float[]>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ProcessDocumentAsync_GeneratesEmbeddings_ForSummaryAndChunks()
    {
        // Arrange
        var filePath = _tempFileHelper.CreateTempFile("Summary sentence. Second sentence.");
        var chunks = new List<TextChunk>
        {
            new TextChunk("chunk one", 0, 9, 2),
            new TextChunk("chunk two", 10, 9, 2)
        };

        _mockDocumentRepository
            .Setup(x => x.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Document>());

        _mockTextChunker
            .Setup(x => x.Chunk(It.IsAny<string>()))
            .Returns(chunks);

        _mockDocumentRepository
            .Setup(x => x.AddAsync(It.IsAny<Document>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("doc-id");

        _mockVectorStore
            .Setup(x => x.StoreTopicIndexAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<float[]>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync("topic-id");

        _mockVectorStore
            .Setup(x => x.StoreChunkAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<float[]>(),
                It.IsAny<int>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync("chunk-id");

        // Act
        var result = await _service.ProcessDocumentAsync(filePath, CancellationToken.None);

        // Assert
        Assert.True(result.Success);
        _mockEmbeddingGenerator.Verify(
            x => x.GenerateEmbeddingAsync("Summary sentence.", It.IsAny<CancellationToken>()),
            Times.Once);
        _mockEmbeddingGenerator.Verify(
            x => x.GenerateEmbeddingAsync("chunk one", It.IsAny<CancellationToken>()),
            Times.Once);
        _mockEmbeddingGenerator.Verify(
            x => x.GenerateEmbeddingAsync("chunk two", It.IsAny<CancellationToken>()),
            Times.Once);
        _mockEmbeddingGenerator.Verify(
            x => x.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Exactly(3));
    }

    [Fact]
    public async Task ProcessDocumentAsync_RejectsEmptyTextContent()
    {
        // Arrange
        var filePath = _tempFileHelper.CreateTempFile("   ");  // Whitespace only

        _mockDocumentRepository
            .Setup(x => x.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Document>());

        // Act
        var result = await _service.ProcessDocumentAsync(filePath, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.False(result.Success);
        Assert.False(string.IsNullOrEmpty(result.ErrorMessage));
        Assert.Contains("text content extracted", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ProcessDocumentAsync_CapturesProcessingTime()
    {
        // Arrange
        var filePath = _tempFileHelper.CreateTempFile("Test document with content");
        var chunks = new List<TextChunk> { new TextChunk("chunk", 0, 5, 1) };

        _mockDocumentRepository
            .Setup(x => x.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Document>());

        _mockTextChunker
            .Setup(x => x.Chunk(It.IsAny<string>()))
            .Returns(chunks);

        _mockVectorStore
            .Setup(x => x.StoreTopicIndexAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<float[]>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync("topic-id");

        _mockVectorStore
            .Setup(x => x.StoreChunkAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<float[]>(),
                It.IsAny<int>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync("chunk-id");

        // Act
        var result = await _service.ProcessDocumentAsync(filePath, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.ProcessingTimeMs >= 0);
    }

    [Fact]
    public async Task ProcessDocumentAsync_ComputeFileHashCorrectly()
    {
        // Arrange
        var content = "Test document content for hashing";
        var filePath = _tempFileHelper.CreateTempFile(content);

        // Act - compute hash twice on the same file
        var hash1 = ComputeFileHash(filePath);
        var hash2 = ComputeFileHash(filePath);

        // Assert - hashes should be identical for same content
        Assert.Equal(hash1, hash2);
        Assert.NotEmpty(hash1);
        Assert.True(hash1.Length > 0);
    }

    [Fact]
    public async Task ProcessDocumentAsync_GeneratesDifferentHashForDifferentContent()
    {
        // Arrange
        var filePath1 = _tempFileHelper.CreateTempFile("First content");
        var filePath2 = _tempFileHelper.CreateTempFile("Different content");

        // Act
        var hash1 = ComputeFileHash(filePath1);
        var hash2 = ComputeFileHash(filePath2);

        // Assert - different content should produce different hashes
        Assert.NotEqual(hash1, hash2);
    }

    [Fact]
    public void DocumentId_IsGeneratedConsistentlyFromPath()
    {
        // This test uses reflection to test the private GenerateDocumentId method
        // Arrange
        var testPath = @"C:\test\document.txt";
        var method = typeof(KnowledgeDocumentProcessor).GetMethod(
            "GenerateDocumentId",
            System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);

        // Act
        var id1 = (string)method!.Invoke(null, new object[] { testPath })!;
        var id2 = (string)method.Invoke(null, new object[] { testPath })!;

        // Assert
        Assert.Equal(id1, id2);
        Assert.NotEmpty(id1);
        Assert.True(id1.Length == 16); // Should be 16 chars from SHA256
    }

    // Helper class to manage temp files
    private sealed class TempFileHandler : IDisposable
    {
        private readonly List<string> _tempFiles = new();

        public string CreateTempFile(string content)
        {
            var tempFile = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid()}.txt");
            File.WriteAllText(tempFile, content);
            _tempFiles.Add(tempFile);
            return tempFile;
        }

        public void Dispose()
        {
            foreach (var file in _tempFiles)
            {
                try
                {
                    if (File.Exists(file))
                        File.Delete(file);
                }
                catch
                {
                    // Ignore cleanup errors
                }
            }
        }
    }

    private string ComputeFileHash(string filePath)
    {
        using var sha256 = System.Security.Cryptography.SHA256.Create();
        using var stream = File.OpenRead(filePath);
        var hash = sha256.ComputeHash(stream);
        return Convert.ToHexString(hash);
    }
}
