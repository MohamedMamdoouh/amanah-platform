namespace Amanah.Api.Options;

public sealed class OtpOptions
{
    public const string SectionName = "Otp";

    public int CooldownSeconds { get; init; } = 120;

    public int HourlySendLimit { get; init; } = 2;

    public int DailySendLimit { get; init; } = 3;

    public int CodeLifetimeMinutes { get; init; } = 10;

    public int MaxVerificationAttempts { get; init; } = 3;

    public int OutboxPollIntervalSeconds { get; init; } = 30;

    public int OutboxMaxAttempts { get; init; } = 5;

    public int OutboxBatchSize { get; init; } = 10;
}
