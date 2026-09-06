using Amanah.Api.Data;
using Amanah.Api.Data.Entities;
using Amanah.Api.Models.Errors;
using Amanah.Api.Services.Infrastructure;
using Amanah.Contracts.Requests.Admin;
using Amanah.Contracts.Responses.Admin;
using Microsoft.EntityFrameworkCore;

namespace Amanah.Api.Services.Admin;

public sealed class CategoryAdminService(AppDbContext dbContext, ICacheService cacheService)
{
    public async Task<Result<AdminCategoryListResponse>> GetAllAsync(
        CancellationToken cancellationToken = default)
    {
        var categories = await dbContext.Categories
            .AsNoTracking()
            .Include(category => category.FieldDefinitions)
            .OrderBy(category => category.SortOrder)
            .ToListAsync(cancellationToken);

        return new AdminCategoryListResponse
        {
            Items = categories.Select(CategoryAdminMapper.ToCategoryResponse).ToList(),
        };
    }

    public async Task<Result<AdminCategoryResponse>> CreateCategoryAsync(
        CreateCategoryRequest request,
        CancellationToken cancellationToken = default)
    {
        if (await CodeExistsAsync(request.Code, cancellationToken: cancellationToken))
        {
            return ResultError.Conflict("A category with this code already exists.");
        }

        var category = new Category
        {
            Code = request.Code,
            SortOrder = request.SortOrder,
            PhotosPrivate = request.PhotosPrivate,
            Active = request.IsActive,
        };

        dbContext.Categories.Add(category);
        await dbContext.SaveChangesAsync(cancellationToken);
        await InvalidateCacheAsync(cancellationToken);

        return CategoryAdminMapper.ToCategoryResponse(category);
    }

    public async Task<Result<AdminCategoryResponse>> UpdateCategoryAsync(
        Guid id,
        UpdateCategoryRequest request,
        CancellationToken cancellationToken = default)
    {
        var category = await dbContext.Categories
            .Include(existing => existing.FieldDefinitions)
            .SingleOrDefaultAsync(existing => existing.Id == id, cancellationToken);

        if (category is null)
        {
            return ResultError.NotFound("Category not found.");
        }

        if (!string.Equals(category.Code, request.Code, StringComparison.Ordinal)
            && await CodeExistsAsync(request.Code, id, cancellationToken))
        {
            return ResultError.Conflict("A category with this code already exists.");
        }

        category.Code = request.Code;
        category.SortOrder = request.SortOrder;
        category.PhotosPrivate = request.PhotosPrivate;
        category.Active = request.IsActive;

        await dbContext.SaveChangesAsync(cancellationToken);
        await InvalidateCacheAsync(cancellationToken);

        return CategoryAdminMapper.ToCategoryResponse(category);
    }

    public async Task<Result<AdminCategoryFieldDefinitionResponse>> CreateFieldAsync(
        Guid categoryId,
        CreateCategoryFieldRequest request,
        CancellationToken cancellationToken = default)
    {
        var category = await dbContext.Categories
            .Include(existing => existing.FieldDefinitions)
            .SingleOrDefaultAsync(existing => existing.Id == categoryId, cancellationToken);

        if (category is null)
        {
            return ResultError.NotFound("Category not found.");
        }

        if (category.FieldDefinitions.Any(field =>
                string.Equals(field.FieldKey, request.FieldKey, StringComparison.Ordinal)))
        {
            return ResultError.Conflict("A field with this key already exists for the category.");
        }

        if (!TryParseFieldType(request.Type, out var fieldType))
        {
            return ResultError.BadRequest("Field type is invalid.");
        }

        if (!TryParseTextFormat(request.TextFormat, fieldType, out var textFormat, out var textFormatError))
        {
            return ResultError.BadRequest(textFormatError!);
        }

        var field = new CategoryFieldDefinition
        {
            CategoryId = categoryId,
            FieldKey = request.FieldKey,
            Type = fieldType,
            Required = request.Required,
            SortOrder = request.SortOrder,
            MinLength = request.MinLength,
            MaxLength = request.MaxLength,
            MinInt = request.MinInt,
            MaxInt = request.MaxInt,
            TextFormat = textFormat,
        };

        dbContext.CategoryFieldDefinitions.Add(field);
        await dbContext.SaveChangesAsync(cancellationToken);
        await InvalidateCacheAsync(cancellationToken);

        return CategoryAdminMapper.ToFieldResponse(field);
    }

