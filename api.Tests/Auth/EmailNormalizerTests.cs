using Amanah.Api.Services.Auth;

namespace Amanah.Api.Tests.Auth;

public class EmailNormalizerTests
{
    [Theory]
    [InlineData("user@example.com", "user@example.com")]
    [InlineData("USER@EXAMPLE.COM", "user@example.com")]
    [InlineData("new.user@example.com", "new.user@example.com")]
    [InlineData("  user@example.com  ", "user@example.com")]
    [InlineData("user+tag@example.co.uk", "user+tag@example.co.uk")]
    [InlineData("user_name@mail.example.com", "user_name@mail.example.com")]
    public void TryNormalize_accepts_valid_emails(string input, string expected)
    {
        var succeeded = EmailNormalizer.TryNormalize(input, out var normalized);

        Assert.True(succeeded);
        Assert.Equal(expected, normalized);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not-an-email")]
    [InlineData("@example.com")]
    [InlineData("user@")]
    [InlineData("user@@example.com")]
    [InlineData("\"Name\" <user@example.com>")]
    [InlineData("user@example")]
    [InlineData("user@localhost")]
    public void TryNormalize_rejects_invalid_emails(string input)
    {
        var succeeded = EmailNormalizer.TryNormalize(input, out var normalized);

        Assert.False(succeeded);
        Assert.Equal(string.Empty, normalized);
    }

    [Fact]
    public void TryNormalize_rejects_overlong_address()
    {
        var local = new string('a', 250);
        var input = $"{local}@example.com";

        var succeeded = EmailNormalizer.TryNormalize(input, out var normalized);

        Assert.False(succeeded);
        Assert.Equal(string.Empty, normalized);
    }
}
