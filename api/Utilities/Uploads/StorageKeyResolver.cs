using Amanah.Api.Services.Storage;

namespace Amanah.Api.Utilities.Uploads;

public static class StorageKeyResolver
{
    public static async Task<string> ResolvePreferExistingThumbnailAsync(
        IBucketStorage bucketStorage,
        string originalKey,
        string? thumbnailKey,
        CancellationToken cancellationToken)
    {
        if (thumbnailKey is not null
            && await bucketStorage.ExistsAsync(thumbnailKey, cancellationToken))
        {
            return thumbnailKey;
        }

        return originalKey;
    }
}
