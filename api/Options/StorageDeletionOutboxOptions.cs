namespace Amanah.Api.Options;

public sealed class StorageDeletionOutboxOptions
{
    public const string SectionName = "StorageDeletion";

    public int PollIntervalSeconds { get; init; } = 10;

    public int MaxAttempts { get; init; } = 5;

    public int BatchSize { get; init; } = 50;
}
