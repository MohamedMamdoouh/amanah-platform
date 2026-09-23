namespace Amanah.Contracts.Responses.Admin;

public sealed class AdminUserListResponse
{
    public IReadOnlyList<AdminUserSummaryResponse> Items { get; init; } = [];
}

public sealed class AdminUserSummaryResponse
{
    public required Guid Id { get; init; }

    public required string DisplayName { get; init; }
}

public sealed class AdminUserDetailResponse
{
    public required Guid Id { get; init; }

    public required string DisplayName { get; init; }

    public string? NormalizedPhone { get; init; }

    public string? NormalizedEmail { get; init; }

    public required string Role { get; init; }

    public bool IsBanned { get; init; }

    public string? BanReason { get; init; }

    public DateTimeOffset? BannedAt { get; init; }

    public int ReportsCount { get; init; }

    public DateTimeOffset CreatedAt { get; init; }
}

public sealed class BanUserResponse
{
    public required Guid UserId { get; init; }

    public required string BanReason { get; init; }

    public DateTimeOffset BannedAt { get; init; }
}

public sealed class UnbanUserResponse
{
    public required Guid UserId { get; init; }
}
