namespace Amanah.Api.Data.Entities;

public class OtpSmsOutboxMessage : IEntity
{
    public Guid Id { get; set; }

    public Guid? OtpCodeId { get; set; }

    public OtpCode? OtpCode { get; set; }

    public required string Phone { get; set; }

    public required string ProtectedPayload { get; set; }

    public OtpSmsOutboxStatus Status { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? ProcessedAt { get; set; }

    public int AttemptCount { get; set; }

    public string? LastError { get; set; }
}
