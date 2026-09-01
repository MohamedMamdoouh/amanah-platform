using System.Net.Http.Json;
using Amanah.Api.Data;
using Amanah.Api.Data.Seeds;
using Amanah.Api.Options;
using Amanah.Api.Services.Catalog;
using Amanah.Api.Services.Infrastructure;
using Amanah.Api.Tests.Infrastructure;
using Amanah.Contracts.Responses.Catalog;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Amanah.Api.Tests.Catalog;

public class CatalogApiTests(ApiWebApplicationFactory factory) : IClassFixture<ApiWebApplicationFactory>
{
    [Fact]
    public async Task Get_categories_returns_active_seeded_categories_with_field_definitions()
    {
        await using var scope = await CreateSeededScopeAsync();
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/v1/categories");
        var body = await response.Content.ReadFromJsonAsync<CategoryListResponse>();

        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body);
        Assert.Equal(8, body.Items.Count);
        Assert.Equal(
            [
                "phones",
                "documents-ids",
                "wallets",
                "keys",
                "bags",
                "electronics",
                "accessories",
                "other",
            ],
            body.Items.Select(category => category.Code));

        var phones = body.Items.Single(category => category.Code == "phones");
        Assert.False(phones.PhotosPrivate);
        Assert.Equal(2, phones.FieldDefinitions.Count);
        Assert.Equal("brand_model", phones.FieldDefinitions[0].FieldKey);
        Assert.Equal("text", phones.FieldDefinitions[0].Type);
        Assert.True(phones.FieldDefinitions[0].Required);
        Assert.Equal(2, phones.FieldDefinitions[0].MinLength);
        Assert.Equal(80, phones.FieldDefinitions[0].MaxLength);

        var documents = body.Items.Single(category => category.Code == "documents-ids");
        Assert.True(documents.PhotosPrivate);
        var firstNameField = documents.FieldDefinitions.Single(field => field.FieldKey == "first_name_on_document");
        Assert.Equal("letters_and_spaces", firstNameField.TextFormat);
        Assert.Null(documents.FieldDefinitions.Single(field => field.FieldKey == "document_type").TextFormat);

        var keys = body.Items.Single(category => category.Code == "keys");
        var keyCount = keys.FieldDefinitions.Single(field => field.FieldKey == "key_count");
        Assert.Equal("integer", keyCount.Type);
        Assert.Equal(1, keyCount.MinInt);
        Assert.Equal(20, keyCount.MaxInt);
    }

    [Fact]
    public async Task Get_categories_excludes_inactive_categories()
    {
        await using var scope = await CreateSeededScopeAsync();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var inactive = await context.Categories.FindAsync(
            context.Categories.Single(category => category.Code == "other").Id);
        inactive!.Active = false;
        await context.SaveChangesAsync();

        await scope.ServiceProvider.GetRequiredService<ICacheService>()
            .RemoveAsync(CacheKeys.Categories);

        var client = factory.CreateClient();
        var body = await client.GetFromJsonAsync<CategoryListResponse>("/api/v1/categories");

        Assert.NotNull(body);
        Assert.Equal(7, body.Items.Count);
        Assert.DoesNotContain(body.Items, category => category.Code == "other");
    }

    [Fact]
    public async Task Get_governorates_returns_twenty_seven_seeded_governorates()
    {
        await using var scope = await CreateSeededScopeAsync();
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/v1/governorates");
        var body = await response.Content.ReadFromJsonAsync<GovernorateListResponse>();

        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body);
        Assert.Equal(27, body.Items.Count);
        Assert.Equal(1, body.Items[0].SortOrder);
        Assert.Equal(27, body.Items[^1].SortOrder);
        Assert.All(body.Items, governorate =>
        {
            Assert.False(string.IsNullOrWhiteSpace(governorate.Code));
        });
    }

    [Fact]
    public async Task Get_categories_uses_cache_on_second_request()
    {
        await using var scope = await CreateSeededScopeAsync();
        var countingLoader = scope.ServiceProvider.GetRequiredService<CountingCategoryLoader>();
        var catalogService = scope.ServiceProvider.GetRequiredService<CatalogService>();

        await catalogService.GetCategoriesAsync();
        await catalogService.GetCategoriesAsync();

        Assert.Equal(1, countingLoader.LoadCount);
    }

    [Fact]
    public async Task Get_categories_reloads_after_cache_invalidation_via_integration_scope()
    {
        await using var scope = await CreateSeededScopeAsync();
        var countingLoader = scope.ServiceProvider.GetRequiredService<CountingCategoryLoader>();
        var catalogService = scope.ServiceProvider.GetRequiredService<CatalogService>();
        var cacheService = scope.ServiceProvider.GetRequiredService<ICacheService>();

        await catalogService.GetCategoriesAsync();
        await cacheService.RemoveAsync(CacheKeys.Categories);
        await catalogService.GetCategoriesAsync();

        Assert.Equal(2, countingLoader.LoadCount);
    }

    private async Task<AsyncServiceScope> CreateSeededScopeAsync()
    {
        var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await context.Database.MigrateAsync();
        await scope.ServiceProvider.GetRequiredService<CatalogSeeder>().SeedAsync();
        return scope;
    }
}

