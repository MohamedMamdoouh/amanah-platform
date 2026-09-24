namespace Amanah.Api.Services.External;

public sealed class NullSupportEmailSender : ISupportEmailSender
{
    public Task SendSupportMessageAsync(
        string normalizedReplyEmail,
        string displayName,
        string message,
        CancellationToken cancellationToken = default) =>
        Task.CompletedTask;
}
