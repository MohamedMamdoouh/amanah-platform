using Amanah.Api.Services.Auth;

namespace Amanah.Api.Tests.Utilities;

public class PhoneNormalizerTests
{
    [Theory]
    [InlineData("01012345678", "+201012345678")]
    [InlineData("+201012345678", "+201012345678")]
    [InlineData("201012345678", "+201012345678")]
    [InlineData("010-1234-5678", "+201012345678")]
    [InlineData("٠١٠١٢٣٤٥٦٧٨", "+201012345678")]
    public void TryNormalize_accepts_valid_egyptian_mobile_formats(string input, string expected)
    {
        var succeeded = PhoneNormalizer.TryNormalize(input, out var normalized);

        Assert.True(succeeded);
        Assert.Equal(expected, normalized);
    }

    [Theory]
    [InlineData("12345")]
    [InlineData("02012345678")]
    [InlineData("")]
    public void TryNormalize_rejects_invalid_phone_numbers(string input)
    {
        var succeeded = PhoneNormalizer.TryNormalize(input, out var normalized);

        Assert.False(succeeded);
        Assert.Equal(string.Empty, normalized);
    }
}
