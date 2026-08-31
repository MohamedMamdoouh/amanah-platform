using Amanah.Api.Models.Common;
using Amanah.Api.Options;
using Microsoft.Extensions.Options;

namespace Amanah.Api.Services.External;

public sealed class UnimtxSmsSender(
    HttpClient httpClient,
    IOptions<SmsOptions> smsOptions,
    IOptions<OtpOptions> otpOptions,
    ILogger<UnimtxSmsSender> logger) : ISmsSender
{
    private const string ApiBaseUrl = "https://api.unimtx.com/";
    private const string SuccessCode = "0";
    private const string SendAction = "sms.message.send";
    private const string VerificationTemplateId = "pub_verif_en_ttl";

    public async Task SendOtpAsync(
        string normalizedPhone,
        string code,
        Guid idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        var apiKey = smsOptions.Value.ApiKey!;
        var ttlMinutes = otpOptions.Value.CodeLifetimeMinutes;

        var requestUri = $"{ApiBaseUrl}?action={SendAction}&accessKeyId={Uri.EscapeDataString(apiKey)}";
        using var request = new HttpRequestMessage(HttpMethod.Post, requestUri);
        request.Content = JsonContent.Create(new SendSmsRequest
        {
            To = normalizedPhone,
            TemplateId = VerificationTemplateId,
            TemplateData = new Dictionary<string, string>
            {
                ["code"] = code,
                ["ttl"] = ttlMinutes.ToString(),
            },
        }, options: ApiJson.SerializerOptions);

        using var response = await httpClient.SendAsync(request, cancellationToken);
        var responseBody = await response.Content.ReadFromJsonAsync<UnimtxResponse>(
            ApiJson.SerializerOptions,
            cancellationToken);
        var firstMessage = responseBody?.Data?.Messages?.FirstOrDefault();

        if (response.IsSuccessStatusCode
            && responseBody is not null
            && responseBody.Code == SuccessCode)
        {
            logger.LogInformation(
                "OTP SMS accepted by Unimtx for {Phone} (idempotency {IdempotencyKey}, messageId {MessageId}, iso {Iso}, parts {Parts}, price {Price}).",
                normalizedPhone,
                idempotencyKey,
                firstMessage?.Id,
                firstMessage?.Iso,
                firstMessage?.Parts,
                firstMessage?.Price);
            return;
        }

        var errorCode = responseBody?.Code ?? "unknown";
        var errorMessage = responseBody?.Message ?? "No response body";
        logger.LogError(
            "Unimtx SMS send failed for {Phone} (idempotency {IdempotencyKey}): {ErrorCode} {ErrorMessage} (HTTP {StatusCode})",
            normalizedPhone,
            idempotencyKey,
            errorCode,
            errorMessage,
            (int)response.StatusCode);

        throw new HttpRequestException(
            $"Unimtx SMS send failed with code {errorCode}: {errorMessage}.");
    }

    private sealed class SendSmsRequest
    {
        public required string To { get; init; }

        public required string TemplateId { get; init; }

        public required Dictionary<string, string> TemplateData { get; init; }
    }

    private sealed class UnimtxResponse
    {
        public string? Code { get; init; }

        public string? Message { get; init; }

        public UnimtxResponseData? Data { get; init; }
    }

    private sealed class UnimtxResponseData
    {
        public UnimtxResponseMessage[]? Messages { get; init; }
    }

    private sealed class UnimtxResponseMessage
    {
        public string? Id { get; init; }

        public string? To { get; init; }

        public string? Iso { get; init; }

        public string? Cc { get; init; }

        public int? Parts { get; init; }

        public string? Price { get; init; }
    }
}
