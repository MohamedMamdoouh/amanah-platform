using Amanah.Api.Data.Entities;
using Amanah.Api.Models.Errors;

namespace Amanah.Api.Services.Lifecycle;

public interface IReportLifecycleService
{
    void InitializePublishedTimer(Report report, DateTimeOffset now);

    void PausePublishedTimer(Report report, DateTimeOffset now);

    void ResumePublishedTimer(Report report, DateTimeOffset now);

    int GetCumulativePublishedSeconds(Report report, DateTimeOffset now);

    Task<Result> WithdrawAsync(
        Report report,
        string? reason,
        CancellationToken cancellationToken = default);
}
