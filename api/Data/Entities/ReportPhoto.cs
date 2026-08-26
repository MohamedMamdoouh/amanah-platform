namespace Amanah.Api.Data.Entities;

public class ReportPhoto : IEntity
{
    public Guid Id { get; set; }

    public Guid ReportId { get; set; }

    public Report Report { get; set; } = null!;

    public required string StorageKey { get; set; }

    public required string ContentType { get; set; }

    public long SizeBytes { get; set; }

    public string? ThumbnailStorageKey { get; set; }

    public int SortOrder { get; set; }
}
