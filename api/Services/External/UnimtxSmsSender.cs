using Amanah.Api.Options;
using Microsoft.Extensions.Options;
using UniSdk;

namespace Amanah.Api.Services.External;

public sealed class UnimtxSmsSender(
    IUnimtxClient unimtxClient,
    IOptions<OtpOptions> otpOptions,
    ILogger<UnimtxSmsSender> logger) : ISmsSender
{
    private const string VerificationTemplateId = "pub_verif_en_ttl";

    public async Task SendOtpAsync(
        string normalizedPhone,
        string code,
        Guid idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        var ttlMinutes = otpOptions.Value.CodeLifetimeMinutes;

        try
        {
            await unimtxClient.SendMessageAsync(
                new
                {
                    to = normalizedPhone,
                    templateId = VerificationTemplateId,
                    templateData = new
                    {
                        code,
                        ttl = ttlMinutes.ToString(),
                    },
                },
                cancellationToken);
        }
        catch (UniException ex)
        {
            logger.LogError(
                ex,
                "Unimtx SMS send failed for {Phone} (idempotency {IdempotencyKey}): {ErrorCode} {ErrorMessage}",
                normalizedPhone,
                idempotencyKey,
                ex.ErrorCode,
                ex.ErrorMessage);

            throw new HttpRequestException(
                $"Unimtx SMS send failed with code {ex.ErrorCode}: {ex.ErrorMessage}.",
                ex);
        }

        logger.LogInformation(
            "OTP SMS accepted by Unimtx for {Phone} (idempotency {IdempotencyKey}).",
            normalizedPhone,
            idempotencyKey);
    }
}
