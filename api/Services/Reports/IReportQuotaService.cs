namespace Amanah.Api.Services.Reports;

public enum QuotaFailureKind
{
    None,
    DailyQuota,
    OpenCap,
}

public sealed record QuotaCheckResult(QuotaFailureKind Kind, int? RetryAfterSeconds = null);

public interface IReportQuotaService
{
    Task<QuotaCheckResult> CheckNewSubmissionAsync(
        Guid userId,
        bool isResubmission = false,
        CancellationToken cancellationToken = default);
}
