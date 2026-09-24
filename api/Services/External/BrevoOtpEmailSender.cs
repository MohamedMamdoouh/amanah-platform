using Amanah.Api.Options;
using Amanah.Api.Services.External.Email;
using Microsoft.Extensions.Options;

namespace Amanah.Api.Services.External;

public sealed class BrevoOtpEmailSender(
    HttpClient httpClient,
    IOptions<EmailOptions> emailOptions,
    IOptions<OtpOptions> otpOptions,
    ILogger<BrevoOtpEmailSender> logger) : IOtpEmailSender
{
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

        using var request = BrevoTransactionalEmail.CreateRequest(
            apiKey,
            fromAddress,
            options.FromName,
            normalizedEmail,
            OtpEmailTemplates.BuildSubject(),
            OtpEmailTemplates.BuildPlainText(code, lifetimeMinutes),
            OtpEmailTemplates.BuildHtml(code, lifetimeMinutes),
            idempotencyKey: idempotencyKey.ToString());

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
            "Brevo OTP email failed for {Email} (HTTP {StatusCode}): {ResponseBody}",
            normalizedEmail,
            (int)response.StatusCode,
            responseBody);

        throw new HttpRequestException(
            $"Brevo OTP email failed with HTTP {(int)response.StatusCode}.");
    }
}
