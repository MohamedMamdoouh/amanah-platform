using Amanah.Api.Services.External.Email;

namespace Amanah.Api.Services.External;

public sealed class ConsoleSupportEmailSender(ILogger<ConsoleSupportEmailSender> logger) : ISupportEmailSender
{
    public Task SendSupportMessageAsync(
        string normalizedReplyEmail,
        string displayName,
        string message,
        CancellationToken cancellationToken = default)
    {
        var plainText = SupportEmailTemplates.BuildPlainText(
            displayName,
            normalizedReplyEmail,
            message);

        logger.LogInformation(
            "[Email] Support message from {ReplyEmail} ({DisplayName}): {PlainText}",
            normalizedReplyEmail,
            displayName,
            plainText);

        return Task.CompletedTask;
    }
}
