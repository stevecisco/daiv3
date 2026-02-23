namespace Daiv3.Knowledge.Embedding;

/// <summary>
/// Configuration for ONNX embedding inference.
/// </summary>
public sealed class EmbeddingOnnxOptions
{
    public string ModelPath { get; set; } = string.Empty;

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
    }

    public string GetExpandedModelPath()
    {
        var expanded = Environment.ExpandEnvironmentVariables(ModelPath);
        return Path.GetFullPath(expanded);
    }
}
