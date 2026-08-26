namespace Amanah.Contracts.Requests.Auth;

public sealed class VerifyOtpRequest
{
    public string Phone { get; init; } = string.Empty;

    public string Code { get; init; } = string.Empty;
}
