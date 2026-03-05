using Daiv3.Knowledge.DocProc;
using Daiv3.Knowledge.Embedding;
using Daiv3.Persistence;
using Daiv3.Persistence.Entities;
using Daiv3.Persistence.Repositories;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;

namespace Daiv3.Knowledge;

/// <summary>
/// Orchestrates the knowledge document ingestion pipeline.
/// Handles text extraction, chunking, embedding generation, and SQLite storage.
/// </summary>
public class KnowledgeDocumentProcessor : IKnowledgeDocumentProcessor
{
    private readonly DocumentRepository _documentRepository;
    private readonly IVectorStoreService _vectorStore;
    private readonly ITextChunker _textChunker;
    private readonly ITokenizerProvider _tokenizerProvider;
    private readonly ITextExtractor _textExtractor;
    private readonly IHtmlToMarkdownConverter _htmlToMarkdownConverter;
    private readonly ITopicSummaryService _topicSummaryService;
    private readonly IEmbeddingGenerator _embeddingGenerator;
    private readonly ILogger<KnowledgeDocumentProcessor> _logger;
    private readonly DocumentProcessingOptions _options;

    public KnowledgeDocumentProcessor(
        DocumentRepository documentRepository,
        IVectorStoreService vectorStore,
        ITextChunker textChunker,
        ITokenizerProvider tokenizerProvider,
        ITextExtractor textExtractor,
        IHtmlToMarkdownConverter htmlToMarkdownConverter,
        ITopicSummaryService topicSummaryService,
        IEmbeddingGenerator embeddingGenerator,
        ILogger<KnowledgeDocumentProcessor> logger,
        DocumentProcessingOptions? options = null)
    {
        _documentRepository = documentRepository ?? throw new ArgumentNullException(nameof(documentRepository));
        _vectorStore = vectorStore ?? throw new ArgumentNullException(nameof(vectorStore));
        _textChunker = textChunker ?? throw new ArgumentNullException(nameof(textChunker));
        _tokenizerProvider = tokenizerProvider ?? throw new ArgumentNullException(nameof(tokenizerProvider));
        _textExtractor = textExtractor ?? throw new ArgumentNullException(nameof(textExtractor));
        _htmlToMarkdownConverter = htmlToMarkdownConverter ?? throw new ArgumentNullException(nameof(htmlToMarkdownConverter));
        _topicSummaryService = topicSummaryService ?? throw new ArgumentNullException(nameof(topicSummaryService));
        _embeddingGenerator = embeddingGenerator ?? throw new ArgumentNullException(nameof(embeddingGenerator));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _options = options ?? new DocumentProcessingOptions();
    }

