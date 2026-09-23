using System.Net.Http.Headers;
using Amanah.Api.Models.Common;
using Amanah.Api.Options;
using Amanah.Api.Services.External.Email;
using Microsoft.Extensions.Options;

namespace Amanah.Api.Services.External;

public sealed class ResendOtpEmailSender(
    HttpClient httpClient,
    IOptions<EmailOptions> emailOptions,
    IOptions<OtpOptions> otpOptions,
    ILogger<ResendOtpEmailSender> logger) : IOtpEmailSender
{
    private const string ApiUrl = "https://api.resend.com/emails";

    public async Task SendOtpAsync(
        string normalizedEmail,
        string code,
        Guid idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        var options = emailOptions.Value;
        var apiKey = options.ApiKey;
        var fromAddress = options.FromAddress;

        if (string.IsNullOrWhiteSpace(apiKey) || string.IsNullOrWhiteSpace(fromAddress))
        {
            throw new InvalidOperationException("Email OTP is not configured.");
        }

        var lifetimeMinutes = otpOptions.Value.CodeLifetimeMinutes;

        using var request = new HttpRequestMessage(HttpMethod.Post, ApiUrl);
        request.Headers.Authorization =
            new AuthenticationHeaderValue("Bearer", apiKey);
        request.Headers.TryAddWithoutValidation(
            "Idempotency-Key",
            idempotencyKey.ToString());
        request.Content = JsonContent.Create(new ResendEmailRequest
        {
            From = fromAddress,
            To = [normalizedEmail],
            Subject = OtpEmailTemplates.BuildSubject(),
            Text = OtpEmailTemplates.BuildPlainText(code, lifetimeMinutes),
            Html = OtpEmailTemplates.BuildHtml(code, lifetimeMinutes),
        }, options: ApiJson.SerializerOptions);

        using var response = await httpClient.SendAsync(request, cancellationToken);
        if (response.IsSuccessStatusCode)
        {
            logger.LogInformation(
                "OTP email sent to {Email} (idempotency {IdempotencyKey}).",
                normalizedEmail,
                idempotencyKey);
            return;
        }

        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
        logger.LogError(
            "Resend OTP email failed for {Email} (HTTP {StatusCode}): {ResponseBody}",
            normalizedEmail,
            (int)response.StatusCode,
            responseBody);

        throw new HttpRequestException(
            $"Resend OTP email failed with HTTP {(int)response.StatusCode}.");
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
