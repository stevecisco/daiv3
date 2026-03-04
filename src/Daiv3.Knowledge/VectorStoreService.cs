using Daiv3.Persistence;
using Daiv3.Persistence.Entities;
using Daiv3.Persistence.Repositories;
using Microsoft.Extensions.Logging;

namespace Daiv3.Knowledge;

/// <summary>
/// Implementation of vector storage using SQLite repositories for embeddings and index entries.
/// </summary>
public class VectorStoreService : IVectorStoreService
{
    private readonly IDatabaseContext _databaseContext;
    private readonly TopicIndexRepository _topicIndexRepository;
    private readonly ChunkIndexRepository _chunkIndexRepository;
    private readonly DocumentRepository _documentRepository;
    private readonly ILogger<VectorStoreService> _logger;

    public VectorStoreService(
        IDatabaseContext databaseContext,
        TopicIndexRepository topicIndexRepository,
        ChunkIndexRepository chunkIndexRepository,
        DocumentRepository documentRepository,
        ILogger<VectorStoreService> logger)
    {
        _databaseContext = databaseContext ?? throw new ArgumentNullException(nameof(databaseContext));
        _topicIndexRepository = topicIndexRepository ?? throw new ArgumentNullException(nameof(topicIndexRepository));
        _chunkIndexRepository = chunkIndexRepository ?? throw new ArgumentNullException(nameof(chunkIndexRepository));
        _documentRepository = documentRepository ?? throw new ArgumentNullException(nameof(documentRepository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<string> StoreTopicIndexAsync(
        string docId,
        string summaryText,
        float[] embedding,
        string sourcePath,
        string fileHash,
        string? metadata = null,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(docId);
        ArgumentNullException.ThrowIfNull(summaryText);
        ArgumentNullException.ThrowIfNull(sourcePath);
        ArgumentNullException.ThrowIfNull(fileHash);
        ArgumentNullException.ThrowIfNull(embedding);

        // Ensure document exists (required by foreign key constraint)
        await EnsureDocumentExistsAsync(docId, sourcePath, fileHash, ct).ConfigureAwait(false);

        var embeddingBlob = ConvertEmbeddingToBytes(embedding);

        var topicIndex = new TopicIndex
        {
            DocId = docId,
            SummaryText = summaryText,
            EmbeddingBlob = embeddingBlob,
            EmbeddingDimensions = embedding.Length,
            SourcePath = sourcePath,
            FileHash = fileHash,
            IngestedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            MetadataJson = metadata
        };

        var existingTopic = await _topicIndexRepository.GetByIdAsync(docId, ct).ConfigureAwait(false);

        if (existingTopic is null)
        {
            await _topicIndexRepository.AddAsync(topicIndex, ct).ConfigureAwait(false);
            _logger.LogInformation(
                "Stored topic index for document {DocId}, {Dimensions} dimensions, {SummaryLength} chars",
                docId,
                embedding.Length,
                summaryText.Length);
        }
        else
        {
            await _topicIndexRepository.UpdateAsync(topicIndex, ct).ConfigureAwait(false);
            _logger.LogInformation(
                "Updated topic index for document {DocId}, {Dimensions} dimensions, {SummaryLength} chars",
                docId,
                embedding.Length,
                summaryText.Length);
        }

        return docId;
    }

    public async Task<string> StoreChunkAsync(
        string docId,
        string chunkText,
        float[] embedding,
        int chunkOrder,
        string? topicTags = null,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(docId);
        ArgumentNullException.ThrowIfNull(chunkText);
        ArgumentNullException.ThrowIfNull(embedding);

        if (chunkOrder < 0)
            throw new ArgumentException("Chunk order must be non-negative", nameof(chunkOrder));

        // Ensure document exists (required by foreign key constraint)
        await EnsureDocumentExistsAsync(docId, sourcePath: "unknown", fileHash: "unknown", ct).ConfigureAwait(false);

        var embeddingBlob = ConvertEmbeddingToBytes(embedding);
        var chunkId = $"{docId}_chunk_{chunkOrder}";

        var chunkIndex = new ChunkIndex
        {
            ChunkId = chunkId,
            DocId = docId,
            ChunkText = chunkText,
            EmbeddingBlob = embeddingBlob,
            EmbeddingDimensions = embedding.Length,
            ChunkOrder = chunkOrder,
            TopicTags = topicTags,
            CreatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
        };

        var result = await _chunkIndexRepository.AddAsync(chunkIndex, ct).ConfigureAwait(false);
        _logger.LogDebug(
            "Stored chunk {ChunkId} for document {DocId}, order {ChunkOrder}, {Dimensions} dimensions",
            chunkId,
            docId,
            chunkOrder,
            embedding.Length);

        return result;
    }

    public async Task<TopicIndex?> GetTopicIndexAsync(string docId, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(docId);

        return await _topicIndexRepository.GetByIdAsync(docId, ct).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<TopicIndex>> GetAllTopicIndicesAsync(CancellationToken ct = default)
    {
        return await _topicIndexRepository.GetAllAsync(ct).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<ChunkIndex>> GetChunksByDocumentAsync(string docId, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(docId);

        return await _chunkIndexRepository.GetByDocumentIdAsync(docId, ct).ConfigureAwait(false);
    }

    public async Task<ChunkIndex?> GetChunkAsync(string chunkId, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(chunkId);

        return await _chunkIndexRepository.GetByIdAsync(chunkId, ct).ConfigureAwait(false);
    }

    public async Task DeleteTopicAndChunksAsync(string docId, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(docId);

        _logger.LogInformation("Deleting topic and all chunks for document {DocId}", docId);

        // Delete chunks first (due to foreign key constraints)
        var chunkCount = await _chunkIndexRepository.DeleteByDocumentIdAsync(docId, ct).ConfigureAwait(false);

        // Delete topic index
        await _topicIndexRepository.DeleteAsync(docId, ct).ConfigureAwait(false);

        _logger.LogInformation("Deleted topic and {ChunkCount} chunks for document {DocId}", chunkCount, docId);
    }

    public async Task<int> GetTopicIndexCountAsync(CancellationToken ct = default)
    {
        return await _topicIndexRepository.GetCountAsync(ct).ConfigureAwait(false);
    }

    public async Task<int> GetChunkIndexCountAsync(CancellationToken ct = default)
    {
        return await _chunkIndexRepository.GetCountAsync(ct).ConfigureAwait(false);
    }

    public async Task<bool> TopicIndexExistsAsync(string docId, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(docId);

        var topic = await _topicIndexRepository.GetByIdAsync(docId, ct).ConfigureAwait(false);
        return topic is not null;
    }

    /// <summary>
    /// Converts a float array to a byte array for storage.
    /// </summary>
    private static byte[] ConvertEmbeddingToBytes(float[] embedding)
    {
        var bytes = new byte[embedding.Length * sizeof(float)];
        System.Buffer.BlockCopy(embedding, 0, bytes, 0, bytes.Length);
        return bytes;
    }

    /// <summary>
    /// Ensures a document record exists before storing embeddings (required by foreign key constraints).
    /// Creates a minimal placeholder document if it doesn't exist.
    /// </summary>
    private async Task EnsureDocumentExistsAsync(string docId, string sourcePath, string fileHash, CancellationToken ct)
    {
        var existingDoc = await _documentRepository.GetByIdAsync(docId, ct).ConfigureAwait(false);

        if (existingDoc is null)
        {
            // Create minimal placeholder document to satisfy foreign key constraint
            var document = new Document
            {
                DocId = docId,
                SourcePath = sourcePath,
                FileHash = fileHash,
                Format = ".unknown",
                SizeBytes = 0,
                LastModified = DateTime.UtcNow.ToFileTimeUtc(),
                Status = "indexed",
                CreatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                MetadataJson = null
            };

            await _documentRepository.AddAsync(document, ct).ConfigureAwait(false);
            _logger.LogDebug("Created placeholder document record for {DocId}", docId);
        }
    }
}
