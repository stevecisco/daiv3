namespace Daiv3.Knowledge.Embedding;

/// <summary>
/// Pooling strategy for transformer token outputs.
/// </summary>
public enum EmbeddingPoolingStrategy
{
    MeanPooling = 0,
    ClsToken = 1,
    None = 2
}