    public async Task<Result<AdminCategoryFieldDefinitionResponse>> UpdateFieldAsync(
        Guid categoryId,
        Guid fieldId,
        UpdateCategoryFieldRequest request,
        CancellationToken cancellationToken = default)
    {
        var field = await dbContext.CategoryFieldDefinitions
            .SingleOrDefaultAsync(
                existing => existing.Id == fieldId && existing.CategoryId == categoryId,
                cancellationToken);

        if (field is null)
        {
            return ResultError.NotFound("Category field not found.");
        }

        var duplicateKey = await dbContext.CategoryFieldDefinitions.AnyAsync(
            existing => existing.CategoryId == categoryId
                && existing.Id != fieldId
                && existing.FieldKey == request.FieldKey,
            cancellationToken);

        if (duplicateKey)
        {
            return ResultError.Conflict("A field with this key already exists for the category.");
        }

        if (!TryParseFieldType(request.Type, out var fieldType))
        {
            return ResultError.BadRequest("Field type is invalid.");
        }

        if (!TryParseTextFormat(request.TextFormat, fieldType, out var textFormat, out var textFormatError))
        {
            return ResultError.BadRequest(textFormatError!);
        }

        field.FieldKey = request.FieldKey;
        field.Type = fieldType;
        field.Required = request.Required;
        field.SortOrder = request.SortOrder;
        field.MinLength = request.MinLength;
        field.MaxLength = request.MaxLength;
        field.MinInt = request.MinInt;
        field.MaxInt = request.MaxInt;
        field.TextFormat = textFormat;

        await dbContext.SaveChangesAsync(cancellationToken);
        await InvalidateCacheAsync(cancellationToken);

        return CategoryAdminMapper.ToFieldResponse(field);
    }

    private async Task<bool> CodeExistsAsync(
        string code,
        Guid? excludeId = null,
        CancellationToken cancellationToken = default)
    {
        var query = dbContext.Categories.AsNoTracking().Where(category => category.Code == code);

        if (excludeId is Guid excludedId)
        {
            query = query.Where(category => category.Id != excludedId);
        }

        return await query.AnyAsync(cancellationToken);
    }

    private Task InvalidateCacheAsync(CancellationToken cancellationToken) =>
        cacheService.RemoveAsync(CacheKeys.Categories, cancellationToken);

    private static bool TryParseFieldType(string type, out CategoryFieldType fieldType)
    {
        switch (type.Trim().ToLowerInvariant())
        {
            case "text":
                fieldType = CategoryFieldType.Text;
                return true;
            case "integer":
                fieldType = CategoryFieldType.Integer;
                return true;
            default:
                fieldType = default;
                return false;
        }
    }

    private static bool TryParseTextFormat(
        string? textFormat,
        CategoryFieldType fieldType,
        out CategoryTextFormat? parsed,
        out string? error)
    {
        parsed = null;
        error = null;

        if (string.IsNullOrWhiteSpace(textFormat))
        {
            return true;
        }

        if (fieldType != CategoryFieldType.Text)
        {
            error = "Text format is only valid for text fields.";
            return false;
        }

        if (textFormat.Trim().Equals("letters_and_spaces", StringComparison.Ordinal))
        {
            parsed = CategoryTextFormat.LettersAndSpaces;
            return true;
        }

        error = "Text format is invalid.";
        return false;
    }
}

internal static class CategoryAdminMapper
{
    public static AdminCategoryResponse ToCategoryResponse(Category category) =>
        new()
        {
            Id = category.Id,
            Code = category.Code,
            SortOrder = category.SortOrder,
            PhotosPrivate = category.PhotosPrivate,
            IsActive = category.Active,
            FieldDefinitions = category.FieldDefinitions
                .OrderBy(field => field.SortOrder)
                .Select(ToFieldResponse)
                .ToList(),
        };

    public static AdminCategoryFieldDefinitionResponse ToFieldResponse(CategoryFieldDefinition field) =>
        new()
        {
            Id = field.Id,
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
        };
}
