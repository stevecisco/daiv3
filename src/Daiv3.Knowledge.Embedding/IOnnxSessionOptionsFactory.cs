using Microsoft.ML.OnnxRuntime;

namespace Daiv3.Knowledge.Embedding;

public interface IOnnxSessionOptionsFactory
{
    SessionOptions Create(out OnnxExecutionProvider selectedProvider);
}
