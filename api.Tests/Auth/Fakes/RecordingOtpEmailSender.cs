using Amanah.Api.Services.External;

namespace Amanah.Api.Tests.Auth.Fakes;

public sealed class RecordingOtpEmailSender : IOtpEmailSender
{
    public List<(string Email, string Code, Guid IdempotencyKey)> SentMessages { get; } = [];

    public Task SendOtpAsync(
        string normalizedEmail,
        string code,
        Guid idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        if (SentMessages.Any(message => message.IdempotencyKey == idempotencyKey))
        {
            return Task.CompletedTask;
        }

        SentMessages.Add((normalizedEmail, code, idempotencyKey));
        return Task.CompletedTask;
    }
}
