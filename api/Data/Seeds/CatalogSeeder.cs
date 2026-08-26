using Amanah.Api.Data.Entities;
using Amanah.Api.Services.Auth;
using Microsoft.EntityFrameworkCore;

namespace Amanah.Api.Data.Seeds;

public sealed class CatalogSeeder(
    AppDbContext context,
    IConfiguration configuration,
    ILogger<CatalogSeeder> logger)
{
    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        await SeedCategoriesAsync(cancellationToken);
        await SeedGovernoratesAsync(cancellationToken);
        await SeedAdminUserAsync(cancellationToken);
    }

    private async Task SeedCategoriesAsync(CancellationToken cancellationToken)
    {
        foreach (var categorySeed in CategorySeedData.Categories)
        {
            var category = await context.Categories
                .Include(category => category.FieldDefinitions)
                .SingleOrDefaultAsync(c => c.Code == categorySeed.Code, cancellationToken);

            if (category is null)
            {
                category = new Category
                {
                    Code = categorySeed.Code,
                    SortOrder = categorySeed.SortOrder,
                    PhotosPrivate = categorySeed.PhotosPrivate,
                    Active = true,
                };

                context.Categories.Add(category);
                await context.SaveChangesAsync(cancellationToken);
            }
            else
            {
                category.SortOrder = categorySeed.SortOrder;
                category.PhotosPrivate = categorySeed.PhotosPrivate;
                category.Active = true;
            }

            foreach (var fieldSeed in categorySeed.Fields)
            {
                var fieldType = Enum.Parse<CategoryFieldType>(fieldSeed.Type);
                var existingField = category.FieldDefinitions
                    .SingleOrDefault(field => field.FieldKey == fieldSeed.FieldKey);

                if (existingField is null)
                {
                    context.CategoryFieldDefinitions.Add(new CategoryFieldDefinition
                    {
                        Id = Guid.NewGuid(),
                        CategoryId = category.Id,
                        FieldKey = fieldSeed.FieldKey,
                        Type = fieldType,
                        MinLength = fieldSeed.MinLength,
                        MaxLength = fieldSeed.MaxLength,
                        MinInt = fieldSeed.MinInt,
                        MaxInt = fieldSeed.MaxInt,
                        Required = fieldSeed.Required,
                        SortOrder = fieldSeed.SortOrder,
                    });
                }
                else
                {
                    existingField.Type = fieldType;
                    existingField.MinLength = fieldSeed.MinLength;
                    existingField.MaxLength = fieldSeed.MaxLength;
                    existingField.MinInt = fieldSeed.MinInt;
                    existingField.MaxInt = fieldSeed.MaxInt;
                    existingField.Required = fieldSeed.Required;
                    existingField.SortOrder = fieldSeed.SortOrder;
                }
            }
        }

        await context.SaveChangesAsync(cancellationToken);
    }

    private async Task SeedGovernoratesAsync(CancellationToken cancellationToken)
    {
        foreach (var (code, sortOrder) in GovernorateSeedData.Governorates)
        {
            var governorate = await context.Governorates
                .SingleOrDefaultAsync(g => g.Code == code, cancellationToken);

            if (governorate is null)
            {
                context.Governorates.Add(new Governorate
                {
                    Id = Guid.NewGuid(),
                    Code = code,
                    SortOrder = sortOrder,
                });
            }
            else
            {
                governorate.SortOrder = sortOrder;
            }
        }

        await context.SaveChangesAsync(cancellationToken);
    }

    private async Task SeedAdminUserAsync(CancellationToken cancellationToken)
    {
        var adminPhone = configuration["ADMIN_PHONE"];
        if (string.IsNullOrWhiteSpace(adminPhone))
        {
            logger.LogDebug("ADMIN_PHONE is not set; skipping admin bootstrap.");
            return;
        }

        if (!PhoneNormalizer.TryNormalize(adminPhone, out var normalizedPhone))
        {
            logger.LogWarning("ADMIN_PHONE is invalid; skipping admin bootstrap.");
            return;
        }

        var existingAdmin = await context.Users
            .AnyAsync(user => user.NormalizedPhone == normalizedPhone && user.Role == UserRole.Admin, cancellationToken);

        if (existingAdmin)
        {
            return;
        }

        var existingUser = await context.Users
            .SingleOrDefaultAsync(user => user.NormalizedPhone == normalizedPhone, cancellationToken);

        if (existingUser is not null)
        {
            existingUser.Role = UserRole.Admin;
            existingUser.DisplayName ??= "Admin";
        }
        else
        {
            context.Users.Add(new User
            {
                NormalizedPhone = normalizedPhone,
                DisplayName = "Admin",
                Role = UserRole.Admin,
                CreatedAt = DateTimeOffset.UtcNow,
            });
        }

        await context.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Admin user bootstrapped for phone {Phone}.", normalizedPhone);
    }
}
