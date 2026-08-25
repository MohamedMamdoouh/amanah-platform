namespace Amanah.Api.Options;

public class RateLimitOptions
{
    public const string SectionName = "RateLimit";

    public Dictionary<string, RateLimitPolicyOptions> Policies { get; set; } = [];
}

public class RateLimitPolicyOptions
{
    public int WindowSeconds { get; set; } = 60;

    public int PermitLimit { get; set; } = 100;
}
