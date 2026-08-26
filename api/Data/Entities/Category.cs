namespace Amanah.Api.Data.Entities;

public class Category : IEntity
{
    public Guid Id { get; set; }

    public required string Code { get; set; }

    public int SortOrder { get; set; }

    public bool PhotosPrivate { get; set; }

    public bool Active { get; set; }

    public ICollection<CategoryFieldDefinition> FieldDefinitions { get; set; } = [];

    public ICollection<Report> Reports { get; set; } = [];
}
