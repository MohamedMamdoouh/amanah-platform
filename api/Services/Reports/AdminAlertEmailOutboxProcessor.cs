using Amanah.Api.Data;
using Amanah.Api.Data.Entities;
using Amanah.Api.Observability;
using Amanah.Api.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Amanah.Api.Services.Reports;

public sealed class AdminAlertEmailOutboxProcessor(
    IServiceScopeFactory scopeFactory,
    IOptions<EmailOptions> options,
    ILogger<AdminAlertEmailOutboxProcessor> logger,
    AppMetrics metrics) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var emailOptions = options.Value;

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessPendingMessagesAsync(stoppingToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                logger.LogError(exception, "Admin alert email outbox processor failed.");
            }

            await Task.Delay(TimeSpan.FromSeconds(emailOptions.OutboxPollIntervalSeconds), stoppingToken);
        }
    }

    private async Task ProcessPendingMessagesAsync(CancellationToken cancellationToken)
    {
        var emailOptions = options.Value;

        await using var scope = scopeFactory.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var dispatcher = scope.ServiceProvider.GetRequiredService<AdminAlertEmailOutboxDispatcher>();

        var pendingIds = await dbContext.AdminAlertEmailOutboxMessages
            .AsNoTracking()
            .Where(message => message.Status == AdminAlertEmailOutboxStatus.Pending
                && message.AttemptCount < emailOptions.OutboxMaxAttempts)
            .OrderBy(message => message.CreatedAt)
            .Select(message => message.Id)
            .Take(emailOptions.OutboxBatchSize)
            .ToListAsync(cancellationToken);

        var backlogCount = await dbContext.AdminAlertEmailOutboxMessages
            .AsNoTracking()
            .CountAsync(
                message => message.Status == AdminAlertEmailOutboxStatus.Pending
                    && message.AttemptCount < emailOptions.OutboxMaxAttempts,
                cancellationToken);

        metrics.SetAdminAlertEmailOutboxBacklog(backlogCount);

        foreach (var outboxId in pendingIds)
        {
            await dispatcher.DispatchAsync(outboxId, cancellationToken);
        }
    }
}
