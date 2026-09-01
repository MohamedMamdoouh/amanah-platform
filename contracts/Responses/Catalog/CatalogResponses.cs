namespace Amanah.Contracts.Responses.Catalog;

public sealed class CategoryListResponse
{
    public IReadOnlyList<CategoryResponse> Items { get; init; } = [];
}

public sealed class CategoryResponse
{
    public required string Code { get; init; }

    public int SortOrder { get; init; }

    public bool PhotosPrivate { get; init; }

    public IReadOnlyList<CategoryFieldDefinitionResponse> FieldDefinitions { get; init; } = [];
}

public sealed class CategoryFieldDefinitionResponse
{
    public required string FieldKey { get; init; }

    public required string Type { get; init; }

    public bool Required { get; init; }

    public int SortOrder { get; init; }

    public int? MinLength { get; init; }

    public int? MaxLength { get; init; }

    public int? MinInt { get; init; }

    public int? MaxInt { get; init; }
}

public sealed class GovernorateListResponse
{
    public IReadOnlyList<GovernorateResponse> Items { get; init; } = [];
}

public sealed class GovernorateResponse
{
    public required string Code { get; init; }

    public int SortOrder { get; init; }
}
