namespace Amanah.Api.Data.Entities;

public class AdminAlertEmailOutboxMessage : IEntity
{
    public Guid Id { get; set; }

    public Guid ReportId { get; set; }

    public Report? Report { get; set; }

    public required string ReportType { get; set; }

    public required string CategoryCode { get; set; }

    public AdminAlertEmailOutboxStatus Status { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? ProcessedAt { get; set; }

    public int AttemptCount { get; set; }

    public string? LastError { get; set; }
}
