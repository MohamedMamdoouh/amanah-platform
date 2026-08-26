namespace Amanah.Contracts.Responses.Auth;

public sealed class UserProfileResponse
{
    public required Guid Id { get; init; }

    public required string DisplayName { get; init; }

    public required string Role { get; init; }

    public required string Phone { get; init; }
}
