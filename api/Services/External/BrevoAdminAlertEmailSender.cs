using Amanah.Api.Options;
using Amanah.Api.Services.External.Email;
using Microsoft.Extensions.Options;

namespace Amanah.Api.Services.External;

public sealed class BrevoAdminAlertEmailSender(
    HttpClient httpClient,
    IOptions<EmailOptions> emailOptions,
    IOptions<CorsOptions> corsOptions,
    ILogger<BrevoAdminAlertEmailSender> logger) : IAdminAlertEmailSender
{
    public async Task SendNewSubmissionAlertAsync(
        Guid reportId,
        string reportType,
        string categoryCode,
        CancellationToken cancellationToken = default)
    {
        var options = emailOptions.Value;
        var apiKey = options.ApiKey;
        var fromAddress = options.FromAddress;
        var adminAlertTo = options.AdminAlertTo;

        if (string.IsNullOrWhiteSpace(apiKey)
            || string.IsNullOrWhiteSpace(fromAddress)
            || string.IsNullOrWhiteSpace(adminAlertTo))
        {
            logger.LogWarning(
                "Admin alert email skipped for report {ReportId}: Email configuration is incomplete.",
                reportId);
            return;
        }

        var reviewLink = BuildReviewLink(reportId, options);
        var subject = AdminAlertEmailTemplates.BuildSubject(reportType);

        using var request = BrevoTransactionalEmail.CreateRequest(
            apiKey,
            fromAddress,
            options.FromName,
            adminAlertTo,
            subject,
            AdminAlertEmailTemplates.BuildPlainText(reportType, categoryCode, reviewLink),
            AdminAlertEmailTemplates.BuildHtml(reportType, categoryCode, reviewLink));

        using var response = await httpClient.SendAsync(request, cancellationToken);
        if (response.IsSuccessStatusCode)
        {
            logger.LogInformation(
                "Admin alert email sent for report {ReportId}.",
                reportId);
            return;
        }

        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
        logger.LogError(
            "Brevo admin alert failed for report {ReportId} (HTTP {StatusCode}): {ResponseBody}",
            reportId,
            (int)response.StatusCode,
            responseBody);

        throw new EmailApiException(
            (int)response.StatusCode,
            $"Brevo admin alert failed with HTTP {(int)response.StatusCode}.");
    }

    private string BuildReviewLink(Guid reportId, EmailOptions options)
    {
        var baseUrl = options.AppBaseUrl?.TrimEnd('/')
            ?? corsOptions.Value.AllowedOrigins.FirstOrDefault()?.TrimEnd('/')
            ?? string.Empty;

        var path = $"/admin/moderation/{reportId}";
        return string.IsNullOrEmpty(baseUrl) ? path : $"{baseUrl}{path}";
    }
}
