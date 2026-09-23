namespace Amanah.Contracts.Requests.Account;

public sealed class SendLinkIdentifierOtpRequest
{
    public string Channel { get; init; } = string.Empty;

    public string Identifier { get; init; } = string.Empty;

    public string CaptchaToken { get; init; } = string.Empty;
}

public sealed class VerifyLinkIdentifierOtpRequest
{
    public string Channel { get; init; } = string.Empty;

    public string Identifier { get; init; } = string.Empty;

    public string Code { get; init; } = string.Empty;
}
