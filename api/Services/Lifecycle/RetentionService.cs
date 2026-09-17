using Amanah.Api.Data;
using Amanah.Api.Data.Entities;
using Amanah.Api.Options;
using Amanah.Api.Services.Storage;
using Amanah.Api.Services.Uploads;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Amanah.Api.Services.Lifecycle;

public sealed class RetentionService(
    AppDbContext dbContext,
    IBucketStorage bucketStorage,
    TimeProvider timeProvider,
    IOptions<LifecycleOptions> lifecycleOptions)
{
    public async Task<int> ProcessRejectedReportCleanupAsync(CancellationToken cancellationToken = default)
    {
        var now = timeProvider.GetUtcNow();
        var threshold = now.AddDays(-lifecycleOptions.Value.RetentionDays);

        var expiredReportIds = await dbContext.ModerationActions
            .AsNoTracking()
            .Where(action => action.Decision == ModerationDecision.Reject && action.ReportId != null)
            .GroupBy(action => action.ReportId)
            .Where(group => group.Max(action => action.CreatedAt) <= threshold)
            .Select(group => group.Key!.Value)
            .ToListAsync(cancellationToken);

        if (expiredReportIds.Count == 0)
        {
            return 0;
        }

        var reports = await dbContext.Reports
            .Include(report => report.Photos)
            .Include(report => report.Claims)
            .Where(report =>
                report.Status == ReportStatus.Rejected
                && expiredReportIds.Contains(report.Id))
            .ToListAsync(cancellationToken);

        if (reports.Count == 0)
        {
            return 0;
        }

        var storageKeys = new List<string>();
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        foreach (var report in reports)
        {
            storageKeys.AddRange(CollectReportPhotoKeys(report.Photos));
            storageKeys.AddRange(CollectClaimPhotoKeys(report.Claims));

            await dbContext.AbuseReports
                .Where(abuseReport => abuseReport.ReportId == report.Id)
                .ExecuteDeleteAsync(cancellationToken);

            await dbContext.Resolutions
                .Where(resolution => resolution.ReportId == report.Id)
                .ExecuteDeleteAsync(cancellationToken);

            dbContext.Claims.RemoveRange(report.Claims);
            dbContext.Reports.Remove(report);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        await bucketStorage.DeleteManyAsync(storageKeys, cancellationToken);

        return reports.Count;
    }

    public async Task<int> ProcessChatRetentionAsync(CancellationToken cancellationToken = default)
    {
        var now = timeProvider.GetUtcNow();
        var threshold = now.AddDays(-lifecycleOptions.Value.RetentionDays);

        var threads = await dbContext.ChatThreads
            .Where(thread => thread.ReadOnlyAt != null && thread.ReadOnlyAt <= threshold)
            .ToListAsync(cancellationToken);

        if (threads.Count == 0)
        {
            return 0;
        }

        var threadIds = threads.Select(thread => thread.Id).ToList();
        var attachments = await dbContext.ChatAttachments
            .Where(attachment => threadIds.Contains(attachment.ChatThreadId))
            .ToListAsync(cancellationToken);

        var storageKeys = CollectChatAttachmentKeys(attachments);

        dbContext.ChatThreads.RemoveRange(threads);
        await dbContext.SaveChangesAsync(cancellationToken);
        await bucketStorage.DeleteManyAsync(storageKeys, cancellationToken);

        return threads.Count;
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
