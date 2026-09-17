using Amanah.Api.Data;
using Amanah.Api.Data.Entities;
using Amanah.Api.Models.Errors;
using Amanah.Api.Options;
using Amanah.Api.Services.Claims;
using Amanah.Api.Services.Notifications;
using Amanah.Api.Services.Storage;
using Amanah.Api.Utilities.Notifications;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Amanah.Api.Services.Lifecycle;

public sealed class ReportLifecycleService(
    AppDbContext dbContext,
    IBucketStorage bucketStorage,
    ClaimCleanupService claimCleanupService,
    TimeProvider timeProvider,
    IOptions<LifecycleOptions> lifecycleOptions)
{
    private const int SecondsPerDay = 86_400;

    private const string DefaultWithdrawReason = "Report withdrawn";

    public const string ExpiredWithdrawReason = "_expired_";

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
                NotificationTypes.ClaimClosedReportUnavailable,
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

    public async Task<int> ProcessListingExpiryWarningsAsync(CancellationToken cancellationToken = default)
    {
        var now = timeProvider.GetUtcNow();
        var options = lifecycleOptions.Value;
        var reports = await dbContext.Reports
            .Where(report =>
                report.Status == ReportStatus.Published
                && !report.ExpiryWarningSent)
            .ToListAsync(cancellationToken);

        var warningsSent = 0;
        foreach (var report in reports)
        {
            if (!HasReachedPublishedDayThreshold(report, options.ListingExpiryWarningDays, now))
            {
                continue;
            }

            dbContext.Notifications.Add(new Notification
            {
                Id = Guid.NewGuid(),
                UserId = report.ReporterId,
                Type = NotificationTypes.ReportExpiringSoon,
                PayloadJson = new NotificationPayload(
                    NotificationTypes.ReportExpiringSoon,
                    now,
                    DeepLink: $"/my/reports/{report.Id}",
                    ReportId: report.Id).ToJson(),
                IsRead = false,
                CreatedAt = now,
            });

            report.ExpiryWarningSent = true;
            report.UpdatedAt = now;
            warningsSent++;
        }

        if (warningsSent > 0)
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        return warningsSent;
    }

    public async Task<int> ProcessListingAutoExpiryAsync(CancellationToken cancellationToken = default)
    {
        var now = timeProvider.GetUtcNow();
        var options = lifecycleOptions.Value;
        var reports = await dbContext.Reports
            .Where(report => report.Status == ReportStatus.Published)
            .ToListAsync(cancellationToken);

        var expiredCount = 0;
        foreach (var report in reports)
        {
            if (!HasReachedPublishedDayThreshold(report, options.ListingExpiryDays, now))
            {
                continue;
            }

            await ExpireReportAsync(report, now, cancellationToken);
            expiredCount++;
        }

        return expiredCount;
    }

    private async Task ExpireReportAsync(
        Report report,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        await ClosePendingClaimsAsync(
            report.Id,
            ExpiredWithdrawReason,
            NotificationTypes.ReportExpired,
            cancellationToken);

        report.Status = ReportStatus.Withdrawn;
        report.WithdrawalReason = ExpiredWithdrawReason;
        report.UpdatedAt = now;

        dbContext.Notifications.Add(new Notification
        {
            Id = Guid.NewGuid(),
            UserId = report.ReporterId,
            Type = NotificationTypes.ReportExpired,
            PayloadJson = new NotificationPayload(
                NotificationTypes.ReportExpired,
                now,
                DeepLink: $"/my/reports/{report.Id}",
                ReportId: report.Id,
                ReasonCode: ExpiredWithdrawReason).ToJson(),
            IsRead = false,
            CreatedAt = now,
        });

        var storageKeys = RemoveReportPhotos(report);
        await dbContext.SaveChangesAsync(cancellationToken);
        await DeleteReportPhotoStorageAsync(storageKeys, cancellationToken);
    }

    private bool HasReachedPublishedDayThreshold(
        Report report,
        int thresholdDays,
        DateTimeOffset now) =>
        GetCumulativePublishedSeconds(report, now) >= thresholdDays * SecondsPerDay;

    private async Task<int> ClosePendingClaimsAsync(
        Guid reportId,
        string reason,
        string notificationType,
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
        var photoKeys = new List<string>();
        foreach (var claim in pendingClaims)
        {
            claim.Status = ClaimStatus.Withdrawn;
            claim.ReviewedAt = now;
            claim.ReviewerDecision = ClosedReviewerDecision;
            claim.DecisionReason = reason;
            claim.CountsAsFailure = false;
            photoKeys.AddRange(claimCleanupService.ClearClaimPhoto(claim));

            dbContext.Notifications.Add(new Notification
            {
                Id = Guid.NewGuid(),
                UserId = claim.ClaimantId,
                Type = notificationType,
                PayloadJson = new NotificationPayload(
                    notificationType,
                    now,
                    DeepLink: report.Type switch
                    {
                        ReportType.Lost => $"/lost/{report.Id}",
                        ReportType.Found => $"/found/{report.Id}",
                        _ => $"/reports/{report.Id}",
                    },
                    ReportId: report.Id,
                    ReasonCode: reason).ToJson(),
                IsRead = false,
                CreatedAt = now,
            });
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        await claimCleanupService.DeleteClaimPhotoStorageAsync(photoKeys, cancellationToken);
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
