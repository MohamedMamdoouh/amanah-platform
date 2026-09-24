namespace Amanah.Api.Options;

public sealed class EmailOptions
{
    public const string SectionName = "Email";

    public string? ApiKey { get; init; }

    public string? FromAddress { get; init; }

    public string? FromName { get; init; }

    public string? AdminAlertTo { get; init; }

    public string? AppBaseUrl { get; init; }

    public int OutboxPollIntervalSeconds { get; init; } = 30;

    public int OutboxMaxAttempts { get; init; } = 5;

    public int OutboxBatchSize { get; init; } = 10;

    public bool IsConfigured => !string.IsNullOrWhiteSpace(ApiKey);
}
