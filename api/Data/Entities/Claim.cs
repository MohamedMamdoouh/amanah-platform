namespace Amanah.Api.Data.Entities;

public class Claim : IEntity
{
    public Guid Id { get; set; }

    public Guid ReportId { get; set; }

    public Report Report { get; set; } = null!;

    public Guid ClaimantId { get; set; }

    public User Claimant { get; set; } = null!;

    public ClaimStatus Status { get; set; }

    public required string SubmittedAnswer { get; set; }

    public string? PhotoStorageKey { get; set; }

    public DateTimeOffset SubmittedAt { get; set; }

    public string? DecisionReason { get; set; }

    public string? ReviewerDecision { get; set; }

    public DateTimeOffset? ReviewedAt { get; set; }

    public Guid? CancelledByUserId { get; set; }

    public User? CancelledByUser { get; set; }

    public int AttemptNumber { get; set; }

    public bool CountsAsFailure { get; set; }

    public ChatThread? ChatThread { get; set; }
}
