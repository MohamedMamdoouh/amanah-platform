using Amanah.Api.Services.Lifecycle;

namespace Amanah.Api.Services.Jobs;

public sealed class ChatRetentionJob(
    RetentionService retentionService,
    ILogger<ChatRetentionJob> logger) : ILifecycleJob
{
    public string Name => "ChatRetention";

    public async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        var deletedCount = await retentionService.ProcessChatRetentionAsync(cancellationToken);

        logger.LogInformation(
            "Chat retention job deleted {DeletedCount} read-only chat thread(s).",
            deletedCount);
    }
}
