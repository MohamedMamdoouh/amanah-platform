namespace Amanah.Contracts.Requests.Claims;

public sealed class MyClaimsQuery
{
    public int Page { get; init; } = 1;

    public int PageSize { get; init; } = 20;
}
