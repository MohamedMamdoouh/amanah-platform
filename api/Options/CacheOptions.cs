namespace Amanah.Api.Options;

public sealed class CacheOptions
{
    public const string SectionName = "Cache";

    public int CategoriesTtlSeconds { get; init; } = 3600;

    public int GovernoratesTtlSeconds { get; init; } = 86400;
}
