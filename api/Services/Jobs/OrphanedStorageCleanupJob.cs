using Amanah.Api.Services.Lifecycle;

namespace Amanah.Api.Services.Jobs;

public sealed class OrphanedStorageCleanupJob(
    OrphanedStorageCleanupService cleanupService,
    ILogger<OrphanedStorageCleanupJob> logger) : ILifecycleJob
{
    public string Name => "OrphanedStorageCleanup";

    public async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        var deletedCount = await cleanupService.CleanupAsync(cancellationToken);

        logger.LogInformation(
            "Orphaned storage cleanup job deleted {DeletedCount} object(s).",
            deletedCount);
    }
}
