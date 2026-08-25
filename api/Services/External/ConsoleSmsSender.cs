namespace Amanah.Api.Services.External;

public sealed class ConsoleSmsSender(ILogger<ConsoleSmsSender> logger) : ISmsSender
{
    public Task SendOtpAsync(
        string normalizedPhone,
        string code,
        Guid idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        logger.LogInformation(
            "[SMS] OTP for {Phone}: {Code} (idempotency {IdempotencyKey})",
            normalizedPhone,
            code,
            idempotencyKey);

        return Task.CompletedTask;
    }
}
