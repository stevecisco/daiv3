using Microsoft.ML.OnnxRuntime.Tensors;

namespace Daiv3.Knowledge.Embedding;

/// <summary>
/// Input tensors for ONNX embedding inference.
/// </summary>
public sealed record EmbeddingModelInput(
    DenseTensor<long> InputIds,
    DenseTensor<long> AttentionMask,
    DenseTensor<long>? TokenTypeIds);
