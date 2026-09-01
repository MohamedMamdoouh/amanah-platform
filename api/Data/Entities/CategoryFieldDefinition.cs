namespace Amanah.Api.Data.Entities;

public class CategoryFieldDefinition : IEntity
{
    public Guid Id { get; set; }

    public Guid CategoryId { get; set; }

    public Category Category { get; set; } = null!;

    public required string FieldKey { get; set; }

    public CategoryFieldType Type { get; set; }

    public int? MinLength { get; set; }

    public int? MaxLength { get; set; }

    public int? MinInt { get; set; }

    public int? MaxInt { get; set; }

    public bool Required { get; set; }

    public int SortOrder { get; set; }

    public CategoryTextFormat? TextFormat { get; set; }
}
