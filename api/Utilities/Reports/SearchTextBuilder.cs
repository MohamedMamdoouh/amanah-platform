using Amanah.Api.Data.Entities;
using Amanah.Api.Utilities.Common;
using Microsoft.EntityFrameworkCore;

namespace Amanah.Api.Utilities.Reports;

public static class SearchTextBuilder
{
    private const int MaxSearchTerms = 24;

    public static IQueryable<Report> FilterBySearchQuery(
        IQueryable<Report> query,
        string? rawQuery) =>
        FilterBySearchTerms(query, ArabicNormalizer.BuildSearchTerms(rawQuery ?? string.Empty));

    public static IQueryable<Report> FilterBySearchTerms(
        IQueryable<Report> query,
        string[] terms)
    {
        foreach (var term in terms.Take(MaxSearchTerms))
        {
            var pattern = $"%{term}%";
            query = query.Where(report =>
                report.NormalizedSearchText != null
                && EF.Functions.ILike(report.NormalizedSearchText, pattern));
        }

        return query;
    }

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
