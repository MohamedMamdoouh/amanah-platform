namespace Amanah.Contracts.Responses.Admin;

public sealed class AdminCategoryListResponse
{
    public IReadOnlyList<AdminCategoryResponse> Items { get; init; } = [];
}

public sealed class AdminCategoryResponse
{
    public required Guid Id { get; init; }

    public required string Code { get; init; }

    public int SortOrder { get; init; }

    public bool PhotosPrivate { get; init; }

    public bool IsActive { get; init; }

    public IReadOnlyList<AdminCategoryFieldDefinitionResponse> FieldDefinitions { get; init; } = [];
}

public sealed class AdminCategoryFieldDefinitionResponse
{
    public required Guid Id { get; init; }

    public required string FieldKey { get; init; }

    public required string Type { get; init; }

    public bool Required { get; init; }

    public int SortOrder { get; init; }

    public int? MinLength { get; init; }

    public int? MaxLength { get; init; }

    public int? MinInt { get; init; }

    public int? MaxInt { get; init; }

    public string? TextFormat { get; init; }
}
