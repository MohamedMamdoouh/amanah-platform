namespace Amanah.Api.Models.Auth;

public sealed class RefreshRequest
{
    public string RefreshToken { get; init; } = string.Empty;
}
