using Daiv3.Knowledge.DocProc;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Daiv3.Knowledge.Tests.DocProc;

public class TokenizerProviderTests
{
    [Fact]
    public void GetTokenizer_CachesInstance()
    {
        var options = Options.Create(new TokenizationOptions
        {
            EncodingName = "r50k_base"
        });

        var provider = new TokenizerProvider(NullLogger<TokenizerProvider>.Instance, options);

        var first = provider.GetTokenizer();
        var second = provider.GetTokenizer();

        Assert.Same(first, second);
    }
}
