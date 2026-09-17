using Amanah.Api.Services.Lifecycle;
using Amanah.Api.Utilities.Common;

namespace Amanah.Api.Services.Jobs;

public sealed class ListingExpiryWarningJob(
    IReportLifecycleService reportLifecycleService,
    ILogger<ListingExpiryWarningJob> logger) : ILifecycleJob
{
    public string Name => "ListingExpiryWarning";

    public async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        var cairoDate = CairoTime.TodayInCairo();
        var warningsSent = await reportLifecycleService.ProcessListingExpiryWarningsAsync(cancellationToken);

        logger.LogInformation(
            "Listing expiry warning job sent {WarningsSent} notification(s) for Cairo date {CairoDate}.",
            warningsSent,
            cairoDate);
    }
}

public sealed class ListingAutoExpiryJob(
    IReportLifecycleService reportLifecycleService,
    ILogger<ListingAutoExpiryJob> logger) : ILifecycleJob
{
    public string Name => "ListingAutoExpiry";

    public async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        var cairoDate = CairoTime.TodayInCairo();
        var expiredCount = await reportLifecycleService.ProcessListingAutoExpiryAsync(cancellationToken);

        logger.LogInformation(
            "Listing auto-expiry job expired {ExpiredCount} report(s) for Cairo date {CairoDate}.",
            expiredCount,
            cairoDate);
    }
}
