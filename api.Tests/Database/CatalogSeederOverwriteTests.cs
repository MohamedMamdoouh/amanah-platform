using Amanah.Api.Data;
using Amanah.Api.Data.Entities;
using Amanah.Api.Data.Seeds;
using Amanah.Api.Services.Auth;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace Amanah.Api.Tests.Database;

public class CatalogSeederOverwriteTests
{
    [Fact]
    public async Task Seed_does_not_overwrite_admin_category_or_field_edits()
    {
        await using var context = CreateContext();
        var seeder = CreateSeeder(context);

        await seeder.SeedAsync();

        var other = await context.Categories.SingleAsync(category => category.Code == "other");
        other.Active = false;
        other.SortOrder = 99;
        other.PhotosPrivate = true;

        var phonesColour = await context.CategoryFieldDefinitions
            .SingleAsync(field => field.FieldKey == "colour" && field.Category.Code == "phones");
        phonesColour.MaxLength = 10;
        phonesColour.Required = false;

        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        await seeder.SeedAsync();

        var otherAfter = await context.Categories.SingleAsync(category => category.Code == "other");
        var phonesColourAfter = await context.CategoryFieldDefinitions
            .SingleAsync(field => field.FieldKey == "colour" && field.Category.Code == "phones");

        Assert.False(otherAfter.Active);
        Assert.Equal(99, otherAfter.SortOrder);
        Assert.True(otherAfter.PhotosPrivate);
        Assert.Equal(10, phonesColourAfter.MaxLength);
        Assert.False(phonesColourAfter.Required);
        Assert.Equal(7, await context.Categories.CountAsync());
    }

    [Fact]
    public async Task Seed_adds_missing_seeded_field_without_touching_existing_ones()
    {
        await using var context = CreateContext();
        var seeder = CreateSeeder(context);

        await seeder.SeedAsync();

        var phonesColour = await context.CategoryFieldDefinitions
            .SingleAsync(field => field.FieldKey == "colour" && field.Category.Code == "phones");
        context.CategoryFieldDefinitions.Remove(phonesColour);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        await seeder.SeedAsync();

        Assert.True(
            await context.CategoryFieldDefinitions.AnyAsync(field =>
                field.FieldKey == "colour" && field.Category.Code == "phones"));
    }

    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new AppDbContext(options);
    }

    private static CatalogSeeder CreateSeeder(AppDbContext context)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection().Build();
        return new CatalogSeeder(
            context,
            configuration,
            new UserPasswordHasher(),
            NullLogger<CatalogSeeder>.Instance);
    }
}
