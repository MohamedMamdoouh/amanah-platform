using Amanah.Api.Data;
using Amanah.Api.Services.Storage;
using Amanah.Api.Services.Uploads;
using Microsoft.EntityFrameworkCore;

namespace Amanah.Api.Services.Lifecycle;

public sealed class OrphanedStorageCleanupService(
    AppDbContext dbContext,
    IBucketStorage bucketStorage,
    TimeProvider timeProvider)
{
    private static readonly string[] StoragePrefixes =
        ["public/reports/", "private/reports/", "private/claims/"];
    private static readonly TimeSpan GracePeriod = TimeSpan.FromHours(1);

    public async Task<int> CleanupAsync(CancellationToken cancellationToken = default)
    {
        var graceThreshold = timeProvider.GetUtcNow() - GracePeriod;
        var referencedKeys = await LoadReferencedStorageKeysAsync(cancellationToken);
        var orphanKeys = new List<string>();

        foreach (var prefix in StoragePrefixes)
        {
            var objects = await bucketStorage.ListAsync(prefix, cancellationToken);
            foreach (var storageObject in objects)
            {
                if (referencedKeys.Contains(storageObject.Key))
                {
                    continue;
                }

                if (storageObject.LastModified > graceThreshold)
                {
                    continue;
                }

                orphanKeys.Add(storageObject.Key);
            }
        }

        if (orphanKeys.Count == 0)
        {
            return 0;
        }

        await bucketStorage.DeleteManyAsync(orphanKeys, cancellationToken);
        return orphanKeys.Count;
    }

    private async Task<HashSet<string>> LoadReferencedStorageKeysAsync(CancellationToken cancellationToken)
    {
        var reportPhotoKeys = await dbContext.ReportPhotos
            .AsNoTracking()
            .Select(photo => new { photo.StorageKey, photo.ThumbnailStorageKey })
            .ToListAsync(cancellationToken);

        var claimPhotoKeys = await dbContext.Claims
            .AsNoTracking()
            .Where(claim => claim.PhotoStorageKey != null)
            .Select(claim => claim.PhotoStorageKey!)
            .ToListAsync(cancellationToken);

        return reportPhotoKeys
            .SelectMany(photo => new[] { photo.StorageKey, photo.ThumbnailStorageKey })
            .Concat(claimPhotoKeys)
            .Concat(claimPhotoKeys.Select(ClaimPhotoStorageKeys.ThumbnailForOriginal))
            .Where(key => !string.IsNullOrWhiteSpace(key))
            .Select(key => key!)
            .ToHashSet(StringComparer.Ordinal);
    }
}
