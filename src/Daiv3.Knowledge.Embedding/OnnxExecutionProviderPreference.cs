namespace Daiv3.Knowledge.Embedding;

/// <summary>
/// Preferred execution provider for ONNX inference.
/// </summary>
public enum OnnxExecutionProviderPreference
{
    Auto = 0,
    DirectML = 1,
    Cpu = 2
}
