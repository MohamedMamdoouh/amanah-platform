using Amanah.Api.Data;
using Amanah.Api.Data.Entities;
using Amanah.Api.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Amanah.Api.Services.Storage;

public sealed class StorageDeletionOutboxBatchService(
    AppDbContext dbContext,
    StorageDeletionOutboxDispatcher dispatcher,
    IOptions<StorageDeletionOutboxOptions> options)
{
    public async Task<int> ProcessPendingBatchAsync(CancellationToken cancellationToken = default)
    {
        var outboxOptions = options.Value;

        var pendingIds = await dbContext.StorageDeletionOutboxMessages
            .AsNoTracking()
            .Where(message =>
                message.Status == StorageDeletionOutboxStatus.Pending
                && message.AttemptCount < outboxOptions.MaxAttempts)
            .OrderBy(message => message.CreatedAt)
            .Select(message => message.Id)
            .Take(outboxOptions.BatchSize)
            .ToListAsync(cancellationToken);

        var processedCount = 0;
        foreach (var outboxId in pendingIds)
        {
            if (await dispatcher.DispatchAsync(outboxId, cancellationToken))
            {
                processedCount++;
            }
        }

        return processedCount;
    }
}
