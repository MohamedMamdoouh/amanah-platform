using System.Net.Http.Headers;
using Amanah.Api.Models.Common;
using Amanah.Api.Options;
using Microsoft.Extensions.Options;

namespace Amanah.Api.Services.External;

public sealed class EsmsAfricaSmsSender(
    HttpClient httpClient,
    IOptions<SmsOptions> options,
    ILogger<EsmsAfricaSmsSender> logger) : ISmsSender
{
    private const string SendEndpoint = "https://sms.esmsafrica.io/api/messages/send";

    public async Task SendOtpAsync(
        string normalizedPhone,
        string code,
        Guid idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        var smsOptions = options.Value;
        var apiKey = smsOptions.ApiKey!;
        var senderId = smsOptions.SenderId!;

        using var request = new HttpRequestMessage(HttpMethod.Post, SendEndpoint);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        request.Content = JsonContent.Create(new SendSmsRequest
        {
            To = normalizedPhone,
            Text = $"رمز التحقق من أمانة: {code}",
            SenderId = senderId,
        }, options: ApiJson.SnakeCaseSerializerOptions);

        using var response = await httpClient.SendAsync(request, cancellationToken);
        if (response.IsSuccessStatusCode)
        {
            logger.LogInformation(
                "OTP SMS sent to {Phone} (idempotency {IdempotencyKey}).",
                normalizedPhone,
                idempotencyKey);
            return;
        }

        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
        logger.LogError(
            "eSMS Africa SMS failed for {Phone} (idempotency {IdempotencyKey}): {StatusCode} {ResponseBody}",
            normalizedPhone,
            idempotencyKey,
            (int)response.StatusCode,
            responseBody);

        throw new HttpRequestException(
            $"eSMS Africa SMS request failed with status {(int)response.StatusCode}.");
    }

    private sealed class SendSmsRequest
    {
        public required string To { get; init; }

        public required string Text { get; init; }

        public required string SenderId { get; init; }
    }
}
