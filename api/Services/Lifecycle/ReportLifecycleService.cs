using Amanah.Api.Data;
using Amanah.Api.Data.Entities;
using Amanah.Api.Models.Errors;
using Amanah.Api.Services.Notifications;
using Amanah.Api.Services.Storage;
using Amanah.Api.Utilities.Notifications;
using Microsoft.EntityFrameworkCore;

namespace Amanah.Api.Services.Lifecycle;

public sealed class ReportLifecycleService(
    AppDbContext dbContext,
    IBucketStorage bucketStorage,
    TimeProvider timeProvider) : IReportLifecycleService
{
    private const string DefaultWithdrawReason = "Report withdrawn";

    public const string ClosedReviewerDecision = "closed";

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
            await ClosePendingClaimsAsync(
                report.Id,
                reason ?? DefaultWithdrawReason,
                cancellationToken);
        }

        report.Status = ReportStatus.Withdrawn;
        report.WithdrawalReason = reason;
        report.UpdatedAt = timeProvider.GetUtcNow();

        var storageKeys = RemoveReportPhotos(report);
        await dbContext.SaveChangesAsync(cancellationToken);
        await DeleteReportPhotoStorageAsync(storageKeys, cancellationToken);

        return Result.Ok();
    }

    private async Task<int> ClosePendingClaimsAsync(
        Guid reportId,
        string reason,
        CancellationToken cancellationToken = default)
    {
        var pendingClaims = await dbContext.Claims
            .Include(claim => claim.Report)
            .Where(claim =>
                claim.ReportId == reportId
                && claim.Status == ClaimStatus.Pending)
            .ToListAsync(cancellationToken);

        if (pendingClaims.Count == 0)
        {
            return 0;
        }

        var now = timeProvider.GetUtcNow();
        var report = pendingClaims[0].Report;

        foreach (var claim in pendingClaims)
        {
            claim.Status = ClaimStatus.Withdrawn;
            claim.ReviewedAt = now;
            claim.ReviewerDecision = ClosedReviewerDecision;
            claim.DecisionReason = reason;
            claim.CountsAsFailure = false;

            dbContext.Notifications.Add(NotificationEntityBuilder.Create(
                claim.ClaimantId,
                NotificationTypes.ClaimClosedReportUnavailable,
                new NotificationPayload(
                    NotificationTypes.ClaimClosedReportUnavailable,
                    now,
                    DeepLink: ReportDeepLinkBuilder.ForPublicReport(report),
                    ReportId: report.Id,
                    ReasonCode: reason),
                now));
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return pendingClaims.Count;
    }

    private IReadOnlyList<string> RemoveReportPhotos(Report report)
    {
        var photos = report.Photos.Count > 0
            ? report.Photos.ToList()
            : dbContext.ReportPhotos
                .Where(photo => photo.ReportId == report.Id)
                .ToList();

        if (photos.Count == 0)
        {
            return [];
        }

        var storageKeys = CollectStorageKeys(photos);
        dbContext.ReportPhotos.RemoveRange(photos);
        report.Photos.Clear();

        return storageKeys;
    }

    private Task DeleteReportPhotoStorageAsync(
        IReadOnlyList<string> storageKeys,
        CancellationToken cancellationToken = default)
    {
        return storageKeys.Count == 0
            ? Task.CompletedTask
            : bucketStorage.DeleteManyAsync(storageKeys, cancellationToken);
    }

    private static IReadOnlyList<string> CollectStorageKeys(IEnumerable<ReportPhoto> photos)
    {
        return photos
            .SelectMany(photo => new[] { photo.StorageKey, photo.ThumbnailStorageKey })
            .Where(key => !string.IsNullOrWhiteSpace(key))
            .Select(key => key!)
            .ToList();
    }

    private static int ElapsedSeconds(DateTimeOffset startedAt, DateTimeOffset now)
    {
        var elapsed = (int)(now - startedAt).TotalSeconds;
        return Math.Max(0, elapsed);
    }
}
