using System.Text;

namespace Amanah.Api.Utilities;

public static class ArabicNormalizer
{
    public static string NormalizeForSearch(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return string.Empty;
        }

        var builder = new StringBuilder(input.Length);

        foreach (var character in input)
        {
            var normalized = NormalizeCharacter(character);
            if (normalized != '\0')
            {
                builder.Append(normalized);
            }
        }

        return TextNormalizer.Normalize(builder.ToString()).ToLowerInvariant();
    }

    public static string[] BuildSearchTerms(string query)
    {
        var normalized = NormalizeForSearch(query);

        if (normalized.Length == 0)
        {
            return [];
        }

        return normalized.Split(' ', StringSplitOptions.RemoveEmptyEntries);
    }

    private static char NormalizeCharacter(char character) => character switch
    {
        'أ' or 'إ' or 'آ' or 'ٱ' => 'ا',
        'ى' => 'ي',
        'ة' => 'ه',
        '\u0640' => '\0',
        _ when IsArabicDiacritic(character) => '\0',
        _ => character,
    };

    private static bool IsArabicDiacritic(char character) =>
        character is >= '\u064B' and <= '\u065F' or '\u0670';
}
