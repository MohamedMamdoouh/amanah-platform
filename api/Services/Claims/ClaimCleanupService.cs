using Amanah.Api.Data.Entities;
using Amanah.Api.Services.Storage;
using Amanah.Api.Services.Uploads;

namespace Amanah.Api.Services.Claims;

public sealed class ClaimCleanupService(StorageDeletionEnqueueService enqueueService)
{
    public IReadOnlyList<string> ClearClaimPhoto(Claim claim)
    {
        if (string.IsNullOrWhiteSpace(claim.PhotoStorageKey))
        {
            return [];
        }

        var storageKeys = new[]
        {
            claim.PhotoStorageKey,
            ClaimPhotoStorageKeys.ThumbnailForOriginal(claim.PhotoStorageKey),
        };

        claim.PhotoStorageKey = null;
        return storageKeys;
    }

    public Task EnqueueClaimPhotoStorageAsync(
        IReadOnlyList<string> storageKeys,
        CancellationToken cancellationToken = default) =>
        storageKeys.Count == 0
            ? Task.CompletedTask
            : enqueueService.EnqueueAsync(
                storageKeys,
                StorageDeletionSource.ClaimTerminal,
                cancellationToken);
}
