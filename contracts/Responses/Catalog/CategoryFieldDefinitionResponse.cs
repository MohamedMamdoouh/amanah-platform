namespace Amanah.Contracts.Responses.Catalog;

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
