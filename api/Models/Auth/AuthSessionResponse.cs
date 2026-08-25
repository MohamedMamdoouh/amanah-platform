namespace Amanah.Api.Models.Auth;

public sealed class AuthSessionResponse
{
    public required string AccessToken { get; init; }

    public required string RefreshToken { get; init; }

    public required UserProfileResponse User { get; init; }
}
