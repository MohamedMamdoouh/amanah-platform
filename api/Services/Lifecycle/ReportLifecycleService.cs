using Amanah.Api.Data;
using Amanah.Api.Data.Entities;
using Amanah.Api.Models.Errors;
using Amanah.Api.Services.Claims;

namespace Amanah.Api.Services.Lifecycle;

public sealed class ReportLifecycleService(
    AppDbContext dbContext,
    IClaimCleanupService claimCleanupService,
    IRetentionService retentionService,
    TimeProvider timeProvider) : IReportLifecycleService
{
    private const string DefaultWithdrawReason = "Report withdrawn";

    public void InitializePublishedTimer(Report report, DateTimeOffset now)
    {
        report.PublishedAt = now;
        report.PublishedTimerResumedAt = now;
        report.PublishedSecondsElapsed = 0;
    }

    public void PausePublishedTimer(Report report, DateTimeOffset now)
    {
        if (report.PublishedTimerResumedAt is null)
        {
            return;
        }

        var elapsedSinceResume = ElapsedSeconds(report.PublishedTimerResumedAt.Value, now);
        if (elapsedSinceResume > 0)
        {
            report.PublishedSecondsElapsed += elapsedSinceResume;
        }

        report.PublishedTimerResumedAt = null;
    }

    public void ResumePublishedTimer(Report report, DateTimeOffset now)
    {
        report.PublishedTimerResumedAt = now;
    }

    public int GetCumulativePublishedSeconds(Report report, DateTimeOffset now)
    {
        if (report.PublishedTimerResumedAt is null)
        {
            return report.PublishedSecondsElapsed;
        }

        return report.PublishedSecondsElapsed + ElapsedSeconds(report.PublishedTimerResumedAt.Value, now);
    }

    public async Task<Result> WithdrawAsync(
        Report report,
        string? reason,
        CancellationToken cancellationToken = default)
    {
        if (report.Status == ReportStatus.ClaimInProgress)
        {
            return ResultError.Conflict(
                "Cancel the approved claim before withdrawing this report.");
        }

        if (report.Status is not ReportStatus.PendingReview and not ReportStatus.Published)
        {
            return ResultError.Conflict("Only pending or published reports can be withdrawn.");
        }

        if (report.Status == ReportStatus.Published)
        {
            await claimCleanupService.ClosePendingClaimsAsync(
                report.Id,
                reason ?? DefaultWithdrawReason,
                cancellationToken);
        }

        report.Status = ReportStatus.Withdrawn;
        report.WithdrawalReason = reason;
        report.UpdatedAt = timeProvider.GetUtcNow();

        var storageKeys = retentionService.RemoveReportPhotos(report);
        await dbContext.SaveChangesAsync(cancellationToken);
        await retentionService.DeleteReportPhotoStorageAsync(storageKeys, cancellationToken);

        return Result.Ok();
    }

    private static int ElapsedSeconds(DateTimeOffset startedAt, DateTimeOffset now)
    {
        var elapsed = (int)(now - startedAt).TotalSeconds;
        return Math.Max(0, elapsed);
    }
}
