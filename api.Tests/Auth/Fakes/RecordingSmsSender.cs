using Amanah.Api.Services.External;

namespace Amanah.Api.Tests.Auth.Fakes;

public sealed class RecordingSmsSender : ISmsSender
{
    public List<(string Phone, string Code, Guid IdempotencyKey)> SentMessages { get; } = [];

    public bool ShouldThrow { get; set; }

    public bool ShouldTimeout { get; set; }

    public Task SendOtpAsync(
        string normalizedPhone,
        string code,
        Guid idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        if (SentMessages.Any(message => message.IdempotencyKey == idempotencyKey))
        {
            return Task.CompletedTask;
        }

        if (ShouldTimeout)
        {
            throw new TimeoutException("SMS provider timed out.");
        }

        if (ShouldThrow)
        {
            throw new HttpRequestException("SMS provider unavailable.");
        }

        SentMessages.Add((normalizedPhone, code, idempotencyKey));
        return Task.CompletedTask;
    }
}
