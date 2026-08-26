namespace Amanah.Contracts.Requests.Auth;

public sealed class RefreshRequest
{
    public string RefreshToken { get; init; } = string.Empty;
}