public class CatalogServiceCacheTests
{
    [Fact]
    public async Task GetCategoriesAsync_calls_loader_once_when_cached()
    {
        var loader = new StubLoaders();
        var service = CreateCatalogService(loader);

        await service.GetCategoriesAsync();
        await service.GetCategoriesAsync();

        Assert.Equal(1, loader.CategoriesLoadCount);
    }

    [Fact]
    public async Task GetGovernoratesAsync_calls_loader_once_when_cached()
    {
        var loader = new StubLoaders();
        var service = CreateCatalogService(loader);

        await service.GetGovernoratesAsync();
        await service.GetGovernoratesAsync();

        Assert.Equal(1, loader.GovernoratesLoadCount);
    }

    [Fact]
    public async Task GetCategoriesAsync_reloads_after_cache_invalidation()
    {
        var loader = new StubLoaders();
        var cache = CreateCacheService();
        var service = CreateCatalogService(loader, cache);

        await service.GetCategoriesAsync();
        await cache.RemoveAsync(CacheKeys.Categories);
        await service.GetCategoriesAsync();

        Assert.Equal(2, loader.CategoriesLoadCount);
    }

    private static CatalogService CreateCatalogService(StubLoaders loaders, ICacheService? cache = null)
    {
        cache ??= CreateCacheService();
        var options = Microsoft.Extensions.Options.Options.Create(new CacheOptions
        {
            CategoriesTtlSeconds = 3600,
            GovernoratesTtlSeconds = 86400,
        });

        return new CatalogService(loaders, loaders, cache, options);
    }

    private static ICacheService CreateCacheService()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDistributedMemoryCache();
        services.AddHybridCache();
        services.AddSingleton<ICacheService, CacheService>();
        return services.BuildServiceProvider().GetRequiredService<ICacheService>();
    }

    private sealed class StubLoaders : ICategoryLoader, IGovernorateLoader
    {
        public int CategoriesLoadCount { get; private set; }

        public int GovernoratesLoadCount { get; private set; }

        public Task<CategoryListResponse> LoadCategoriesAsync(CancellationToken cancellationToken = default)
        {
            CategoriesLoadCount++;
            return Task.FromResult(new CategoryListResponse
            {
                Items =
                [
                    new CategoryResponse
                    {
                        Code = "phones",
                        SortOrder = 1,
                    },
                ],
            });
        }

        public Task<GovernorateListResponse> LoadGovernoratesAsync(CancellationToken cancellationToken = default)
        {
            GovernoratesLoadCount++;
            return Task.FromResult(new GovernorateListResponse
            {
                Items =
                [
                    new GovernorateResponse
                    {
                        Code = "cairo",
                        SortOrder = 1,
                    },
                ],
            });
        }
    }
}

public sealed class CountingCategoryLoader(ICategoryLoader inner) : ICategoryLoader
{
    public int LoadCount { get; private set; }

    public async Task<CategoryListResponse> LoadCategoriesAsync(CancellationToken cancellationToken = default)
    {
        LoadCount++;
        return await inner.LoadCategoriesAsync(cancellationToken);
    }
}
