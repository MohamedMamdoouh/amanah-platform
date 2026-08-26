namespace Amanah.Contracts.Responses.Catalog;

public sealed class CategoryResponse
{
    public required string Code { get; init; }

    public int SortOrder { get; init; }

    public bool PhotosPrivate { get; init; }

    public IReadOnlyList<CategoryFieldDefinitionResponse> FieldDefinitions { get; init; } = [];
}
