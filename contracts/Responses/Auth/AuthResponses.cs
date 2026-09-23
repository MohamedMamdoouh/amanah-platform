namespace Amanah.Contracts.Responses.Auth;

public sealed class VerifyOtpResponse
{
    public required VerifyOtpStatus Status { get; init; }

    public string? SignupToken { get; init; }

    public string? ResetToken { get; init; }
}

public sealed class AuthSessionResponse
{
    public required string AccessToken { get; init; }

    public required UserProfileResponse User { get; init; }
}

public sealed class UserProfileResponse
{
    public required Guid Id { get; init; }

    public required string DisplayName { get; init; }

    public required string Role { get; init; }

    public string? Phone { get; init; }

    public string? Email { get; init; }

    public bool RequiresAccountReactivation { get; init; }
}
