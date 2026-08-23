namespace Amanah.Api.Middleware;

public class RateLimitOptions
{
    public const string SectionName = "RateLimit";

    public int WindowSeconds { get; set; } = 60;

    public int PermitLimit { get; set; } = 100;
}
