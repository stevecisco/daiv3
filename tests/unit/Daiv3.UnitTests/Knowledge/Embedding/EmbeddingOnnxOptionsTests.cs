using Daiv3.Knowledge.Embedding;
using Xunit;

namespace Daiv3.UnitTests.Knowledge.Embedding;

public class EmbeddingOnnxOptionsTests
{
    [Fact]
    public void Validate_ThrowsWhenModelPathMissing()
    {
        var options = new EmbeddingOnnxOptions { ModelPath = " " };

        var exception = Assert.Throws<InvalidOperationException>(() => options.Validate());

        Assert.Contains("model path", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void GetExpandedModelPath_ExpandsEnvironmentVariables()
    {
        var tempPath = Path.GetTempPath().TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var options = new EmbeddingOnnxOptions
        {
            ModelPath = Path.Combine("%TEMP%", "model.onnx")
        };

        var expanded = options.GetExpandedModelPath();

        Assert.Contains(tempPath, expanded, StringComparison.OrdinalIgnoreCase);
    }
}
