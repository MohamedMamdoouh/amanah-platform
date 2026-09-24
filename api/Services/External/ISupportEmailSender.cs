namespace Amanah.Api.Services.External;

public interface ISupportEmailSender
{
    Task SendSupportMessageAsync(
        string normalizedReplyEmail,
        string displayName,
        string message,
        CancellationToken cancellationToken = default);
}
