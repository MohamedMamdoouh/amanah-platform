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

    // UniSdk wraps timeouts/network/parse failures as UniException("-1") with no
    // inner exception. Map those to TimeoutException so the outbox retries instead
    // of marking Failed and deleting the OTP.
    private const string SdkTransportErrorCode = "-1";

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

            if (ex.ErrorCode == SdkTransportErrorCode)
            {
                throw new TimeoutException(
                    $"Unimtx SMS send failed with a transport error: {ex.ErrorMessage}.",
                    ex);
            }

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
