namespace Amanah.Contracts.Requests.Admin;

public sealed class CreateCategoryRequest
{
    public string Code { get; init; } = string.Empty;

    public int SortOrder { get; init; }

    public bool PhotosPrivate { get; init; }

    public bool IsActive { get; init; } = true;
}

public sealed class UpdateCategoryRequest
{
    public string Code { get; init; } = string.Empty;

    public int SortOrder { get; init; }

    public bool PhotosPrivate { get; init; }

    public bool IsActive { get; init; }
}

public sealed class CreateCategoryFieldRequest
{
    public string FieldKey { get; init; } = string.Empty;

    public string Type { get; init; } = string.Empty;

    public bool Required { get; init; }

    public int SortOrder { get; init; }

    public int? MinLength { get; init; }

    public int? MaxLength { get; init; }

    public int? MinInt { get; init; }

    public int? MaxInt { get; init; }

    public string? TextFormat { get; init; }
}

public sealed class UpdateCategoryFieldRequest
{
    public string FieldKey { get; init; } = string.Empty;

    public string Type { get; init; } = string.Empty;

    public bool Required { get; init; }

    public int SortOrder { get; init; }

    public int? MinLength { get; init; }

    public int? MaxLength { get; init; }

    public int? MinInt { get; init; }

    public int? MaxInt { get; init; }

    public string? TextFormat { get; init; }
}
