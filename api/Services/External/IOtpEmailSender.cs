namespace Amanah.Api.Services.External;

public interface IOtpEmailSender
{
    Task SendOtpAsync(
        string normalizedEmail,
        string code,
        Guid idempotencyKey,
        CancellationToken cancellationToken = default);
}
