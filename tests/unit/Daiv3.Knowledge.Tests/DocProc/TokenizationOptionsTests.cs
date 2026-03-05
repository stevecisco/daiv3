using Daiv3.Knowledge.DocProc;
using Xunit;

namespace Daiv3.Knowledge.Tests.DocProc;

public class TokenizationOptionsTests
{
    [Fact]
    public void Validate_Defaults_NoThrow()
    {
        var options = new TokenizationOptions();

        options.Validate();
    }

    [Fact]
    public void Validate_EmptyEncoding_Throws()
    {
        var options = new TokenizationOptions
        {
            EncodingName = " "
        };

        Assert.Throws<InvalidOperationException>(() => options.Validate());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Validate_InvalidMaxTokens_Throws(int maxTokens)
    {
        var options = new TokenizationOptions
        {
            MaxTokensPerChunk = maxTokens
        };

        Assert.Throws<InvalidOperationException>(() => options.Validate());
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(400)]
    public void Validate_InvalidOverlap_Throws(int overlapTokens)
    {
        var options = new TokenizationOptions
        {
            MaxTokensPerChunk = 400,
            OverlapTokens = overlapTokens
        };

        Assert.Throws<InvalidOperationException>(() => options.Validate());
    }
}
