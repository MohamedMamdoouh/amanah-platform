using System.Net.Http.Headers;
using Amanah.Api.Models.Common;
using Amanah.Api.Options;
using Amanah.Api.Services.External.Email;
using Microsoft.Extensions.Options;

namespace Amanah.Api.Services.External;

public sealed class ResendSupportEmailSender(
    HttpClient httpClient,
    IOptions<EmailOptions> emailOptions,
    ILogger<ResendSupportEmailSender> logger) : ISupportEmailSender
{
    private const string ApiUrl = "https://api.resend.com/emails";

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
            logger.LogWarning("Support email skipped: Email configuration is incomplete.");
            return;
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, ApiUrl);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        request.Content = JsonContent.Create(new ResendEmailRequest
        {
            From = fromAddress,
            To = [adminAlertTo],
            ReplyTo = normalizedReplyEmail,
            Subject = SupportEmailTemplates.BuildSubject(displayName),
            Text = SupportEmailTemplates.BuildPlainText(
                displayName,
                normalizedReplyEmail,
                message),
            Html = SupportEmailTemplates.BuildHtml(
                displayName,
                normalizedReplyEmail,
                message),
        }, options: ApiJson.SerializerOptions);

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
            "Resend support email failed for {ReplyEmail} (HTTP {StatusCode}): {ResponseBody}",
            normalizedReplyEmail,
            (int)response.StatusCode,
            responseBody);

        throw new ResendApiException(
            (int)response.StatusCode,
            $"Resend support email failed with HTTP {(int)response.StatusCode}.");
    }

    private sealed class ResendEmailRequest
    {
        public required string From { get; init; }

        public required IReadOnlyList<string> To { get; init; }

        public required string ReplyTo { get; init; }

        public required string Subject { get; init; }

        public required string Text { get; init; }

        public required string Html { get; init; }
    }
}
