namespace Amanah.Api.Services.External;

public interface ISmsSender
{
    Task SendOtpAsync(
        string normalizedPhone,
        string code,
        Guid idempotencyKey,
        CancellationToken cancellationToken = default);
}
