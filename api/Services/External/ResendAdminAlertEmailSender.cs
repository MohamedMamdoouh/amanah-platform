using Amanah.Api.Models.Common;
using Amanah.Api.Options;
using Amanah.Api.Services.External.Email;
using Microsoft.Extensions.Options;

namespace Amanah.Api.Services.External;

public sealed class ResendAdminAlertEmailSender(
    HttpClient httpClient,
    IOptions<EmailOptions> emailOptions,
    IOptions<CorsOptions> corsOptions,
    ILogger<ResendAdminAlertEmailSender> logger) : IAdminAlertEmailSender
{
    private const string ApiUrl = "https://api.resend.com/emails";

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

        using var request = new HttpRequestMessage(HttpMethod.Post, ApiUrl);
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiKey);
        request.Content = JsonContent.Create(new ResendEmailRequest
        {
            From = fromAddress,
            To = [adminAlertTo],
            Subject = subject,
            Text = AdminAlertEmailTemplates.BuildPlainText(reportType, categoryCode, reviewLink),
            Html = AdminAlertEmailTemplates.BuildHtml(reportType, categoryCode, reviewLink),
        }, options: ApiJson.SerializerOptions);

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
            "Resend admin alert failed for report {ReportId} (HTTP {StatusCode}): {ResponseBody}",
            reportId,
            (int)response.StatusCode,
            responseBody);

        throw new ResendApiException(
            (int)response.StatusCode,
            $"Resend admin alert failed with HTTP {(int)response.StatusCode}.");
    }

    private string BuildReviewLink(Guid reportId, EmailOptions options)
    {
        var baseUrl = options.AppBaseUrl?.TrimEnd('/')
            ?? corsOptions.Value.AllowedOrigins.FirstOrDefault()?.TrimEnd('/')
            ?? string.Empty;

        var path = $"/admin/moderation/{reportId}";
        return string.IsNullOrEmpty(baseUrl) ? path : $"{baseUrl}{path}";
    }

    private sealed class ResendEmailRequest
    {
        public required string From { get; init; }

        public required IReadOnlyList<string> To { get; init; }

        public required string Subject { get; init; }

        public required string Text { get; init; }

        public required string Html { get; init; }
    }
}
