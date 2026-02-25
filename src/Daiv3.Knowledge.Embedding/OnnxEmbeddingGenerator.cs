using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.ML.OnnxRuntime.Tensors;

namespace Daiv3.Knowledge.Embedding;

public sealed class OnnxEmbeddingGenerator : IEmbeddingGenerator
{
    private readonly ILogger<OnnxEmbeddingGenerator> _logger;
    private readonly EmbeddingOnnxOptions _onnxOptions;
    private readonly EmbeddingTokenizationOptions _tokenizationOptions;
    private readonly IEmbeddingTokenizerProvider _tokenizerProvider;
    private readonly IOnnxEmbeddingModelRunner _modelRunner;

    public OnnxEmbeddingGenerator(
        ILogger<OnnxEmbeddingGenerator> logger,
        IOptions<EmbeddingOnnxOptions> onnxOptions,
        IOptions<EmbeddingTokenizationOptions> tokenizationOptions,
        IEmbeddingTokenizerProvider tokenizerProvider,
        IOnnxEmbeddingModelRunner modelRunner)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _onnxOptions = onnxOptions?.Value ?? throw new ArgumentNullException(nameof(onnxOptions));
        _tokenizationOptions = tokenizationOptions?.Value ?? throw new ArgumentNullException(nameof(tokenizationOptions));
        _tokenizerProvider = tokenizerProvider ?? throw new ArgumentNullException(nameof(tokenizerProvider));
        _modelRunner = modelRunner ?? throw new ArgumentNullException(nameof(modelRunner));
    }

    public async Task<float[]> GenerateEmbeddingAsync(string text, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            throw new ArgumentException("Text must be provided for embedding generation.", nameof(text));
        }

        _onnxOptions.Validate();
        _tokenizationOptions.Validate();
        ct.ThrowIfCancellationRequested();

        var tokenizer = _tokenizerProvider.GetEmbeddingTokenizer();
        var tokenIds = tokenizer.Tokenize(text);

        if (tokenIds.Length == 0)
        {
            throw new InvalidOperationException("Tokenizer produced no tokens for embedding generation.");
        }

        // Validate tokens are within model vocabulary
        if (!tokenizer.ValidateTokenIds(tokenIds))
        {
            _logger.LogError(
                "Tokenizer {TokenizerName} produced invalid token IDs. Vocabulary size: {VocabSize}",
                tokenizer.Name,
                tokenizer.VocabularySize);
            throw new InvalidOperationException(
                $"Tokenizer produced token IDs outside vocabulary bounds (vocab size: {tokenizer.VocabularySize})");
        }

        var tokenCount = Math.Min(tokenIds.Length, _tokenizationOptions.MaxTokens);
        var inputIds = new long[tokenCount];
        var attentionMask = new long[tokenCount];

        for (int i = 0; i < tokenCount; i++)
        {
            inputIds[i] = tokenIds[i];
            attentionMask[i] = 1;
        }

        var input = new EmbeddingModelInput(
            new DenseTensor<long>(inputIds, new[] { 1, tokenCount }),
            new DenseTensor<long>(attentionMask, new[] { 1, tokenCount }),
            CreateTokenTypeIds(tokenCount));

        var output = await _modelRunner.RunAsync(input, ct).ConfigureAwait(false);
        var embedding = ExtractEmbedding(output, attentionMask, _onnxOptions.PoolingStrategy);

        if (_onnxOptions.NormalizeEmbeddings)
        {
            NormalizeInPlace(embedding);
        }

        _logger.LogDebug(
            "Generated {Dimension}-dimensional embedding using {TokenizerName} ({TokenCount} tokens).",
            embedding.Length,
            tokenizer.Name,
            tokenCount);
        return embedding;
    }

    private DenseTensor<long>? CreateTokenTypeIds(int tokenCount)
    {
        // Always create token_type_ids (zeros) - many models require this input
        var tokenTypeIds = new long[tokenCount];
        return new DenseTensor<long>(tokenTypeIds, new[] { 1, tokenCount });
    }

    private static float[] ExtractEmbedding(
        DenseTensor<float> output,
        IReadOnlyList<long> attentionMask,
        EmbeddingPoolingStrategy poolingStrategy)
    {
        if (output.Rank == 1)
        {
            return output.ToArray();
        }

        if (output.Rank == 2)
        {
            return output.ToArray();
        }

        if (output.Rank != 3)
        {
            throw new InvalidOperationException("Unexpected ONNX output rank for embedding generation.");
        }

        var sequenceLength = output.Dimensions[1];
        var hiddenSize = output.Dimensions[2];
        var embedding = new float[hiddenSize];

        switch (poolingStrategy)
        {
            case EmbeddingPoolingStrategy.ClsToken:
                for (int j = 0; j < hiddenSize; j++)
                {
                    embedding[j] = output[0, 0, j];
                }
                return embedding;
            case EmbeddingPoolingStrategy.None:
                throw new InvalidOperationException("Pooling strategy None requires a 2D embedding output.");
        }

        long tokenTotal = 0;
        for (int i = 0; i < sequenceLength && i < attentionMask.Count; i++)
        {
            if (attentionMask[i] == 0)
            {
                continue;
            }

            tokenTotal++;
            for (int j = 0; j < hiddenSize; j++)
            {
                embedding[j] += output[0, i, j];
            }
        }

        if (tokenTotal == 0)
        {
            return embedding;
        }

        var divisor = 1.0f / tokenTotal;
        for (int j = 0; j < hiddenSize; j++)
        {
            embedding[j] *= divisor;
        }

        return embedding;
    }

    private static void NormalizeInPlace(float[] embedding)
    {
        if (embedding.Length == 0)
        {
            return;
        }

        double sumSquares = 0;
        for (int i = 0; i < embedding.Length; i++)
        {
            sumSquares += embedding[i] * embedding[i];
        }

        if (sumSquares <= 0)
        {
            return;
        }

        var scale = (float)(1.0 / Math.Sqrt(sumSquares));
        for (int i = 0; i < embedding.Length; i++)
        {
            embedding[i] *= scale;
        }
    }
}
