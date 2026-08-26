namespace Amanah.Api.Data.Entities;

public class AbuseReport : IEntity
{
    public Guid Id { get; set; }

    public Guid ReporterId { get; set; }

    public User Reporter { get; set; } = null!;

    public Guid ReportId { get; set; }

    public Report Report { get; set; } = null!;

    public required string Reason { get; set; }

    public string? Note { get; set; }

    public AbuseReportStatus Status { get; set; }

    public string? ResolutionOutcome { get; set; }

    public Guid? ResolvedByUserId { get; set; }

    public User? ResolvedByUser { get; set; }

    public DateTimeOffset? ResolvedAt { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}
