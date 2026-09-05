namespace Amanah.Api.Utilities.Common;

public static class TextNormalizer
{
    public static string Normalize(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return string.Empty;
        }

        return string.Join(' ', input.Trim().Split(
            (char[]?)null,
            StringSplitOptions.RemoveEmptyEntries));
    }
}
