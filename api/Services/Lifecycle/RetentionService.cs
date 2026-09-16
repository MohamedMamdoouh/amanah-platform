using Amanah.Api.Data;
using Amanah.Api.Data.Entities;
using Amanah.Api.Services.Storage;

namespace Amanah.Api.Services.Lifecycle;

public sealed class RetentionService(
    AppDbContext dbContext,
    IBucketStorage bucketStorage) : IRetentionService
{
    public IReadOnlyList<string> RemoveReportPhotos(Report report)
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

    public Task DeleteReportPhotoStorageAsync(
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
}
