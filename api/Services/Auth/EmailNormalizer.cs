using EmailValidation;

namespace Amanah.Api.Services.Auth;

public static class EmailNormalizer
{
    private const bool AllowTopLevelDomains = false;
    private const bool AllowInternational = false;

    public static bool TryNormalize(string input, out string normalizedEmail)
    {
        normalizedEmail = string.Empty;

        if (string.IsNullOrWhiteSpace(input))
        {
            return false;
        }

        var trimmed = input.Trim();
        if (trimmed.Length > 254)
        {
            return false;
        }

        if (!EmailValidator.Validate(trimmed, AllowTopLevelDomains, AllowInternational))
        {
            return false;
        }

        normalizedEmail = trimmed.ToLowerInvariant();
        return true;
    }
}
