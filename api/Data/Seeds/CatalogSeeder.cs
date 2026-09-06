using Amanah.Api.Data.Entities;
using Amanah.Api.Services.Auth;
using Microsoft.EntityFrameworkCore;

namespace Amanah.Api.Data.Seeds;

public sealed class CatalogSeeder(
    AppDbContext context,
    IConfiguration configuration,
    UserPasswordHasher passwordHasher,
    ILogger<CatalogSeeder> logger)
{
    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        await SeedCategoriesAsync(cancellationToken);
        await SeedGovernoratesAsync(cancellationToken);
        await SeedBootstrapUserAsync(
            "ADMIN_PHONE",
            "ADMIN_PASSWORD",
            UserRole.Admin,
            "Admin",
            promoteExistingUser: true,
            cancellationToken);
        await SeedBootstrapUserAsync(
            "SEED_USER_PHONE",
            "SEED_USER_PASSWORD",
            UserRole.User,
            "User",
            promoteExistingUser: false,
            cancellationToken);
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

            foreach (var fieldSeed in categorySeed.Fields)
            {
                var existingField = category.FieldDefinitions
                    .SingleOrDefault(field => field.FieldKey == fieldSeed.FieldKey);

                if (existingField is not null)
                {
                    continue;
                }

                var fieldType = Enum.Parse<CategoryFieldType>(fieldSeed.Type);
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
                    TextFormat = fieldSeed.TextFormat,
                });
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

    private async Task SeedBootstrapUserAsync(
        string phoneConfigKey,
        string passwordConfigKey,
        UserRole role,
        string defaultDisplayName,
        bool promoteExistingUser,
        CancellationToken cancellationToken)
    {
        var phone = configuration[phoneConfigKey];
        if (string.IsNullOrWhiteSpace(phone))
        {
            logger.LogDebug("{PhoneConfigKey} is not set; skipping {Role} bootstrap.", phoneConfigKey, role);
            return;
        }

        var password = configuration[passwordConfigKey];
        if (string.IsNullOrWhiteSpace(password))
        {
            logger.LogWarning("{PasswordConfigKey} is not set; skipping {Role} bootstrap.", passwordConfigKey, role);
            return;
        }

        if (!PhoneNormalizer.TryNormalize(phone, out var normalizedPhone))
        {
            logger.LogWarning("{PhoneConfigKey} is invalid; skipping {Role} bootstrap.", phoneConfigKey, role);
            return;
        }

        var existingUserWithRole = await context.Users
            .AnyAsync(user => user.NormalizedPhone == normalizedPhone && user.Role == role, cancellationToken);

        if (existingUserWithRole)
        {
            return;
        }

        var existingUser = await context.Users
            .SingleOrDefaultAsync(user => user.NormalizedPhone == normalizedPhone, cancellationToken);

        if (existingUser is not null)
        {
            if (!promoteExistingUser)
            {
                logger.LogDebug(
                    "User with phone {Phone} already exists with role {ExistingRole}; skipping {Role} bootstrap.",
                    normalizedPhone,
                    existingUser.Role,
                    role);
                return;
            }

            existingUser.Role = role;
            existingUser.DisplayName ??= defaultDisplayName;
        }
        else
        {
            var user = new User
            {
                NormalizedPhone = normalizedPhone,
                DisplayName = defaultDisplayName,
                Role = role,
                CreatedAt = DateTimeOffset.UtcNow,
                PasswordHash = string.Empty,
            };
            user.PasswordHash = passwordHasher.HashPassword(user, password);
            context.Users.Add(user);
        }

        await context.SaveChangesAsync(cancellationToken);
        logger.LogInformation("{Role} user bootstrapped for phone {Phone}.", role, normalizedPhone);
    }
}
