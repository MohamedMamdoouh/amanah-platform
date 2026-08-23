using Amanah.Api.Utilities;

namespace Amanah.Api.Tests.Utilities;

public class TextNormalizerTests
{
    [Fact]
    public void Normalize_trims_and_collapses_internal_whitespace()
    {
        Assert.Equal("hello world", TextNormalizer.Normalize("  hello   world  "));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Normalize_returns_empty_string_for_null_or_whitespace(string? input)
    {
        Assert.Equal(string.Empty, TextNormalizer.Normalize(input));
    }
}
