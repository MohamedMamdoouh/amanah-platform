using Amanah.Api.Services.Claims;

namespace Amanah.Api.Services.Jobs;

public sealed class PendingClaimTimeoutJob(
    IClaimService claimService,
    ILogger<PendingClaimTimeoutJob> logger) : ILifecycleJob
{
    public string Name => "PendingClaimTimeout";

    public async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        var withdrawnCount = await claimService.ProcessPendingClaimTimeoutsAsync(cancellationToken);

        logger.LogInformation(
            "Pending claim timeout job auto-withdrew {WithdrawnCount} claim(s).",
            withdrawnCount);
    }
}
