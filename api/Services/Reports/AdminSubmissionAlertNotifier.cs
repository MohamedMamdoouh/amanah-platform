using Amanah.Api.Data;
using Amanah.Api.Data.Entities;
using Amanah.Api.Options;
using Microsoft.Extensions.Options;

namespace Amanah.Api.Services.Reports;

public sealed class AdminSubmissionAlertNotifier(
    AppDbContext dbContext,
    IOptions<EmailOptions> emailOptions,
    TimeProvider timeProvider)
{
    public void EnqueuePendingReview(Report report, Category category)
    {
        if (!emailOptions.Value.IsConfigured)
        {
            return;
        }

        var reportType = report.Type == ReportType.Found ? "found" : "lost";

        dbContext.AdminAlertEmailOutboxMessages.Add(new AdminAlertEmailOutboxMessage
        {
            ReportId = report.Id,
            ReportType = reportType,
            CategoryCode = category.Code,
            Status = AdminAlertEmailOutboxStatus.Pending,
            CreatedAt = timeProvider.GetUtcNow(),
        });
    }
}
