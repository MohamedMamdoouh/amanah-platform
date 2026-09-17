using Amanah.Api.Services.Lifecycle;

namespace Amanah.Api.Services.Jobs;

public sealed class RejectedReportCleanupJob(
    RetentionService retentionService,
    ILogger<RejectedReportCleanupJob> logger) : ILifecycleJob
{
    public string Name => "RejectedReportCleanup";

    public async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        var deletedCount = await retentionService.ProcessRejectedReportCleanupAsync(cancellationToken);

        logger.LogInformation(
            "Rejected report cleanup job deleted {DeletedCount} report(s).",
            deletedCount);
    }
}
