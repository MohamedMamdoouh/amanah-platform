using Amanah.Api.Data;
using Amanah.Api.Data.Entities;
using Amanah.Api.Models.Errors;
using Amanah.Api.Utilities.Abuse;
using Amanah.Api.Utilities.Common;
using Amanah.Contracts.Errors;
using Amanah.Contracts.Requests.Abuse;
using Amanah.Contracts.Responses.Abuse;
using Microsoft.EntityFrameworkCore;

namespace Amanah.Api.Services.Abuse;

public sealed class AbuseFlagService(AppDbContext dbContext, TimeProvider timeProvider)
{
    public async Task<Result<FlagListingResponse>> CreateAsync(
        Guid reportId,
        Guid abuseReporterId,
        FlagListingRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!AbuseFlagReasons.All.Contains(request.Reason))
        {
            return ResultError.BadRequest(
                "Flag reason is invalid.",
                ErrorCodes.AbuseInvalidReason);
        }

        var report = await dbContext.Reports
            .AsNoTracking()
            .SingleOrDefaultAsync(existingReport => existingReport.Id == reportId, cancellationToken);

        if (report is null)
        {
            return ResultError.NotFound("Report not found.");
        }

        if (report.ReporterId == abuseReporterId)
        {
            return ResultError.Conflict(
                "You cannot flag your own listing.",
                ErrorCodes.AbuseCannotFlagOwnListing);
        }

        if (report.Status is not ReportStatus.Published and not ReportStatus.ClaimInProgress)
        {
            return ResultError.Conflict(
                "This listing cannot be flagged.",
                ErrorCodes.AbuseListingNotFlaggable);
        }

        var hasOpenFlag = await dbContext.AbuseReports
            .AsNoTracking()
            .AnyAsync(
                abuseReport =>
                    abuseReport.ReportId == reportId
                    && abuseReport.AbuseReporterId == abuseReporterId
                    && abuseReport.Status == AbuseReportStatus.Open,
                cancellationToken);

        if (hasOpenFlag)
        {
            return ResultError.Conflict(
                "You already have an open flag on this listing.",
                ErrorCodes.AbuseDuplicateFlag);
        }

        var now = timeProvider.GetUtcNow();
        var note = string.IsNullOrWhiteSpace(request.Note)
            ? null
            : TextNormalizer.Normalize(request.Note);

        var abuseReport = new AbuseReport
        {
            Id = Guid.NewGuid(),
            AbuseReporterId = abuseReporterId,
            ReportId = reportId,
            Reason = request.Reason,
            Note = note,
            Status = AbuseReportStatus.Open,
            CreatedAt = now,
        };

        dbContext.AbuseReports.Add(abuseReport);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            return ResultError.Conflict(
                "You already have an open flag on this listing.",
                ErrorCodes.AbuseDuplicateFlag);
        }

        return Map(abuseReport);
    }

    public async Task<Result<FlagListingResponse>> GetOpenAsync(
        Guid reportId,
        Guid abuseReporterId,
        CancellationToken cancellationToken = default)
    {
        var abuseReport = await dbContext.AbuseReports
            .AsNoTracking()
            .SingleOrDefaultAsync(
                existingReport =>
                    existingReport.ReportId == reportId
                    && existingReport.AbuseReporterId == abuseReporterId
                    && existingReport.Status == AbuseReportStatus.Open,
                cancellationToken);

        if (abuseReport is null)
        {
            return ResultError.NotFound("No open flag found on this listing.");
        }

        return Map(abuseReport);
    }

    private static FlagListingResponse Map(AbuseReport abuseReport) =>
        new()
        {
            Id = abuseReport.Id,
            ReportId = abuseReport.ReportId,
            Reason = abuseReport.Reason,
            Note = abuseReport.Note,
            Status = abuseReport.Status switch
            {
                AbuseReportStatus.Open => "open",
                AbuseReportStatus.Resolved => "resolved",
                _ => abuseReport.Status.ToString().ToLowerInvariant(),
            },
            CreatedAt = abuseReport.CreatedAt,
        };
}
