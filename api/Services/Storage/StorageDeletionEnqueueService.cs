using Amanah.Api.Data;
using Amanah.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Amanah.Api.Services.Storage;

public sealed class StorageDeletionEnqueueService(
    AppDbContext dbContext,
    TimeProvider timeProvider)
{
    public async Task EnqueueAsync(
        IEnumerable<string> storageKeys,
        StorageDeletionSource source,
        CancellationToken cancellationToken = default)
    {
        var normalizedKeys = storageKeys
            .Where(key => !string.IsNullOrWhiteSpace(key))
            .Select(key => key.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToList();

        if (normalizedKeys.Count == 0)
        {
            return;
        }

        var alreadyPending = await dbContext.StorageDeletionOutboxMessages
            .AsNoTracking()
            .Where(message =>
                message.Status == StorageDeletionOutboxStatus.Pending
                && normalizedKeys.Contains(message.StorageKey))
            .Select(message => message.StorageKey)
            .ToListAsync(cancellationToken);

        var pendingKeySet = alreadyPending.ToHashSet(StringComparer.Ordinal);
        var now = timeProvider.GetUtcNow();

        foreach (var storageKey in normalizedKeys)
        {
            if (pendingKeySet.Contains(storageKey))
            {
                continue;
            }

            dbContext.StorageDeletionOutboxMessages.Add(new StorageDeletionOutboxMessage
            {
                Id = Guid.NewGuid(),
                StorageKey = storageKey,
                Status = StorageDeletionOutboxStatus.Pending,
                Source = source,
                CreatedAt = now,
                AttemptCount = 0,
            });
        }
    }
}