    public async Task<DocumentProcessingResult> ProcessDocumentAsync(
        string documentPath,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(documentPath);

        var stopwatch = Stopwatch.StartNew();
        var result = new DocumentProcessingResult { DocumentId = GenerateDocumentId(documentPath) };

        try
        {
            if (!File.Exists(documentPath))
            {
                result.Success = false;
                result.ErrorMessage = "File not found";
                await UpsertDocumentWithStatusAsync(documentPath, "error", result.ErrorMessage, cancellationToken).ConfigureAwait(false);
                stopwatch.Stop();
                result.ProcessingTimeMs = stopwatch.ElapsedMilliseconds;
                _logger.LogWarning("Document file not found: {Path}", documentPath);
                return result;
            }

            var fileInfo = new FileInfo(documentPath);
            var fileHash = await ComputeFileHashAsync(documentPath, cancellationToken).ConfigureAwait(false);

            // Check if document already exists and hasn't changed
            var existingDocs = await _documentRepository.GetAllAsync(cancellationToken).ConfigureAwait(false) ?? [];
            var existingEntry = existingDocs.FirstOrDefault(d => d.SourcePath == documentPath);

            if (existingEntry != null && _options.SkipUnchangedDocuments && existingEntry.FileHash == fileHash)
            {
                result.Success = true;
                result.ErrorMessage = "Document unchanged (skipped)";
                stopwatch.Stop();
                result.ProcessingTimeMs = stopwatch.ElapsedMilliseconds;
                _logger.LogDebug("Skipped processing unchanged document: {Path}", documentPath);
                return result;
            }

            var text = await _textExtractor.ExtractAsync(documentPath, cancellationToken).ConfigureAwait(false);

            if (string.IsNullOrWhiteSpace(text))
            {
                result.Success = false;
                result.ErrorMessage = "No text content extracted";
                await UpsertDocumentWithStatusAsync(documentPath, "error", result.ErrorMessage, cancellationToken).ConfigureAwait(false);
                stopwatch.Stop();
                result.ProcessingTimeMs = stopwatch.ElapsedMilliseconds;
                _logger.LogWarning("No text extracted from document: {Path}", documentPath);
                return result;
            }

            // Convert HTML to Markdown if this is an HTML document
            var extension = Path.GetExtension(documentPath).ToLowerInvariant();
            if (extension is ".html" or ".htm")
            {
                try
                {
                    text = _htmlToMarkdownConverter.ConvertHtmlToMarkdown(text);
                    _logger.LogDebug("Converted HTML document to Markdown: {Path}", documentPath);
                }
                catch (InvalidOperationException ex)
                {
                    _logger.LogWarning(ex, "Failed to convert HTML to Markdown, using plain text extraction for: {Path}", documentPath);
                    // Continue with plain text extraction if conversion fails
                }
            }

            // Generate topic summary using extractive summarization
            var summary = await _topicSummaryService.GenerateSummaryAsync(text, cancellationToken)
                .ConfigureAwait(false);
            var summaryTokens = CountTokens(summary);

            // Chunk the document
            var chunks = _textChunker.Chunk(text);

            if (chunks.Count == 0)
            {
                result.Success = false;
                result.ErrorMessage = "Document could not be chunked";
                await UpsertDocumentWithStatusAsync(documentPath, "error", result.ErrorMessage, cancellationToken).ConfigureAwait(false);
                stopwatch.Stop();
                result.ProcessingTimeMs = stopwatch.ElapsedMilliseconds;
                return result;
            }

            // Create or update document entry
            var docId = result.DocumentId;
            var document = new Document
            {
                DocId = docId,
                SourcePath = documentPath,
                FileHash = fileHash,
                Format = Path.GetExtension(documentPath).TrimStart('.').ToLowerInvariant(),
                SizeBytes = fileInfo.Length,
                LastModified = fileInfo.LastWriteTimeUtc.ToFileTimeUtc(),
                Status = "indexed",
                CreatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                MetadataJson = BuildMetadataJson(
                    warningCount: 0,
                    errorMessage: null,
                    isSensitive: false,
                    isShareable: true,
                    machineLocation: Environment.MachineName,
                    lastIndexedAtUnix: DateTimeOffset.UtcNow.ToUnixTimeSeconds())
            };

            // Check if we need to update existing document
            if (existingEntry != null)
            {
                // Delete old embeddings first
                await _vectorStore.DeleteTopicAndChunksAsync(docId, cancellationToken).ConfigureAwait(false);
                await _documentRepository.UpdateAsync(document, cancellationToken).ConfigureAwait(false);
                _logger.LogInformation("Updated document: {Path}", documentPath);
            }
            else
            {
                await _documentRepository.AddAsync(document, cancellationToken).ConfigureAwait(false);
                _logger.LogInformation("Added new document: {Path}", documentPath);
            }

            // Generate and store embeddings using ONNX Runtime
            var summaryEmbedding = await _embeddingGenerator
                .GenerateEmbeddingAsync(summary, cancellationToken)
                .ConfigureAwait(false);

            await _vectorStore.StoreTopicIndexAsync(
                docId,
                summary,
                summaryEmbedding,
                documentPath,
                fileHash,
                ct: cancellationToken).ConfigureAwait(false);

            // Store chunks with embeddings
            for (int i = 0; i < chunks.Count; i++)
            {
                var chunkEmbedding = await _embeddingGenerator
                    .GenerateEmbeddingAsync(chunks[i].Text, cancellationToken)
                    .ConfigureAwait(false);

                await _vectorStore.StoreChunkAsync(
                    docId,
                    chunks[i].Text,
                    chunkEmbedding,
                    i,
                    ct: cancellationToken).ConfigureAwait(false);
            }

            result.Success = true;
            result.ChunkCount = chunks.Count;
            result.SummaryTokens = summaryTokens;
            result.TotalTokens = CountTokens(text);

            _logger.LogInformation(
                "Successfully processed document {DocId}: {ChunkCount} chunks, {TotalTokens} tokens",
                docId,
                result.ChunkCount,
                result.TotalTokens);
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.ErrorMessage = ex.Message;
            try
            {
                await UpsertDocumentWithStatusAsync(documentPath, "error", ex.Message, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception statusEx)
            {
                _logger.LogDebug(statusEx, "Failed to persist error status for document: {Path}", documentPath);
            }
            _logger.LogError(ex, "Error processing document: {Path}", documentPath);
        }
        finally
        {
            stopwatch.Stop();
            result.ProcessingTimeMs = stopwatch.ElapsedMilliseconds;
        }

        return result;
    }

    public async Task<IReadOnlyList<DocumentProcessingResult>> ProcessDocumentsAsync(
        IEnumerable<string> documentPaths,
        IProgress<(int Processed, int Total, string CurrentFile)>? progressCallback = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(documentPaths);

        var paths = documentPaths.ToList();
        var results = new List<DocumentProcessingResult>();

        for (int i = 0; i < paths.Count; i++)
        {
            try
            {
                var result = await ProcessDocumentAsync(paths[i], cancellationToken).ConfigureAwait(false);
                results.Add(result);

                progressCallback?.Report((i + 1, paths.Count, paths[i]));
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing document in batch: {Path}", paths[i]);
            }
        }

        _logger.LogInformation(
            "Processed {SuccessCount}/{TotalCount} documents successfully",
            results.Count(r => r.Success),
            paths.Count);

        return results;
    }

    public async Task<DocumentProcessingResult> UpdateDocumentAsync(
        string documentPath,
        CancellationToken cancellationToken = default)
    {
        return await ProcessDocumentAsync(documentPath, cancellationToken).ConfigureAwait(false);
    }

    public async Task<bool> RemoveDocumentAsync(
        string documentPath,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(documentPath);

        try
        {
            var allDocs = await _documentRepository.GetAllAsync(cancellationToken).ConfigureAwait(false);
            var doc = allDocs.FirstOrDefault(d => d.SourcePath == documentPath);

            if (doc == null)
            {
                _logger.LogWarning("Document not found for removal: {Path}", documentPath);
                return false;
            }

            // Delete embeddings and index entries
            await _vectorStore.DeleteTopicAndChunksAsync(doc.DocId, cancellationToken).ConfigureAwait(false);

            // Delete document record
            await _documentRepository.DeleteAsync(doc.DocId, cancellationToken).ConfigureAwait(false);

            _logger.LogInformation("Removed document from index: {Path}", documentPath);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing document: {Path}", documentPath);
            return false;
        }
    }

    /// <summary>
    /// Generates a document ID from the file path.
    /// </summary>
    private static string GenerateDocumentId(string filePath)
    {
        // Use hash of the canonical path as ID
        var canonicalPath = Path.GetFullPath(filePath);
        using var sha256 = SHA256.Create();
        var hash = sha256.ComputeHash(System.Text.Encoding.UTF8.GetBytes(canonicalPath));
        return Convert.ToHexString(hash).ToLower()[..16]; // First 16 chars of hex
    }

    /// <summary>
    /// Computes SHA256 hash of a file.
    /// </summary>
    private static async Task<string> ComputeFileHashAsync(string filePath, CancellationToken cancellationToken)
    {
        using var sha256 = SHA256.Create();
        using var stream = File.OpenRead(filePath);
        var hash = await sha256.ComputeHashAsync(stream, cancellationToken).ConfigureAwait(false);
        return Convert.ToHexString(hash);
    }

    /// <summary>
    /// Counts tokens in text using the configured tokenizer.
    /// </summary>
    private int CountTokens(string text)
    {
        // Placeholder: approximate word count
        return text.Split(new[] { ' ', '\t', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries).Length;
    }

    private async Task UpsertDocumentWithStatusAsync(
        string documentPath,
        string status,
        string? errorMessage,
        CancellationToken cancellationToken)
    {
        var fileInfo = new FileInfo(documentPath);
        var nowUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        string fileHash;
        if (fileInfo.Exists)
        {
            fileHash = await ComputeFileHashAsync(documentPath, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            fileHash = Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(documentPath)));
        }

        var existingDocs = await _documentRepository.GetAllAsync(cancellationToken).ConfigureAwait(false) ?? [];
        var existing = existingDocs.FirstOrDefault(doc => string.Equals(doc.SourcePath, documentPath, StringComparison.OrdinalIgnoreCase));
        var docId = existing?.DocId ?? GenerateDocumentId(documentPath);

        var entity = new Document
        {
            DocId = docId,
            SourcePath = documentPath,
            FileHash = fileHash,
            Format = Path.GetExtension(documentPath).TrimStart('.').ToLowerInvariant(),
            SizeBytes = fileInfo.Exists ? fileInfo.Length : 0,
            LastModified = fileInfo.Exists ? fileInfo.LastWriteTimeUtc.ToFileTimeUtc() : DateTime.UtcNow.ToFileTimeUtc(),
            Status = status,
            CreatedAt = existing?.CreatedAt ?? nowUnix,
            MetadataJson = BuildMetadataJson(
                warningCount: 0,
                errorMessage: errorMessage,
                isSensitive: false,
                isShareable: true,
                machineLocation: Environment.MachineName,
                lastIndexedAtUnix: status == "indexed" ? nowUnix : (long?)null)
        };

        if (existing == null)
        {
            await _documentRepository.AddAsync(entity, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            await _documentRepository.UpdateAsync(entity, cancellationToken).ConfigureAwait(false);
        }
    }

    private static string BuildMetadataJson(
        int warningCount,
        string? errorMessage,
        bool isSensitive,
        bool isShareable,
        string machineLocation,
        long? lastIndexedAtUnix)
    {
        var metadata = new Dictionary<string, object?>
        {
            ["warningCount"] = warningCount,
            ["errorMessage"] = errorMessage,
            ["isSensitive"] = isSensitive,
            ["isShareable"] = isShareable,
            ["machineLocation"] = machineLocation,
            ["lastIndexedAt"] = lastIndexedAtUnix
        };

        return JsonSerializer.Serialize(metadata);
    }
}
