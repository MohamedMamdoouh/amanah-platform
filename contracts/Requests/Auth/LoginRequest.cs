namespace Amanah.Contracts.Requests.Auth;

public sealed class LoginRequest
{
    public string Phone { get; init; } = string.Empty;

    public string LoginToken { get; init; } = string.Empty;
}
