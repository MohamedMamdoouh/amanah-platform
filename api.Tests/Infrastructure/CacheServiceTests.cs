using Amanah.Api.Services.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace Amanah.Api.Tests.Infrastructure;

public class CacheServiceTests
{
    private static ICacheService CreateCacheService()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDistributedMemoryCache();
        services.AddHybridCache();
        services.AddSingleton<ICacheService, CacheService>();
        return services.BuildServiceProvider().GetRequiredService<ICacheService>();
    }

    [Fact]
    public async Task GetOrSetAsync_returns_cached_value_without_calling_factory_again()
    {
        var cache = CreateCacheService();
        var factoryCalls = 0;

        async Task<string> Factory(CancellationToken _) =>
            await Task.FromResult($"{++factoryCalls}");

        var first = await cache.GetOrSetAsync("test:key", Factory, TimeSpan.FromMinutes(1));
        var second = await cache.GetOrSetAsync("test:key", Factory, TimeSpan.FromMinutes(1));

        Assert.Equal("1", first);
        Assert.Equal("1", second);
        Assert.Equal(1, factoryCalls);
    }

    [Fact]
    public async Task RemoveAsync_clears_cached_value()
    {
        var cache = CreateCacheService();
        var factoryCalls = 0;

        async Task<int> Factory(CancellationToken _) =>
            await Task.FromResult(++factoryCalls);

        await cache.GetOrSetAsync("test:remove", Factory, TimeSpan.FromMinutes(1));
        await cache.RemoveAsync("test:remove");
        var value = await cache.GetOrSetAsync("test:remove", Factory, TimeSpan.FromMinutes(1));

        Assert.Equal(2, value);
        Assert.Equal(2, factoryCalls);
    }
}
