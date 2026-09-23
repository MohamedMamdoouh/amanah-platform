using Amanah.Api.Options;
using Amanah.Api.Services.External.Email;
using Microsoft.Extensions.Options;

namespace Amanah.Api.Services.External;

public sealed class ConsoleOtpEmailSender(
    IOptions<OtpOptions> otpOptions,
    ILogger<ConsoleOtpEmailSender> logger) : IOtpEmailSender
{
    public Task SendOtpAsync(
        string normalizedEmail,
        string code,
        Guid idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        var plainText = OtpEmailTemplates.BuildPlainText(
            code,
            otpOptions.Value.CodeLifetimeMinutes);

        logger.LogInformation(
            "[Email] OTP for {Email} (idempotency {IdempotencyKey}): {PlainText}",
            normalizedEmail,
            idempotencyKey,
            plainText);

        return Task.CompletedTask;
    }
}
