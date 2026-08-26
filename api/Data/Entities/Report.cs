namespace Amanah.Api.Data.Entities;

public class Report : IEntity
{
    public Guid Id { get; set; }

    public Guid ReporterId { get; set; }

    public User Reporter { get; set; } = null!;

    public ReportType Type { get; set; }

    public Guid CategoryId { get; set; }

    public Category Category { get; set; } = null!;

    public required string Title { get; set; }

    public required string Description { get; set; }

    public DateOnly DateLostOrFound { get; set; }

    public Guid GovernorateId { get; set; }

    public Governorate Governorate { get; set; } = null!;

    public string? AreaText { get; set; }

    public string? ItemHeldLocation { get; set; }

    public ReportStatus Status { get; set; }

    public bool HasReward { get; set; }

    public int? RewardAmount { get; set; }

    public required string HiddenDetail { get; set; }

    public string? WithdrawalReason { get; set; }

    public int ResubmissionCount { get; set; }

    public string? NormalizedSearchText { get; set; }

    public DateTimeOffset? PublishedAt { get; set; }

    public int PublishedSecondsElapsed { get; set; }

    public DateTimeOffset? PublishedTimerResumedAt { get; set; }

    public bool ExpiryWarningSent { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public ICollection<CategoryField> CategoryFields { get; set; } = [];

    public ICollection<ReportPhoto> Photos { get; set; } = [];

    public ICollection<Claim> Claims { get; set; } = [];

    public Resolution? Resolution { get; set; }

    public ICollection<AbuseReport> AbuseReports { get; set; } = [];

    public ICollection<ModerationAction> ModerationActions { get; set; } = [];
}
