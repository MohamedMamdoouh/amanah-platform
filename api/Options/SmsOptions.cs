namespace Amanah.Api.Options;

public sealed class SmsOptions
{
    public const string SectionName = "Sms";

    public string? ApiKey { get; init; }

    public string? SenderId { get; init; }

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(ApiKey)
        && !string.IsNullOrWhiteSpace(SenderId);
}
