using Microsoft.ML.OnnxRuntime.Tensors;

namespace Daiv3.Knowledge.Embedding;

/// <summary>
/// Executes ONNX embedding inference.
/// </summary>
public interface IOnnxEmbeddingModelRunner
{
    Task<DenseTensor<float>> RunAsync(EmbeddingModelInput input, CancellationToken ct = default);
}
