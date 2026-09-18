using Amanah.Api.Data;
using Amanah.Api.Data.Entities;
using Amanah.Api.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Amanah.Api.Services.Storage;

public sealed class StorageDeletionOutboxDispatcher(
    AppDbContext dbContext,
    IBucketStorage bucketStorage,
    IOptions<StorageDeletionOutboxOptions> options,
    TimeProvider timeProvider,
    ILogger<StorageDeletionOutboxDispatcher> logger)
{
    public async Task<bool> DispatchAsync(Guid outboxId, CancellationToken cancellationToken = default)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        await dbContext.Database.ExecuteSqlAsync(
            $"SELECT pg_advisory_xact_lock(hashtext({outboxId.ToString()}))",
            cancellationToken);

        var message = await dbContext.StorageDeletionOutboxMessages
            .FirstOrDefaultAsync(entry => entry.Id == outboxId, cancellationToken);

        if (message is null)
        {
            logger.LogWarning("Storage deletion outbox message {OutboxId} was not found.", outboxId);
            await transaction.RollbackAsync(cancellationToken);
            return false;
        }

        if (message.Status == StorageDeletionOutboxStatus.Sent)
        {
            await transaction.CommitAsync(cancellationToken);
            return true;
        }

        if (message.Status == StorageDeletionOutboxStatus.Failed)
        {
            await transaction.RollbackAsync(cancellationToken);
            return false;
        }

        if (message.Status != StorageDeletionOutboxStatus.Pending)
        {
            await transaction.RollbackAsync(cancellationToken);
            return false;
        }

        if (message.AttemptCount >= options.Value.MaxAttempts)
        {
            await MarkFailedAsync(message, "Maximum dispatch attempts exceeded.", cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return false;
        }

        message.AttemptCount++;

        try
        {
            await bucketStorage.DeleteAsync(message.StorageKey, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            await MarkFailureStateAsync(message, exception.Message, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return false;
        }

        var now = timeProvider.GetUtcNow();
        message.Status = StorageDeletionOutboxStatus.Sent;
        message.ProcessedAt = now;
        message.LastError = null;
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return true;
    }

    private async Task MarkFailureStateAsync(
        StorageDeletionOutboxMessage message,
        string error,
        CancellationToken cancellationToken)
    {
        if (message.AttemptCount >= options.Value.MaxAttempts)
        {
            await MarkFailedAsync(message, error, cancellationToken);
            return;
        }

        message.LastError = error;
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task MarkFailedAsync(
        StorageDeletionOutboxMessage message,
        string error,
        CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        message.Status = StorageDeletionOutboxStatus.Failed;
        message.ProcessedAt = now;
        message.LastError = error;
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
