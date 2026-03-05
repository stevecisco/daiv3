using Microsoft.ML.OnnxRuntime;

namespace Daiv3.Knowledge.Embedding;

public interface IOnnxInferenceSessionProvider : IAsyncDisposable, IDisposable
{
    Task<InferenceSession> GetSessionAsync(CancellationToken ct = default);

    OnnxExecutionProvider? SelectedProvider { get; }
}
