namespace Amanah.Api.Options;

public sealed class LifecycleOptions
{
    public const string SectionName = "Lifecycle";

    public int ListingExpiryDays { get; set; } = 90;

    public int ListingExpiryWarningDaysBefore { get; set; } = 7;

    public int ClaimTimeoutMinutes { get; set; } = 10 * 24 * 60;

    public int RetentionDays { get; set; } = 30;

    public int JobsPollIntervalSeconds { get; set; } = 3600;

    public int ListingExpiryWarningDays =>
        ListingExpiryDays - ListingExpiryWarningDaysBefore;
}
