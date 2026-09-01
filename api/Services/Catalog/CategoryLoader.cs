using Amanah.Api.Data;
using Amanah.Api.Data.Entities;
using Amanah.Contracts.Responses.Catalog;
using Microsoft.EntityFrameworkCore;

namespace Amanah.Api.Services.Catalog;

public sealed class CategoryLoader(AppDbContext dbContext) : ICategoryLoader
{
    public async Task<CategoryListResponse> LoadCategoriesAsync(CancellationToken cancellationToken = default)
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
                        Type = field.Type == CategoryFieldType.Integer ? "integer" : "text",
                        Required = field.Required,
                        SortOrder = field.SortOrder,
                        MinLength = field.MinLength,
                        MaxLength = field.MaxLength,
                        MinInt = field.MinInt,
                        MaxInt = field.MaxInt,
                        TextFormat = field.TextFormat == CategoryTextFormat.LettersAndSpaces
                            ? "letters_and_spaces"
                            : null,
                    })
                    .ToList(),
            })
            .ToListAsync(cancellationToken);

        return new CategoryListResponse { Items = categories };
    }
}
