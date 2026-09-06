namespace Amanah.Api.Services.External;

public interface IAdminAlertEmailSender
{
    Task SendNewSubmissionAlertAsync(
        Guid reportId,
        string reportType,
        string categoryCode,
        CancellationToken cancellationToken = default);
}
