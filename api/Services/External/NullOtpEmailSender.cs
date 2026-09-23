namespace Amanah.Api.Services.External;

public sealed class NullOtpEmailSender : IOtpEmailSender
{
    public Task SendOtpAsync(
        string normalizedEmail,
        string code,
        Guid idempotencyKey,
        CancellationToken cancellationToken = default) =>
        throw new InvalidOperationException("Email OTP is not configured.");
}
