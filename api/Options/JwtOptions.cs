namespace Amanah.Api.Options;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    public string SigningKey { get; init; } = string.Empty;

    public int HandoffTokenLifetimeMinutes { get; init; } = 15;
}
