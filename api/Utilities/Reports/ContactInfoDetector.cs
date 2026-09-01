namespace Amanah.Api.Utilities.Reports;

public static class ContactInfoDetector
{
    private static readonly string[] SocialDomains =
    [
        "facebook.com",
        "instagram.com",
        "t.me",
        "telegram.me",
        "wa.me",
        "whatsapp.com",
    ];

    public const string ContactInfoMessage =
        "Contact information is not allowed in this field.";

    public static bool ContainsContactInfo(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var normalized = text.Trim();

        if (ContainsUrlLikeText(normalized) || ContainsSocialDomain(normalized))
        {
            return true;
        }

        return CountNormalizedDigits(normalized) >= 10;
    }

    private static bool ContainsUrlLikeText(string text)
    {
        var lower = text.ToLowerInvariant();
        return lower.Contains("http://", StringComparison.Ordinal)
            || lower.Contains("https://", StringComparison.Ordinal)
            || lower.Contains("www.", StringComparison.Ordinal);
    }

    private static bool ContainsSocialDomain(string text)
    {
        var lower = text.ToLowerInvariant();
        return SocialDomains.Any(domain => lower.Contains(domain, StringComparison.Ordinal));
    }

    private static int CountNormalizedDigits(string text)
    {
        var digitCount = 0;

        foreach (var character in text)
        {
            if (TryNormalizeDigit(character, out _))
            {
                digitCount++;
            }
        }

        return digitCount;
    }

    private static bool TryNormalizeDigit(char character, out char digit)
    {
        if (character is >= '0' and <= '9')
        {
            digit = character;
            return true;
        }

        if (character is >= '\u0660' and <= '\u0669')
        {
            digit = (char)('0' + (character - '\u0660'));
            return true;
        }

        digit = default;
        return false;
    }
}
