using System.Net.Http.Json;
using Amanah.Api.Data;
using Amanah.Api.Data.Entities;
using Amanah.Api.Data.Seeds;
using Amanah.Api.Options;
using Amanah.Api.Services.Catalog;
using Amanah.Api.Services.Infrastructure;
using Amanah.Api.Tests.Infrastructure;
using Amanah.Contracts.Responses.Catalog;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

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

        var cacheService = scope.ServiceProvider.GetRequiredService<ICacheService>();
        await cacheService.RemoveAsync(CacheKeys.Categories);

        try
        {
            var client = factory.CreateClient();
            var body = await client.GetFromJsonAsync<CategoryListResponse>("/api/v1/categories");

            Assert.NotNull(body);
            Assert.Equal(7, body.Items.Count);
            Assert.DoesNotContain(body.Items, category => category.Code == "other");
        }
        finally
        {
            inactive.Active = true;
            await context.SaveChangesAsync();
            await cacheService.RemoveAsync(CacheKeys.Categories);
        }
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
        await using var context = CreateContext();
        var observingCache = CreateObservingCacheService();
        var service = CreateCatalogService(context, observingCache);

        await service.GetCategoriesAsync();
        await service.GetCategoriesAsync();

        Assert.Equal(1, observingCache.CategoriesFactoryCalls);
    }

    [Fact]
    public async Task GetGovernoratesAsync_calls_loader_once_when_cached()
    {
        await using var context = CreateContext();
        var observingCache = CreateObservingCacheService();
        var service = CreateCatalogService(context, observingCache);

        await service.GetGovernoratesAsync();
        await service.GetGovernoratesAsync();

        Assert.Equal(1, observingCache.GovernoratesFactoryCalls);
    }

    [Fact]
    public async Task GetCategoriesAsync_reloads_after_cache_invalidation()
    {
        await using var context = CreateContext();
        var observingCache = CreateObservingCacheService();
        var service = CreateCatalogService(context, observingCache);

        await service.GetCategoriesAsync();
        await observingCache.RemoveAsync(CacheKeys.Categories);
        await service.GetCategoriesAsync();

        Assert.Equal(2, observingCache.CategoriesFactoryCalls);
    }

    private static CatalogService CreateCatalogService(AppDbContext context, ObservingCacheService cache)
    {
        var options = Microsoft.Extensions.Options.Options.Create(new CacheOptions
        {
            CategoriesTtlSeconds = 3600,
            GovernoratesTtlSeconds = 86400,
        });

        return new CatalogService(context, cache, options);
    }

    private static ObservingCacheService CreateObservingCacheService()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDistributedMemoryCache();
        services.AddHybridCache();
        services.AddSingleton<ICacheService, CacheService>();
        var inner = services.BuildServiceProvider().GetRequiredService<ICacheService>();
        return new ObservingCacheService(inner);
    }

    private static AppDbContext CreateContext()
    {
        var databaseName = Guid.NewGuid().ToString();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName)
            .Options;

        var context = new AppDbContext(options);
        var categoryId = Guid.NewGuid();
        var governorateId = Guid.NewGuid();

        context.Categories.Add(new Category
        {
            Id = categoryId,
            Code = "phones",
            SortOrder = 1,
            PhotosPrivate = false,
            Active = true,
        });

        context.Governorates.Add(new Governorate
        {
            Id = governorateId,
            Code = "cairo",
            SortOrder = 1,
        });

        context.SaveChanges();

        return context;
    }

    private sealed class ObservingCacheService(ICacheService inner) : ICacheService
    {
        public int CategoriesFactoryCalls { get; private set; }

        public int GovernoratesFactoryCalls { get; private set; }

        public Task<T> GetOrSetAsync<T>(
            string key,
            Func<CancellationToken, Task<T>> factory,
            TimeSpan expiration,
            CancellationToken cancellationToken = default)
        {
            Func<CancellationToken, Task<T>> observedFactory = async ct =>
            {
                if (key == CacheKeys.Categories)
                {
                    CategoriesFactoryCalls++;
                }
                else if (key == CacheKeys.Governorates)
                {
                    GovernoratesFactoryCalls++;
                }

                return await factory(ct);
            };

            return inner.GetOrSetAsync(key, observedFactory, expiration, cancellationToken);
        }

        public Task RemoveAsync(string key, CancellationToken cancellationToken = default) =>
            inner.RemoveAsync(key, cancellationToken);
    }
}
