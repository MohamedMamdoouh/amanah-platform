namespace Amanah.Contracts.Responses.Catalog;

public sealed class CategoryListResponse
{
    public IReadOnlyList<CategoryResponse> Items { get; init; } = [];
}
