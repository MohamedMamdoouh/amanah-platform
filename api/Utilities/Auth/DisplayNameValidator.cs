using Amanah.Api.Utilities.Common;

namespace Amanah.Api.Utilities.Auth;

public static class DisplayNameValidator
{
    private const int MinLength = 3;
    private const int MaxLength = 40;

    public static bool IsValid(string? displayName)
    {
        if (string.IsNullOrWhiteSpace(displayName))
        {
            return false;
        }

        var normalized = Normalize(displayName);
        if (normalized.Length is < MinLength or > MaxLength)
        {
            return false;
        }

        foreach (var character in normalized)
        {
            if (IsAllowedCharacter(character))
            {
                continue;
            }

            return false;
        }

        return true;
    }

    public static string Normalize(string displayName) => TextNormalizer.Normalize(displayName);

    private static bool IsAllowedCharacter(char character) =>
        char.IsLetter(character)
        || char.IsDigit(character)
        || character is ' ' or '-' or '_' or '.';
}
