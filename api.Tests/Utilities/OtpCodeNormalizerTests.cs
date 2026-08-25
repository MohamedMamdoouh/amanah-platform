using Amanah.Api.Services.Auth;

namespace Amanah.Api.Tests.Utilities;

public class OtpCodeNormalizerTests
{
    [Fact]
    public void Valid_ascii_digits_normalize()
    {
        Assert.True(OtpCodeNormalizer.TryNormalize("123456", out var code));
        Assert.Equal("123456", code);
    }

    [Fact]
    public void Arabic_indic_digits_normalize()
    {
        Assert.True(OtpCodeNormalizer.TryNormalize("١٢٣٤٥٦", out var code));
        Assert.Equal("123456", code);
    }

    [Fact]
    public void Too_short_code_rejected()
    {
        Assert.False(OtpCodeNormalizer.TryNormalize("12345", out _));
    }

    [Fact]
    public void Non_digit_character_rejected()
    {
        Assert.False(OtpCodeNormalizer.TryNormalize("12345a", out _));
    }
}
