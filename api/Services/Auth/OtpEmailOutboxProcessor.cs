using Amanah.Api.Data;
using Amanah.Api.Data.Entities;
using Amanah.Api.Observability;
using Amanah.Api.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Amanah.Api.Services.Auth;

public sealed class OtpEmailOutboxProcessor(
    IServiceScopeFactory scopeFactory,
    IOptions<OtpOptions> options,
    ILogger<OtpEmailOutboxProcessor> logger,
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
                logger.LogError(exception, "OTP email outbox processor failed.");
            }

            await Task.Delay(TimeSpan.FromSeconds(otpOptions.OutboxPollIntervalSeconds), stoppingToken);
        }
    }

    private async Task ProcessPendingMessagesAsync(CancellationToken cancellationToken)
    {
        var otpOptions = options.Value;

        await using var scope = scopeFactory.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var dispatcher = scope.ServiceProvider.GetRequiredService<OtpEmailOutboxDispatcher>();

        var pendingIds = await dbContext.OtpEmailOutboxMessages
            .AsNoTracking()
            .Where(message => message.Status == OtpSmsOutboxStatus.Pending
                && message.AttemptCount < otpOptions.OutboxMaxAttempts)
            .OrderBy(message => message.CreatedAt)
            .Select(message => message.Id)
            .Take(otpOptions.OutboxBatchSize)
            .ToListAsync(cancellationToken);

        var backlogCount = await dbContext.OtpEmailOutboxMessages
            .AsNoTracking()
            .CountAsync(
                message => message.Status == OtpSmsOutboxStatus.Pending
                    && message.AttemptCount < otpOptions.OutboxMaxAttempts,
                cancellationToken);

        metrics.SetOtpEmailOutboxBacklog(backlogCount);

        foreach (var outboxId in pendingIds)
        {
            await dispatcher.DispatchAsync(outboxId, cancellationToken);
        }
    }
}
