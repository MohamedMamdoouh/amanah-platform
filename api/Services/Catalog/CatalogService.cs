using Amanah.Api.Data;
using Amanah.Api.Data.Entities;
using Amanah.Api.Options;
using Amanah.Api.Services.Infrastructure;
using Amanah.Contracts.Responses.Catalog;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Amanah.Api.Services.Catalog;

public sealed class CatalogService(
    AppDbContext dbContext,
    ICacheService cacheService,
    IOptions<CacheOptions> cacheOptions)
{
    private readonly CacheOptions _cacheOptions = cacheOptions.Value;

    public Task<CategoryListResponse> GetCategoriesAsync(CancellationToken cancellationToken = default) =>
        cacheService.GetOrSetAsync(
            CacheKeys.Categories,
            LoadCategoriesAsync,
            TimeSpan.FromSeconds(_cacheOptions.CategoriesTtlSeconds),
            cancellationToken);

    public Task<GovernorateListResponse> GetGovernoratesAsync(CancellationToken cancellationToken = default) =>
        cacheService.GetOrSetAsync(
            CacheKeys.Governorates,
            LoadGovernoratesAsync,
            TimeSpan.FromSeconds(_cacheOptions.GovernoratesTtlSeconds),
            cancellationToken);

    private async Task<CategoryListResponse> LoadCategoriesAsync(CancellationToken cancellationToken = default)
    {
        var categories = await dbContext.Categories
            .AsNoTracking()
            .Where(category => category.Active)
            .OrderBy(category => category.SortOrder)
            .Select(category => new CategoryResponse
            {
                Code = category.Code,
                SortOrder = category.SortOrder,
                PhotosPrivate = category.PhotosPrivate,
                FieldDefinitions = category.FieldDefinitions
                    .OrderBy(field => field.SortOrder)
                    .Select(field => new CategoryFieldDefinitionResponse
                    {
                        FieldKey = field.FieldKey,
                        Type = "text",
                        Required = field.Required,
                        SortOrder = field.SortOrder,
                        MinLength = field.MinLength,
                        MaxLength = field.MaxLength,
                        TextFormat = field.TextFormat == CategoryTextFormat.LettersAndSpaces
                            ? "letters_and_spaces"
                            : null,
                    })
                    .ToList(),
            })
            .ToListAsync(cancellationToken);

        return new CategoryListResponse { Items = categories };
    }

    private async Task<GovernorateListResponse> LoadGovernoratesAsync(CancellationToken cancellationToken = default)
    {
        var governorates = await dbContext.Governorates
            .AsNoTracking()
            .OrderBy(governorate => governorate.SortOrder)
            .Select(governorate => new GovernorateResponse
            {
                Code = governorate.Code,
                SortOrder = governorate.SortOrder,
            })
            .ToListAsync(cancellationToken);

        return new GovernorateListResponse { Items = governorates };
    }
}

public static class CatalogServiceCollectionExtensions
{
    public static IServiceCollection AddCatalogServices(this IServiceCollection services)
    {
        services.AddScoped<CatalogService>();

        return services;
    }
}
