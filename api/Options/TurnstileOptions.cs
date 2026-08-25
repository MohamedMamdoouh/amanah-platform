namespace Amanah.Api.Options;

public sealed class TurnstileOptions
{
    public const string SectionName = "Turnstile";

    public string? SecretKey { get; init; }
}
