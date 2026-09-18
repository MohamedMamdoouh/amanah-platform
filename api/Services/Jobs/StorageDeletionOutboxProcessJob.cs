using Amanah.Api.Services.Storage;

namespace Amanah.Api.Services.Jobs;

public sealed class StorageDeletionOutboxProcessJob(
    StorageDeletionOutboxBatchService batchService,
    ILogger<StorageDeletionOutboxProcessJob> logger) : ILifecycleJob
{
    public string Name => "StorageDeletionOutboxProcess";

    public async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        var processedCount = await batchService.ProcessPendingBatchAsync(cancellationToken);

        logger.LogInformation(
            "Storage deletion outbox process job handled {ProcessedCount} message(s).",
            processedCount);
    }
}
