using Amanah.Api.Data;
using Amanah.Api.Data.Entities;
using Amanah.Api.Services.Claims;
using Amanah.Api.Services.Enforcement;
using Amanah.Api.Services.Lifecycle;
using Amanah.Api.Services.Storage;
using Amanah.Api.Services.Uploads;
using Microsoft.EntityFrameworkCore;

namespace Amanah.Api.Services.Admin;

/// <summary>
/// One-shot cleanup of Admin participation data (reports, claims, flags, chats)
/// so Admin accounts are moderator-only. Idempotent: no-op when nothing remains.
/// </summary>
public sealed class AdminParticipationPurgeService(
    AppDbContext dbContext,
    ReportLifecycleService reportLifecycleService,
    ApprovedClaimCancellation approvedClaimCancellation,
    ClaimCleanupService claimCleanupService,
    StorageDeletionEnqueueService storageDeletionEnqueueService,
    TimeProvider timeProvider,
    ILogger<AdminParticipationPurgeService> logger)
{
    public async Task PurgeAsync(CancellationToken cancellationToken = default)
    {
        var adminIds = await dbContext.Users
            .AsNoTracking()
            .Where(user => user.Role == UserRole.Admin)
            .Select(user => user.Id)
            .ToListAsync(cancellationToken);

        if (adminIds.Count == 0)
        {
            return;
        }

        var hasParticipation = await dbContext.Reports
                .AsNoTracking()
                .AnyAsync(report => adminIds.Contains(report.ReporterId), cancellationToken)
            || await dbContext.Claims
                .AsNoTracking()
                .AnyAsync(claim => adminIds.Contains(claim.ClaimantId), cancellationToken)
            || await dbContext.AbuseReports
                .AsNoTracking()
                .AnyAsync(
                    abuseReport => adminIds.Contains(abuseReport.AbuseReporterId),
                    cancellationToken);

        if (!hasParticipation)
        {
            return;
        }

        logger.LogInformation(
            "Purging Admin participation data for {AdminCount} admin account(s).",
            adminIds.Count);

        foreach (var adminId in adminIds)
        {
            await PurgeAdminAsync(adminId, cancellationToken);
        }

        logger.LogInformation("Admin participation purge completed.");
    }

    private async Task PurgeAdminAsync(Guid adminId, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        await CancelApprovedClaimsAsClaimantAsync(adminId, now, cancellationToken);
        await CancelApprovedClaimsAsReporterAsync(adminId, cancellationToken);
        await WithdrawPendingClaimsAsync(adminId, now, cancellationToken);

        var storageKeys = new List<string>();
        storageKeys.AddRange(await HardDeleteAdminOwnedReportsAsync(adminId, cancellationToken));
        storageKeys.AddRange(await HardDeleteAdminClaimsOnOthersAsync(adminId, cancellationToken));

        await dbContext.AbuseReports
            .Where(abuseReport => abuseReport.AbuseReporterId == adminId)
            .ExecuteDeleteAsync(cancellationToken);

        await dbContext.Notifications
            .Where(notification => notification.UserId == adminId)
            .ExecuteDeleteAsync(cancellationToken);

        if (storageKeys.Count > 0)
        {
            await storageDeletionEnqueueService.EnqueueAsync(
                storageKeys,
                StorageDeletionSource.ReportRetention,
                cancellationToken);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    private async Task CancelApprovedClaimsAsClaimantAsync(
        Guid adminId,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var claims = await dbContext.Claims
            .Include(claim => claim.Report)
            .ThenInclude(report => report.Photos)
            .Include(claim => claim.Report)
            .ThenInclude(report => report.Resolution)
            .Include(claim => claim.ChatThread)
            .Where(claim =>
                claim.ClaimantId == adminId
                && claim.Status == ClaimStatus.Approved
                && claim.Report.Status == ReportStatus.ClaimInProgress)
            .ToListAsync(cancellationToken);

        foreach (var claim in claims)
        {
            await reportLifecycleService.LockReportRowForUpdateAsync(claim.Report.Id, cancellationToken);

            var lockedStatus = await dbContext.Reports
                .AsNoTracking()
                .Where(report => report.Id == claim.ReportId)
                .Select(report => (ReportStatus?)report.Status)
                .SingleOrDefaultAsync(cancellationToken);

            if (lockedStatus != ReportStatus.ClaimInProgress)
            {
                continue;
            }

            var cancelResult = await approvedClaimCancellation.CancelForEnforcementAsync(
                claim.Report,
                cancellationToken);
            if (!cancelResult.IsSuccess)
            {
                throw new InvalidOperationException(
                    $"Failed to cancel admin claim {claim.Id}: {cancelResult.Error!.Message}");
            }

            claim.Report.Status = ReportStatus.Published;
            reportLifecycleService.ResumePublishedTimer(claim.Report, now);
            claim.Report.UpdatedAt = now;
        }
    }

    private async Task CancelApprovedClaimsAsReporterAsync(
        Guid adminId,
        CancellationToken cancellationToken)
    {
        var claims = await dbContext.Claims
            .Include(claim => claim.Report)
            .ThenInclude(report => report.Photos)
            .Include(claim => claim.Report)
            .ThenInclude(report => report.Resolution)
            .Include(claim => claim.ChatThread)
            .Where(claim =>
                claim.Report.ReporterId == adminId
                && claim.Status == ClaimStatus.Approved
                && claim.Report.Status == ReportStatus.ClaimInProgress)
            .ToListAsync(cancellationToken);

        foreach (var claim in claims)
        {
            await reportLifecycleService.LockReportRowForUpdateAsync(claim.Report.Id, cancellationToken);

            var lockedStatus = await dbContext.Reports
                .AsNoTracking()
                .Where(report => report.Id == claim.ReportId)
                .Select(report => (ReportStatus?)report.Status)
                .SingleOrDefaultAsync(cancellationToken);

            if (lockedStatus != ReportStatus.ClaimInProgress)
            {
                continue;
            }

            var cancelResult = await approvedClaimCancellation.CancelForEnforcementAsync(
                claim.Report,
                cancellationToken);
            if (!cancelResult.IsSuccess)
            {
                throw new InvalidOperationException(
                    $"Failed to cancel claim on admin report {claim.ReportId}: {cancelResult.Error!.Message}");
            }
        }
    }

    private async Task WithdrawPendingClaimsAsync(
        Guid adminId,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var pendingClaims = await dbContext.Claims
            .Where(claim => claim.ClaimantId == adminId && claim.Status == ClaimStatus.Pending)
            .ToListAsync(cancellationToken);

        if (pendingClaims.Count == 0)
        {
            return;
        }

        var photoKeys = new List<string>();
        foreach (var claim in pendingClaims)
        {
            claim.Status = ClaimStatus.Withdrawn;
            claim.ReviewedAt = now;
            claim.CountsAsFailure = false;
            photoKeys.AddRange(claimCleanupService.ClearClaimPhoto(claim));
        }

        await claimCleanupService.EnqueueClaimPhotoStorageAsync(photoKeys, cancellationToken);
    }

    private async Task<IReadOnlyList<string>> HardDeleteAdminOwnedReportsAsync(
        Guid adminId,
        CancellationToken cancellationToken)
    {
        var reports = await dbContext.Reports
            .Include(report => report.Photos)
            .Include(report => report.Claims)
            .ThenInclude(claim => claim.ChatThread)
            .Where(report => report.ReporterId == adminId)
            .ToListAsync(cancellationToken);

        if (reports.Count == 0)
        {
            return [];
        }

        var storageKeys = new List<string>();
        var reportIds = reports.Select(report => report.Id).ToList();
        var claimIds = reports.SelectMany(report => report.Claims).Select(claim => claim.Id).ToList();
        var threadIds = reports
            .SelectMany(report => report.Claims)
            .Where(claim => claim.ChatThread is not null)
            .Select(claim => claim.ChatThread!.Id)
            .ToList();

        foreach (var report in reports)
        {
            storageKeys.AddRange(CollectReportPhotoKeys(report.Photos));
            storageKeys.AddRange(CollectClaimPhotoKeys(report.Claims));
        }

        if (threadIds.Count > 0)
        {
            var attachments = await dbContext.ChatAttachments
                .AsNoTracking()
                .Where(attachment => threadIds.Contains(attachment.ChatThreadId))
                .ToListAsync(cancellationToken);
            storageKeys.AddRange(CollectChatAttachmentKeys(attachments));

            await dbContext.ChatAttachments
                .Where(attachment => threadIds.Contains(attachment.ChatThreadId))
                .ExecuteDeleteAsync(cancellationToken);
            await dbContext.Messages
                .Where(message => threadIds.Contains(message.ChatThreadId))
                .ExecuteDeleteAsync(cancellationToken);
            await dbContext.ChatThreads
                .Where(thread => threadIds.Contains(thread.Id))
                .ExecuteDeleteAsync(cancellationToken);
        }

        await dbContext.AbuseReports
            .Where(abuseReport => reportIds.Contains(abuseReport.ReportId))
            .ExecuteDeleteAsync(cancellationToken);

        await dbContext.Resolutions
            .Where(resolution => reportIds.Contains(resolution.ReportId))
            .ExecuteDeleteAsync(cancellationToken);

        if (claimIds.Count > 0)
        {
            await dbContext.Claims
                .Where(claim => claimIds.Contains(claim.Id))
                .ExecuteDeleteAsync(cancellationToken);
        }

        await dbContext.Reports
            .Where(report => reportIds.Contains(report.Id))
            .ExecuteDeleteAsync(cancellationToken);

        foreach (var entry in dbContext.ChangeTracker.Entries()
                     .Where(entry =>
                         (entry.Entity is Report report && reportIds.Contains(report.Id))
                         || (entry.Entity is Claim claim && claimIds.Contains(claim.Id))
                         || (entry.Entity is ChatThread thread && threadIds.Contains(thread.Id))
                         || (entry.Entity is ReportPhoto photo && reportIds.Contains(photo.ReportId)))
                     .ToList())
        {
            entry.State = EntityState.Detached;
        }

        return storageKeys;
    }

    private async Task<IReadOnlyList<string>> HardDeleteAdminClaimsOnOthersAsync(
        Guid adminId,
        CancellationToken cancellationToken)
    {
        var claims = await dbContext.Claims
            .Include(claim => claim.ChatThread)
            .Where(claim => claim.ClaimantId == adminId)
            .ToListAsync(cancellationToken);

        if (claims.Count == 0)
        {
            return [];
        }

        var storageKeys = new List<string>();
        storageKeys.AddRange(CollectClaimPhotoKeys(claims));

        var claimIds = claims.Select(claim => claim.Id).ToList();
        var threadIds = claims
            .Where(claim => claim.ChatThread is not null)
            .Select(claim => claim.ChatThread!.Id)
            .ToList();

        if (threadIds.Count > 0)
        {
            var attachments = await dbContext.ChatAttachments
                .AsNoTracking()
                .Where(attachment => threadIds.Contains(attachment.ChatThreadId))
                .ToListAsync(cancellationToken);
            storageKeys.AddRange(CollectChatAttachmentKeys(attachments));

            await dbContext.ChatAttachments
                .Where(attachment => threadIds.Contains(attachment.ChatThreadId))
                .ExecuteDeleteAsync(cancellationToken);
            await dbContext.Messages
                .Where(message => threadIds.Contains(message.ChatThreadId))
                .ExecuteDeleteAsync(cancellationToken);
            await dbContext.ChatThreads
                .Where(thread => threadIds.Contains(thread.Id))
                .ExecuteDeleteAsync(cancellationToken);
        }

        await dbContext.Claims
            .Where(claim => claimIds.Contains(claim.Id))
            .ExecuteDeleteAsync(cancellationToken);

        foreach (var entry in dbContext.ChangeTracker.Entries()
                     .Where(entry =>
                         (entry.Entity is Claim claim && claimIds.Contains(claim.Id))
                         || (entry.Entity is ChatThread thread && threadIds.Contains(thread.Id)))
                     .ToList())
        {
            entry.State = EntityState.Detached;
        }

        return storageKeys;
    }

    private static IReadOnlyList<string> CollectReportPhotoKeys(IEnumerable<ReportPhoto> photos) =>
        photos
            .SelectMany(photo => new[] { photo.StorageKey, photo.ThumbnailStorageKey })
            .Where(key => !string.IsNullOrWhiteSpace(key))
            .Select(key => key!)
            .ToList();

    private static IReadOnlyList<string> CollectClaimPhotoKeys(IEnumerable<Claim> claims) =>
        claims
            .Where(claim => !string.IsNullOrWhiteSpace(claim.PhotoStorageKey))
            .SelectMany(claim => new[]
            {
                claim.PhotoStorageKey!,
                ClaimPhotoStorageKeys.ThumbnailForOriginal(claim.PhotoStorageKey!),
            })
            .ToList();

    private static IReadOnlyList<string> CollectChatAttachmentKeys(IEnumerable<ChatAttachment> attachments) =>
        attachments
            .SelectMany(attachment => new[] { attachment.StorageKey, attachment.ThumbnailStorageKey })
            .Where(key => !string.IsNullOrWhiteSpace(key))
            .Select(key => key!)
            .ToList();
}
