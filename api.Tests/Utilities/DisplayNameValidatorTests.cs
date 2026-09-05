using Amanah.Api.Utilities.Auth;

namespace Amanah.Api.Tests.Utilities;

public class DisplayNameValidatorTests
{
    [Fact]
    public void Valid_arabic_display_name_accepted()
    {
        Assert.True(DisplayNameValidator.IsValid("أحمد"));
    }

    [Fact]
    public void Valid_latin_display_name_with_allowed_punctuation_accepted()
    {
        Assert.True(DisplayNameValidator.IsValid("Ahmad_123"));
    }

    [Fact]
    public void Too_short_display_name_rejected()
    {
        Assert.False(DisplayNameValidator.IsValid("ab"));
    }

    [Fact]
    public void Disallowed_symbol_rejected()
    {
        Assert.False(DisplayNameValidator.IsValid("Ahmad@"));
    }
}
