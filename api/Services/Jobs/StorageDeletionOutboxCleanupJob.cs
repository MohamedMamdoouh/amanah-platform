using Amanah.Api.Services.Lifecycle;

namespace Amanah.Api.Services.Jobs;

public sealed class StorageDeletionOutboxCleanupJob(
    RetentionService retentionService,
    ILogger<StorageDeletionOutboxCleanupJob> logger) : ILifecycleJob
{
    public string Name => "StorageDeletionOutboxCleanup";

    public async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        var deletedCount = await retentionService.ProcessStorageDeletionOutboxCleanupAsync(cancellationToken);

        logger.LogInformation(
            "Storage deletion outbox cleanup job removed {DeletedCount} processed row(s).",
            deletedCount);
    }
}
