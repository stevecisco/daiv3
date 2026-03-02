using Daiv3.Knowledge.Embedding;
using Daiv3.Orchestration.Interfaces;
using Daiv3.Orchestration.Models;
using Daiv3.Persistence.Entities;
using Daiv3.Persistence.Repositories;
using Microsoft.Extensions.Logging;

namespace Daiv3.Orchestration;

/// <summary>
/// Service for creating and managing learning records per LM-REQ-001.
/// Handles learning creation triggers from various sources:
/// - User feedback
/// - Self-correction
/// - Compilation errors
/// - Tool failures
/// - Knowledge conflicts
/// - Explicit calls
/// </summary>
public class LearningService : ILearningService
{
    private readonly ILogger<LearningService> _logger;
    private readonly LearningRepository _learningRepository;
    private readonly IEmbeddingGenerator _embeddingGenerator;

    public LearningService(
        ILogger<LearningService> logger,
        LearningRepository learningRepository,
        IEmbeddingGenerator embeddingGenerator)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _learningRepository = learningRepository ?? throw new ArgumentNullException(nameof(learningRepository));
        _embeddingGenerator = embeddingGenerator ?? throw new ArgumentNullException(nameof(embeddingGenerator));
    }

    /// <inheritdoc/>
    public async Task<Learning> CreateLearningAsync(LearningTriggerContext context, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        
        _logger.LogInformation(
            "Creating learning from trigger type {TriggerType}: {Title}",
            context.TriggerType, context.Title);

        try
        {
            // Generate embedding for semantic retrieval
            float[] embedding = await GenerateEmbeddingAsync(context.Description, ct).ConfigureAwait(false);

            // Create learning entity
            var learning = new Learning
            {
                LearningId = Guid.NewGuid().ToString(),
                Title = context.Title,
                Description = context.Description,
                TriggerType = context.TriggerType,
                Scope = context.Scope,
                SourceAgent = context.SourceAgent,
                SourceTaskId = context.SourceTaskId,
                EmbeddingBlob = ConvertToByteArray(embedding),
                EmbeddingDimensions = embedding.Length > 0 ? embedding.Length : null,
                Tags = context.Tags,
                Confidence = context.Confidence,
                Status = "Active",
                TimesApplied = 0,
                CreatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                UpdatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                CreatedBy = context.CreatedBy
            };

            // Persist to database
            await _learningRepository.AddAsync(learning, ct).ConfigureAwait(false);

            _logger.LogInformation(
                "Learning created successfully. ID: {LearningId}, TriggerType: {TriggerType}, Scope: {Scope}, Confidence: {Confidence:P}",
                learning.LearningId, learning.TriggerType, learning.Scope, learning.Confidence);

            return learning;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to create learning from trigger type {TriggerType}: {Title}",
                context.TriggerType, context.Title);
            throw new InvalidOperationException(
                $"Failed to create learning: {ex.Message}", ex);
        }
    }

    /// <inheritdoc/>
    public Task<Learning> CreateSelfCorrectionLearningAsync(
        SelfCorrectionTriggerContext context, 
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        
        _logger.LogDebug(
            "Creating self-correction learning. Failed iteration: {FailedIteration}, Success iteration: {SuccessIteration}",
            context.FailedIteration, context.SuccessIteration);

        return CreateLearningAsync(context, ct);
    }

    /// <inheritdoc/>
    public Task<Learning> CreateUserFeedbackLearningAsync(
        UserFeedbackTriggerContext context, 
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        
        _logger.LogDebug(
            "Creating user feedback learning: {Title}",
            context.Title);

        return CreateLearningAsync(context, ct);
    }

    /// <inheritdoc/>
    public Task<Learning> CreateCompilationErrorLearningAsync(
        CompilationErrorTriggerContext context, 
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        
        _logger.LogDebug(
            "Creating compilation error learning: {Title}. Language: {Language}",
            context.Title, context.Language ?? "unknown");

        return CreateLearningAsync(context, ct);
    }

    /// <inheritdoc/>
    public Task<Learning> CreateToolFailureLearningAsync(
        ToolFailureTriggerContext context, 
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        
        _logger.LogDebug(
            "Creating tool failure learning: {Title}. Tool: {ToolName}",
            context.Title, context.ToolName ?? "unknown");

        return CreateLearningAsync(context, ct);
    }

    /// <inheritdoc/>
    public Task<Learning> CreateKnowledgeConflictLearningAsync(
        KnowledgeConflictTriggerContext context, 
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        
        _logger.LogDebug(
            "Creating knowledge conflict learning: {Title}. Source: {ConflictSource}",
            context.Title, context.ConflictSource ?? "unknown");

        return CreateLearningAsync(context, ct);
    }

    /// <inheritdoc/>
    public Task<Learning> CreateExplicitLearningAsync(
        ExplicitTriggerContext context, 
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        
        _logger.LogDebug(
            "Creating explicit learning: {Title}. Agent: {SourceAgent}",
            context.Title, context.SourceAgent ?? "unknown");

        return CreateLearningAsync(context, ct);
    }

    /// <summary>
    /// Generates embedding for learning description.
    /// Handles exceptions and provides fallback behavior.
    /// </summary>
    private async Task<float[]> GenerateEmbeddingAsync(string text, CancellationToken ct)
    {
        try
        {
            return await _embeddingGenerator.GenerateEmbeddingAsync(text, ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Failed to generate embedding for learning. Creating learning without embedding.");
            
            // Return empty array if embedding generation fails
            // Learning will still be created but won't be semantically searchable
            return Array.Empty<float>();
        }
    }

    /// <summary>
    /// Converts float array to byte array for database storage.
    /// </summary>
    private static byte[] ConvertToByteArray(float[] embedding)
    {
        if (embedding.Length == 0)
        {
            return Array.Empty<byte>();
        }

        var bytes = new byte[embedding.Length * sizeof(float)];
        Buffer.BlockCopy(embedding, 0, bytes, 0, bytes.Length);
        return bytes;
    }
}
