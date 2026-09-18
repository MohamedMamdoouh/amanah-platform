using Amanah.Api.Options;
using Microsoft.Extensions.Options;

namespace Amanah.Api.Services.Storage;

public sealed class StorageDeletionOutboxProcessor(
    IServiceScopeFactory scopeFactory,
    IOptions<StorageDeletionOutboxOptions> options,
    ILogger<StorageDeletionOutboxProcessor> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var outboxOptions = options.Value;

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var batchService = scope.ServiceProvider.GetRequiredService<StorageDeletionOutboxBatchService>();
                await batchService.ProcessPendingBatchAsync(stoppingToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                logger.LogError(exception, "Storage deletion outbox processor failed.");
            }

            await Task.Delay(TimeSpan.FromSeconds(outboxOptions.PollIntervalSeconds), stoppingToken);
        }
    }
}
