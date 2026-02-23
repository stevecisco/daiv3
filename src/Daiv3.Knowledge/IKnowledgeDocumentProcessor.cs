namespace Daiv3.Knowledge;

/// <summary>
/// Options for document processing configuration.
/// </summary>
public class DocumentProcessingOptions
{
    /// <summary>
    /// Whether to skip processing of unchanged documents (by file hash).
    /// Default: true.
    /// </summary>
    public bool SkipUnchangedDocuments { get; set; } = true;

    /// <summary>
    /// Target number of tokens per chunk. 
    /// Default: 400.
    /// </summary>
    public int TargetChunkTokens { get; set; } = 400;

    /// <summary>
    /// Number of tokens to overlap between chunks.
    /// Default: 50.
    /// </summary>
    public int ChunkOverlapTokens { get; set; } = 50;
}

/// <summary>
/// Represents the result of processing a single document through the knowledge ingestion pipeline.
/// </summary>
public class DocumentProcessingResult
{
    /// <summary>
    /// The document ID.
    /// </summary>
    public string DocumentId { get; set; } = string.Empty;

    /// <summary>
    /// Whether the document was successfully processed.
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Error message if processing failed.
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Number of chunks created from this document.
    /// </summary>
    public int ChunkCount { get; set; }

    /// <summary>
    /// Number of tokens in the topic summary.
    /// </summary>
    public int SummaryTokens { get; set; }

    /// <summary>
    /// Total tokens processed from document (excluding chunks).
    /// </summary>
    public int TotalTokens { get; set; }

    /// <summary>
    /// Time taken to process the document in milliseconds.
    /// </summary>
    public long ProcessingTimeMs { get; set; }
}

/// <summary>
/// Orchestrates the knowledge ingest pipeline from document to indexed embeddings.
/// Handles text extraction, chunking, embedding generation, and storage.
/// </summary>
public interface IKnowledgeDocumentProcessor
{
    /// <summary>
    /// Processes a single document file through the full ingestion pipeline.
    /// Extracts text, generates topic summary, chunks content, generates embeddings, and stores in SQLite.
    /// </summary>
    /// <param name="documentPath">Full path to the document file.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Result with processing status and metrics.</returns>
    Task<DocumentProcessingResult> ProcessDocumentAsync(
        string documentPath,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Processes multiple documents in batch.
    /// Reports progress through the progress callback.
    /// </summary>
    /// <param name="documentPaths">Collection of file paths to process.</param>
    /// <param name="progressCallback">Optional callback for progress reporting.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of processing results for each document.</returns>
    Task<IReadOnlyList<DocumentProcessingResult>> ProcessDocumentsAsync(
        IEnumerable<string> documentPaths,
        IProgress<(int Processed, int Total, string CurrentFile)>? progressCallback = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Re-processes a document that already exists in the index.
    /// Detects if unchanged (by file hash) and skips if configured.
    /// </summary>
    /// <param name="documentPath">Path to the document to update.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Result indicating whether document was updated or skipped.</returns>
    Task<DocumentProcessingResult> UpdateDocumentAsync(
        string documentPath,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes a document from the index entirely.
    /// Deletes its topic and chunk entries.
    /// </summary>
    /// <param name="documentPath">Path of the document to remove.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if document was found and deleted, false if not found.</returns>
    Task<bool> RemoveDocumentAsync(
        string documentPath,
        CancellationToken cancellationToken = default);
}
