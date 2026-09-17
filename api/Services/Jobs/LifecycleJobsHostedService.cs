using Amanah.Api.Options;
using Amanah.Api.Utilities.Common;
using Microsoft.Extensions.Options;

namespace Amanah.Api.Services.Jobs;

public sealed class LifecycleJobsHostedService(
    IServiceScopeFactory scopeFactory,
    IOptions<LifecycleOptions> options,
    ILogger<LifecycleJobsHostedService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var lifecycleOptions = options.Value;

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                logger.LogDebug(
                    "Lifecycle jobs poll starting for Cairo date {CairoDate}.",
                    CairoTime.TodayInCairo());

                await using var scope = scopeFactory.CreateAsyncScope();
                var jobRunner = scope.ServiceProvider.GetRequiredService<IJobRunner>();
                await jobRunner.RunAllAsync(stoppingToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                logger.LogError(exception, "Lifecycle jobs hosted service failed.");
            }

            await Task.Delay(
                TimeSpan.FromSeconds(lifecycleOptions.JobsPollIntervalSeconds),
                stoppingToken);
        }
    }
}
