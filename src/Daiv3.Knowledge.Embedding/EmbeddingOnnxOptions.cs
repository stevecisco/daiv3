namespace Daiv3.Knowledge.Embedding;

/// <summary>
/// Configuration for ONNX embedding inference.
/// </summary>
public sealed class EmbeddingOnnxOptions
{
    public string ModelPath { get; set; } = string.Empty;

    public string InputIdsTensorName { get; set; } = "input_ids";

    public string AttentionMaskTensorName { get; set; } = "attention_mask";

    public string TokenTypeIdsTensorName { get; set; } = "token_type_ids";

    public string OutputTensorName { get; set; } = "last_hidden_state";

    public EmbeddingPoolingStrategy PoolingStrategy { get; set; } = EmbeddingPoolingStrategy.MeanPooling;

    public bool NormalizeEmbeddings { get; set; } = true;

    public OnnxExecutionProviderPreference ExecutionProviderPreference { get; set; } =
        OnnxExecutionProviderPreference.Auto;

    public int? IntraOpNumThreads { get; set; }

    public int? InterOpNumThreads { get; set; }

    public bool EnableMemoryPattern { get; set; } = true;

    public bool EnableCpuMemArena { get; set; } = true;

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(ModelPath))
        {
            throw new InvalidOperationException("Embedding model path must be configured.");
        }

        if (string.IsNullOrWhiteSpace(InputIdsTensorName))
        {
            throw new InvalidOperationException("InputIds tensor name must be configured.");
        }

        if (string.IsNullOrWhiteSpace(AttentionMaskTensorName))
        {
            throw new InvalidOperationException("AttentionMask tensor name must be configured.");
        }

        if (string.IsNullOrWhiteSpace(OutputTensorName))
        {
            throw new InvalidOperationException("Output tensor name must be configured.");
        }
    }

    public string GetExpandedModelPath()
    {
        var expanded = Environment.ExpandEnvironmentVariables(ModelPath);
        return Path.GetFullPath(expanded);
    }
}
