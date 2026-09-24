using Amanah.Api.Services.External;

namespace Amanah.Api.Tests.Auth.Fakes;

public sealed class RecordingAdminAlertEmailSender : IAdminAlertEmailSender
{
    public List<(Guid ReportId, string ReportType, string CategoryCode)> SentAlerts { get; } = [];

    public bool ShouldThrow { get; set; }

    public int? FailureStatusCode { get; set; }

    public Task SendNewSubmissionAlertAsync(
        Guid reportId,
        string reportType,
        string categoryCode,
        CancellationToken cancellationToken = default)
    {
        if (FailureStatusCode is int statusCode)
        {
            throw new EmailApiException(statusCode, $"Email provider failed with HTTP {statusCode}.");
        }

        if (ShouldThrow)
        {
            throw new HttpRequestException("Email provider unavailable.");
        }

        SentAlerts.Add((reportId, reportType, categoryCode));
        return Task.CompletedTask;
    }
}
