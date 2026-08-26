namespace Amanah.Api.Data.Entities;

public class CategoryField : IEntity
{
    public Guid Id { get; set; }

    public Guid ReportId { get; set; }

    public Report Report { get; set; } = null!;

    public required string FieldKey { get; set; }

    public required string Value { get; set; }
}
