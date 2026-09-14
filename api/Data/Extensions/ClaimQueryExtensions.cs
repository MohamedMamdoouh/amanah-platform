using Amanah.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Amanah.Api.Data.Extensions;

public static class ClaimQueryExtensions
{
    public static IQueryable<Claim> WithReportInclude(this IQueryable<Claim> query) =>
        query.Include(claim => claim.Report);

    public static IQueryable<Claim> WithResolutionDetailIncludes(this IQueryable<Claim> query) =>
        query
            .Include(claim => claim.Report)
            .ThenInclude(report => report.Resolution)
            .Include(claim => claim.ChatThread);

    public static IQueryable<Claim> WithClaimDetailIncludes(this IQueryable<Claim> query) =>
        query
            .Include(claim => claim.Report)
            .ThenInclude(report => report.Reporter)
            .Include(claim => claim.Report)
            .ThenInclude(report => report.Resolution)
            .Include(claim => claim.Claimant)
            .Include(claim => claim.ChatThread);
}
