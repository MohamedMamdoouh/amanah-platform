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
    StorageDeletionEnqueueService storageDeletionEnqueueService,
    ClaimCleanupService claimCleanupService,
    TimeProvider timeProvider,
    IOptions<LifecycleOptions> lifecycleOptions)
{
    private const int SecondsPerDay = 86_400;

    private const string DefaultWithdrawReason = "Report withdrawn";

    public const string ExpiredWithdrawReason = "_expired_";

    public const string AdminTakedownReason = "_admin_takedown_";

    public const string BanCleanupWithdrawReason = "_ban_cleanup_";

    public const string ClosedReviewerDecision = "closed";

    public Task LockReportRowForUpdateAsync(
        Guid reportId,
        CancellationToken cancellationToken = default) =>
        dbContext.Database.ExecuteSqlAsync(
            $"SELECT 1 FROM reports WHERE \"Id\" = {reportId} FOR UPDATE",
            cancellationToken);

    private async Task<ReportStatus?> ReadLockedReportStatusAsync(
        Report report,
        CancellationToken cancellationToken)
    {
        var databaseValues = await dbContext.Entry(report).GetDatabaseValuesAsync(cancellationToken);
        if (databaseValues is null)
        {
            return null;
        }

        return databaseValues.GetValue<ReportStatus>(nameof(Report.Status));
    }

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

        var reportId = report.Id;
        var withdrawalReason = reason;
        var now = timeProvider.GetUtcNow();

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        var result = await WithdrawPublishedOrPendingReviewCoreAsync(
            reportId,
            withdrawalReason,
            now,
            cancellationToken);

        if (!result.IsSuccess)
        {
            await transaction.RollbackAsync(cancellationToken);
            return result;
        }

        await transaction.CommitAsync(cancellationToken);
        return result;
    }

    public async Task<Result> WithdrawInTransactionAsync(
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

        return await WithdrawPublishedOrPendingReviewCoreAsync(
            report.Id,
            reason,
            timeProvider.GetUtcNow(),
            cancellationToken);
    }

    private async Task<Result> WithdrawPublishedOrPendingReviewCoreAsync(
        Guid reportId,
        string? withdrawalReason,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        await LockReportRowForUpdateAsync(reportId, cancellationToken);

        var reportStatus = await dbContext.Reports
            .Where(existingReport => existingReport.Id == reportId)
            .Select(existingReport => existingReport.Status)
            .SingleOrDefaultAsync(cancellationToken);

        if (reportStatus == ReportStatus.Published)
        {
            await ClosePendingClaimsAsync(
                reportId,
                withdrawalReason ?? DefaultWithdrawReason,
                NotificationTypes.ClaimClosedReportUnavailable,
                cancellationToken);
        }

        var transitionRows = await dbContext.Reports
            .Where(existingReport =>
                existingReport.Id == reportId
                && (existingReport.Status == ReportStatus.PendingReview
                    || existingReport.Status == ReportStatus.Published))
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(existingReport => existingReport.Status, ReportStatus.Withdrawn)
                    .SetProperty(existingReport => existingReport.WithdrawalReason, withdrawalReason)
                    .SetProperty(existingReport => existingReport.UpdatedAt, now),
                cancellationToken);

        if (transitionRows == 0)
        {
            return ResultError.Conflict("Only pending or published reports can be withdrawn.");
        }

        var report = await dbContext.Reports
            .Include(existingReport => existingReport.Photos)
            .SingleAsync(existingReport => existingReport.Id == reportId, cancellationToken);

        var storageKeys = RemoveReportPhotos(report);
        await storageDeletionEnqueueService.EnqueueAsync(
            storageKeys,
            StorageDeletionSource.ReportWithdraw,
            cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Result.Ok();
    }

    public async Task<Result> WithdrawForBanCleanupAsync(
        Report report,
        CancellationToken cancellationToken = default)
    {
        if (report.Status is not ReportStatus.PendingReview
            and not ReportStatus.Published
            and not ReportStatus.ClaimInProgress)
        {
            return ResultError.Conflict("Only active reports can be withdrawn during ban cleanup.");
        }

        await LockReportRowForUpdateAsync(report.Id, cancellationToken);

        // The caller loaded this report before the row lock. A concurrent approve can
        // commit ClaimInProgress while this transaction waits, and writing the stale
        // status would withdraw the report while leaving that claim Approved.
        var currentStatus = await ReadLockedReportStatusAsync(report, cancellationToken);
        if (currentStatus is null)
        {
            return ResultError.Conflict("Only active reports can be withdrawn during ban cleanup.");
        }

        if (report.Status != ReportStatus.ClaimInProgress
            && currentStatus == ReportStatus.ClaimInProgress)
        {
            return ResultError.Conflict(
                "A claim was approved while the ban was in progress. Please try again.");
        }

        if (report.Status != ReportStatus.ClaimInProgress
            && currentStatus is not ReportStatus.PendingReview and not ReportStatus.Published)
        {
            return Result.Ok();
        }

        if (currentStatus == ReportStatus.Published)
        {
            await ClosePendingClaimsAsync(
                report.Id,
                BanCleanupWithdrawReason,
                NotificationTypes.ClaimClosedReportUnavailable,
                cancellationToken);
        }

        report.Status = ReportStatus.Withdrawn;
        report.WithdrawalReason = BanCleanupWithdrawReason;
        report.UpdatedAt = timeProvider.GetUtcNow();

        var storageKeys = RemoveReportPhotos(report);
        await storageDeletionEnqueueService.EnqueueAsync(
            storageKeys,
            StorageDeletionSource.ReportWithdraw,
            cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Result.Ok();
    }

    public Task ClosePendingClaimsForAdminTakedownAsync(
        Guid reportId,
        CancellationToken cancellationToken = default) =>
        ClosePendingClaimsAsync(
            reportId,
            AdminTakedownReason,
            NotificationTypes.ClaimClosedReportUnavailable,
            cancellationToken);

    public async Task FinalizeAdminTakedownPhotosAsync(
        Guid reportId,
        CancellationToken cancellationToken = default)
    {
        var report = await dbContext.Reports
            .Include(existingReport => existingReport.Photos)
            .SingleAsync(existingReport => existingReport.Id == reportId, cancellationToken);

        var storageKeys = RemoveReportPhotos(report);
        await storageDeletionEnqueueService.EnqueueAsync(
            storageKeys,
            StorageDeletionSource.ReportWithdraw,
            cancellationToken);
    }

    public async Task FinalizeResolvedReportPhotosAsync(
        Guid reportId,
        CancellationToken cancellationToken = default)
    {
        var report = await dbContext.Reports
            .Include(existingReport => existingReport.Photos)
            .SingleAsync(existingReport => existingReport.Id == reportId, cancellationToken);

        var storageKeys = RemoveReportPhotos(report);
        await storageDeletionEnqueueService.EnqueueAsync(
            storageKeys,
            StorageDeletionSource.ReportWithdraw,
            cancellationToken);
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
        var reportId = report.Id;

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        await LockReportRowForUpdateAsync(reportId, cancellationToken);

        await ClosePendingClaimsAsync(
            reportId,
            ExpiredWithdrawReason,
            NotificationTypes.ReportExpired,
            cancellationToken);

        var transitionRows = await dbContext.Reports
            .Where(existingReport =>
                existingReport.Id == reportId
                && existingReport.Status == ReportStatus.Published)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(existingReport => existingReport.Status, ReportStatus.Withdrawn)
                    .SetProperty(existingReport => existingReport.WithdrawalReason, ExpiredWithdrawReason)
                    .SetProperty(existingReport => existingReport.UpdatedAt, now),
                cancellationToken);

        if (transitionRows == 0)
        {
            await transaction.RollbackAsync(cancellationToken);
            return;
        }

        report = await dbContext.Reports
            .Include(existingReport => existingReport.Photos)
            .SingleAsync(existingReport => existingReport.Id == reportId, cancellationToken);

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
        await storageDeletionEnqueueService.EnqueueAsync(
            storageKeys,
            StorageDeletionSource.ReportWithdraw,
            cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
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

        await claimCleanupService.EnqueueClaimPhotoStorageAsync(photoKeys, cancellationToken);
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
