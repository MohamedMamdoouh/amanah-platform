namespace Amanah.Api.Services.External;

public sealed class NullAdminAlertEmailSender : IAdminAlertEmailSender
{
    public Task SendNewSubmissionAlertAsync(
        Guid reportId,
        string reportType,
        string categoryCode,
        CancellationToken cancellationToken = default) =>
        Task.CompletedTask;
}
