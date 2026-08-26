namespace Amanah.Api.Data.Entities;

public class Resolution : IEntity
{
    public Guid Id { get; set; }

    public Guid ReportId { get; set; }

    public Report Report { get; set; } = null!;

    public DateTimeOffset? ReporterConfirmedAt { get; set; }

    public DateTimeOffset? ClaimantConfirmedAt { get; set; }

    public DateTimeOffset? ResolvedAt { get; set; }
}
