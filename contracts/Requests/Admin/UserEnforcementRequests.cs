namespace Amanah.Contracts.Requests.Admin;

public sealed class AdminUserSearchQuery
{
    public string Query { get; init; } = string.Empty;
}

public sealed class BanUserRequest
{
    public string Reason { get; init; } = string.Empty;
}
