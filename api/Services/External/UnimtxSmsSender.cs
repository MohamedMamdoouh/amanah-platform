using Amanah.Api.Models.Common;
using Amanah.Api.Observability;
using Amanah.Api.Options;
using Microsoft.Extensions.Options;

namespace Amanah.Api.Services.External;

public sealed class UnimtxSmsSender(
    HttpClient httpClient,
    IOptions<SmsOptions> smsOptions,
    IOptions<OtpOptions> otpOptions,
    ILogger<UnimtxSmsSender> logger,
    AppMetrics metrics) : ISmsSender
{
    private const string ApiBaseUrl = "https://api.unimtx.com/";
    private const string SuccessCode = "0";

    public Task SendOtpAsync(
        string normalizedPhone,
        string code,
        Guid idempotencyKey,
        CancellationToken cancellationToken = default) =>
        SendOtpCoreAsync(normalizedPhone, code, idempotencyKey, cancellationToken);

    private async Task SendOtpCoreAsync(
        string normalizedPhone,
        string code,
        Guid idempotencyKey,
        CancellationToken cancellationToken)
    {
        var apiKey = smsOptions.Value.ApiKey!;
        var ttlSeconds = otpOptions.Value.CodeLifetimeMinutes * 60;

        var requestUri = $"{ApiBaseUrl}?action=otp.send&accessKeyId={Uri.EscapeDataString(apiKey)}";
        using var request = new HttpRequestMessage(HttpMethod.Post, requestUri);
        request.Content = JsonContent.Create(new SendOtpRequest
        {
            To = normalizedPhone,
            Code = code,
            Digits = 6,
            Ttl = ttlSeconds,
            Channel = "sms",
        }, options: ApiJson.SerializerOptions);

        using var response = await httpClient.SendAsync(request, cancellationToken);
        var responseBody = await response.Content.ReadFromJsonAsync<UnimtxResponse>(
            ApiJson.SerializerOptions,
            cancellationToken);

        if (response.IsSuccessStatusCode
            && responseBody is not null
            && responseBody.Code == SuccessCode)
        {
            metrics.RecordSmsCompleted();
            logger.LogInformation(
                "OTP SMS sent (idempotency {IdempotencyKey}).",
                idempotencyKey);
            return;
        }

        metrics.RecordSmsFailed();
        var errorCode = responseBody?.Code ?? "unknown";
        var errorMessage = responseBody?.Message ?? "No response body";
        logger.LogError(
            "Unimtx OTP send failed (idempotency {IdempotencyKey}): {ErrorCode} {ErrorMessage} (HTTP {StatusCode})",
            idempotencyKey,
            errorCode,
            errorMessage,
            (int)response.StatusCode);

        throw new HttpRequestException(
            $"Unimtx OTP send failed with code {errorCode}: {errorMessage}.");
    }

    private sealed class SendOtpRequest
    {
        public required string To { get; init; }

        public required string Code { get; init; }

        public int Digits { get; init; }

        public int Ttl { get; init; }

        public required string Channel { get; init; }
    }

    private sealed class UnimtxResponse
    {
        public string? Code { get; init; }

        public string? Message { get; init; }
    }
}
