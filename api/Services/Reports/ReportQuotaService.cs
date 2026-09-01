using Amanah.Api.Data;
using Amanah.Api.Data.Entities;
using Amanah.Api.Utilities;
using Microsoft.EntityFrameworkCore;

namespace Amanah.Api.Services.Reports;

public sealed class ReportQuotaService(AppDbContext dbContext, TimeProvider timeProvider) : IReportQuotaService
{
    private const int DailyQuotaLimit = 3;
    private const int OpenReportCap = 5;

    private static readonly ReportStatus[] OpenCapStatuses =
    [
        ReportStatus.PendingReview,
        ReportStatus.Published,
        ReportStatus.ClaimInProgress,
    ];

    public async Task<QuotaCheckResult> CheckNewSubmissionAsync(
        Guid userId,
        bool isResubmission = false,
        CancellationToken cancellationToken = default)
    {
        if (isResubmission)
        {
            return new QuotaCheckResult(QuotaFailureKind.None);
        }

        var dailyCount = await CountReportsCreatedTodayAsync(userId, cancellationToken);
        if (dailyCount >= DailyQuotaLimit)
        {
            return new QuotaCheckResult(
                QuotaFailureKind.DailyQuota,
                SecondsUntilNextCairoMidnight());
        }

        var openCount = await CountOpenReportsAsync(userId, cancellationToken);
        if (openCount >= OpenReportCap)
        {
            return new QuotaCheckResult(QuotaFailureKind.OpenCap);
        }

        return new QuotaCheckResult(QuotaFailureKind.None);
    }

    private async Task<int> CountReportsCreatedTodayAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var dayStart = CairoTime.CairoDayStartUtc(now);
        var nextDayStart = dayStart.AddDays(1);

        return await dbContext.Reports
            .AsNoTracking()
            .CountAsync(
                report => report.ReporterId == userId
                    && report.CreatedAt >= dayStart
                    && report.CreatedAt < nextDayStart,
                cancellationToken);
    }

    private async Task<int> CountOpenReportsAsync(
        Guid userId,
        CancellationToken cancellationToken) =>
        await dbContext.Reports
            .AsNoTracking()
            .CountAsync(
                report => report.ReporterId == userId
                    && OpenCapStatuses.Contains(report.Status),
                cancellationToken);

    private int SecondsUntilNextCairoMidnight()
    {
        var now = timeProvider.GetUtcNow();
        var dayStart = CairoTime.CairoDayStartUtc(now);
        var nextDayStart = dayStart.AddDays(1);
        var seconds = (int)Math.Ceiling((nextDayStart - now).TotalSeconds);

        return Math.Max(seconds, 1);
    }
}
