using Amanah.Api.Options;
using Amanah.Api.Services.Infrastructure;
using Amanah.Contracts.Responses.Catalog;
using Microsoft.Extensions.Options;

namespace Amanah.Api.Services.Catalog;

public sealed class CatalogService(
    ICategoryLoader categoryLoader,
    IGovernorateLoader governorateLoader,
    ICacheService cacheService,
    IOptions<CacheOptions> cacheOptions)
{
    private readonly CacheOptions _cacheOptions = cacheOptions.Value;

    public Task<CategoryListResponse> GetCategoriesAsync(CancellationToken cancellationToken = default) =>
        cacheService.GetOrSetAsync(
            CacheKeys.Categories,
            categoryLoader.LoadCategoriesAsync,
            TimeSpan.FromSeconds(_cacheOptions.CategoriesTtlSeconds),
            cancellationToken);

    public Task<GovernorateListResponse> GetGovernoratesAsync(CancellationToken cancellationToken = default) =>
        cacheService.GetOrSetAsync(
            CacheKeys.Governorates,
            governorateLoader.LoadGovernoratesAsync,
            TimeSpan.FromSeconds(_cacheOptions.GovernoratesTtlSeconds),
            cancellationToken);
}

public static class CatalogServiceCollectionExtensions
{
    public static IServiceCollection AddCatalogServices(this IServiceCollection services)
    {
        services.AddScoped<ICategoryLoader, CategoryLoader>();
        services.AddScoped<IGovernorateLoader, GovernorateLoader>();
        services.AddScoped<CatalogService>();

        return services;
    }
}
