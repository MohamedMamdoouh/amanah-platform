namespace Amanah.Api.Data.Entities;

public class Governorate : IEntity
{
    public Guid Id { get; set; }

    public required string Code { get; set; }

    public int SortOrder { get; set; }

    public ICollection<Report> Reports { get; set; } = [];
}
