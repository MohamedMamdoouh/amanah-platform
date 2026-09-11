namespace Amanah.Api.Services.Claims;

public interface IClaimCleanupService
{
    Task<int> ClosePendingClaimsAsync(
        Guid reportId,
        string reason,
        CancellationToken cancellationToken = default);
}
