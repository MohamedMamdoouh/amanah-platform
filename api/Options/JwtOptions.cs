namespace Amanah.Api.Options;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    public string AccessTokenSigningKey { get; init; } = string.Empty;

    public string HandoffTokenSigningKey { get; init; } = string.Empty;

    public int HandoffTokenLifetimeMinutes { get; init; } = 15;

    public int AccessTokenLifetimeMinutes { get; init; } = 15;

    public int RefreshTokenLifetimeDays { get; init; } = 30;
}
