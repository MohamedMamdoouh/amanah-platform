namespace Amanah.Contracts.Responses.Catalog;

public sealed class GovernorateListResponse
{
    public IReadOnlyList<GovernorateResponse> Items { get; init; } = [];
}
