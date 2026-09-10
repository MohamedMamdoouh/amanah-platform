namespace Amanah.Api.Services.Claims;

public sealed record ClaimQuotaCheckResult(bool IsExceeded, int? RetryAfterSeconds = null);

public interface IClaimQuotaService
{
    Task<ClaimQuotaCheckResult> CheckDailySubmissionAsync(
        Guid userId,
        CancellationToken cancellationToken = default);
}
