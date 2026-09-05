using Amanah.Api.Utilities.Common;

namespace Amanah.Api.Utilities.Reports;

public static class SearchTextBuilder
{
    public static string Build(
        string title,
        string description,
        string? areaText,
        IEnumerable<string> categoryFieldValues)
    {
        var parts = new List<string>();

        if (!string.IsNullOrWhiteSpace(title))
        {
            parts.Add(title);
        }

        if (!string.IsNullOrWhiteSpace(description))
        {
            parts.Add(description);
        }

        foreach (var value in categoryFieldValues)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                parts.Add(value);
            }
        }

        if (!string.IsNullOrWhiteSpace(areaText))
        {
            parts.Add(areaText);
        }

        if (parts.Count == 0)
        {
            return string.Empty;
        }

        return ArabicNormalizer.NormalizeForSearch(string.Join(' ', parts));
    }
}
