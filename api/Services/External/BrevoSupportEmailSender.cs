using Amanah.Api.Options;
using Amanah.Api.Services.External.Email;
using Microsoft.Extensions.Options;

namespace Amanah.Api.Services.External;

public sealed class BrevoSupportEmailSender(
    HttpClient httpClient,
    IOptions<EmailOptions> emailOptions,
    ILogger<BrevoSupportEmailSender> logger) : ISupportEmailSender
{
    public async Task SendSupportMessageAsync(
        string normalizedReplyEmail,
        string displayName,
        string message,
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
            logger.LogWarning("Support email not sent: Email configuration is incomplete.");
            throw new EmailApiException(
                StatusCodes.Status503ServiceUnavailable,
                "Support email configuration is incomplete.");
        }

        using var request = BrevoTransactionalEmail.CreateRequest(
            apiKey,
            fromAddress,
            options.FromName,
            adminAlertTo,
            SupportEmailTemplates.BuildSubject(displayName),
            SupportEmailTemplates.BuildPlainText(
                displayName,
                normalizedReplyEmail,
                message),
            SupportEmailTemplates.BuildHtml(
                displayName,
                normalizedReplyEmail,
                message),
            replyToEmail: normalizedReplyEmail);

        using var response = await httpClient.SendAsync(request, cancellationToken);
        if (response.IsSuccessStatusCode)
        {
            logger.LogInformation(
                "Support email sent from {ReplyEmail} ({DisplayName}).",
                normalizedReplyEmail,
                displayName);
            return;
        }

        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
        logger.LogError(
            "Brevo support email failed for {ReplyEmail} (HTTP {StatusCode}): {ResponseBody}",
            normalizedReplyEmail,
            (int)response.StatusCode,
            responseBody);

        throw new EmailApiException(
            (int)response.StatusCode,
            $"Brevo support email failed with HTTP {(int)response.StatusCode}.");
    }
}
