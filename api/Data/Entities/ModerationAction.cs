namespace Amanah.Api.Data.Entities;

public class ModerationAction : IEntity
{
    public Guid Id { get; set; }

    public Guid? ReportId { get; set; }

    public Report? Report { get; set; }

    public Guid AdminId { get; set; }

    public User Admin { get; set; } = null!;

    public ModerationDecision Decision { get; set; }

    public string? ReasonCode { get; set; }

    public string? Note { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}
