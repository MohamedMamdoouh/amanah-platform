namespace Amanah.Contracts.Requests.Support;

public sealed class SubmitSupportMessageRequest
{
    public string DisplayName { get; init; } = string.Empty;

    public string ReplyEmail { get; init; } = string.Empty;

    public string Message { get; init; } = string.Empty;

    public string CaptchaToken { get; init; } = string.Empty;
}
