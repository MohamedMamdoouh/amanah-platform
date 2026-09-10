using Amanah.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Amanah.Api.Utilities.Reports;

public static class ReportSearchQueryExtensions
{
    /// <summary>
    /// Each term adds another nested <c>Where</c>, and EF Core translates that chain with one
    /// recursion level per call. An unbounded chain overflows the stack while the query is being
    /// compiled, which cannot be caught and takes the whole process down.
    /// </summary>
    public const int MaxSearchTerms = 24;

    public static IQueryable<Report> WhereMatchesAllSearchTerms(
        this IQueryable<Report> query,
        IEnumerable<string> terms)
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
}
