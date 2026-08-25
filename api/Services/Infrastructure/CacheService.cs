using Microsoft.Extensions.Caching.Hybrid;

namespace Amanah.Api.Services.Infrastructure;

public sealed class CacheService(HybridCache hybridCache, ILogger<CacheService> logger) : ICacheService
{
    public async Task<T> GetOrSetAsync<T>(
        string key,
        Func<CancellationToken, Task<T>> factory,
        TimeSpan ttl,
        CancellationToken cancellationToken = default)
    {
        var entryOptions = new HybridCacheEntryOptions
        {
            Expiration = ttl,
            LocalCacheExpiration = ttl,
        };

        var factoryInvoked = false;

        try
        {
            var value = await hybridCache.GetOrCreateAsync(
                key,
                async ct =>
                {
                    factoryInvoked = true;
                    logger.LogDebug("Cache miss {CacheKey}", key);
                    return await factory(ct);
                },
                entryOptions,
                cancellationToken: cancellationToken);

            if (!factoryInvoked)
            {
                logger.LogDebug("Cache hit {CacheKey}", key);
            }

            return value;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Cache read failed for {CacheKey}; falling back to factory", key);
            return await factory(cancellationToken);
        }
    }

    public async Task RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        try
        {
            await hybridCache.RemoveAsync(key, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Cache remove failed for {CacheKey}", key);
        }
    }
}
