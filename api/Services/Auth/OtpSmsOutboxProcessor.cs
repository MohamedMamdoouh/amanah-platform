using Amanah.Api.Data;
using Amanah.Api.Data.Entities;
using Amanah.Api.Observability;
using Amanah.Api.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Amanah.Api.Services.Auth;

public sealed class OtpSmsOutboxProcessor(
    IServiceScopeFactory scopeFactory,
    IOptions<OtpOptions> options,
    ILogger<OtpSmsOutboxProcessor> logger,
    AppMetrics metrics) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var otpOptions = options.Value;

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessPendingMessagesAsync(stoppingToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                logger.LogError(exception, "OTP SMS outbox processor failed.");
            }

            await Task.Delay(TimeSpan.FromSeconds(otpOptions.OutboxPollIntervalSeconds), stoppingToken);
        }
    }

    private async Task ProcessPendingMessagesAsync(CancellationToken cancellationToken)
    {
        var otpOptions = options.Value;

        await using var scope = scopeFactory.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var dispatcher = scope.ServiceProvider.GetRequiredService<OtpSmsOutboxDispatcher>();

        var pendingIds = await dbContext.OtpSmsOutboxMessages
            .AsNoTracking()
            .Where(message => message.Status == OtpSmsOutboxStatus.Pending
                && message.AttemptCount < otpOptions.OutboxMaxAttempts)
            .OrderBy(message => message.CreatedAt)
            .Select(message => message.Id)
            .Take(otpOptions.OutboxBatchSize)
            .ToListAsync(cancellationToken);

        var backlogCount = await dbContext.OtpSmsOutboxMessages
            .AsNoTracking()
            .CountAsync(
                message => message.Status == OtpSmsOutboxStatus.Pending
                    && message.AttemptCount < otpOptions.OutboxMaxAttempts,
                cancellationToken);

        metrics.SetOtpOutboxBacklog(backlogCount);

        foreach (var outboxId in pendingIds)
        {
            await dispatcher.DispatchAsync(outboxId, cancellationToken);
        }
    }
}
