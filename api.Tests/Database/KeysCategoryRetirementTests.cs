using Amanah.Api.Data;
using Amanah.Api.Data.Entities;
using Amanah.Api.Data.Seeds;
using Amanah.Api.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace Amanah.Api.Tests.Database;

/// <summary>
/// Guards the TextOnlyCategoryFieldsAndRemoveKeys retirement SQL.
/// reports.CategoryId → categories is Restrict; a blind DELETE of keys fails
/// AutoMigrate (and process startup) when any keys report exists.
/// </summary>
public class KeysCategoryRetirementTests(ApiWebApplicationFactory factory)
    : IClassFixture<ApiWebApplicationFactory>
{
    // Keep in sync with TextOnlyCategoryFieldsAndRemoveKeys.Up retirement SQL
    // (excluding Integer→Text rewrite and column drops).
    private const string KeysRetirementSql = """
        UPDATE categories
        SET "Active" = false,
            "SortOrder" = 1000
        WHERE "Code" = 'keys'
          AND EXISTS (
              SELECT 1
              FROM reports
              WHERE reports."CategoryId" = categories."Id"
          );

        DELETE FROM categories
        WHERE "Code" = 'keys'
          AND NOT EXISTS (
              SELECT 1
              FROM reports
              WHERE reports."CategoryId" = categories."Id"
          );

        UPDATE categories
        SET "SortOrder" = "SortOrder" - 1
        WHERE "SortOrder" > 4
          AND "SortOrder" < 1000;
        """;

    [Fact]
    public async Task Blind_delete_of_keys_fails_when_reports_reference_category()
    {
        await using var scope = await CreateSeededScopeAsync();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var keys = await SeedKeysCategoryAsync(context);
        await SeedReportForCategoryAsync(context, keys.Id);

        var ex = await Assert.ThrowsAsync<PostgresException>(async () =>
            await context.Database.ExecuteSqlRawAsync(
                """DELETE FROM categories WHERE "Code" = 'keys';"""));

        Assert.Equal("23503", ex.SqlState);
        Assert.True(await context.Categories.AnyAsync(category => category.Code == "keys"));
    }

    [Fact]
    public async Task Retirement_sql_soft_deactivates_keys_when_reports_exist()
    {
        await using var scope = await CreateSeededScopeAsync();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var keys = await SeedKeysCategoryAsync(context);
        await SeedReportForCategoryAsync(context, keys.Id);

        var bagsBefore = await context.Categories.SingleAsync(category => category.Code == "bags");
        var bagsSortBefore = bagsBefore.SortOrder;

        await context.Database.ExecuteSqlRawAsync(KeysRetirementSql);
        context.ChangeTracker.Clear();

        var keysAfter = await context.Categories.SingleAsync(category => category.Code == "keys");
        Assert.False(keysAfter.Active);
        Assert.Equal(1000, keysAfter.SortOrder);

        // Compacting still runs for SortOrder 5..999; parked keys (1000) is excluded.
        var bagsAfter = await context.Categories.SingleAsync(category => category.Code == "bags");
        Assert.Equal(bagsSortBefore - 1, bagsAfter.SortOrder);

        Assert.True(await context.Reports.AnyAsync(report => report.CategoryId == keys.Id));
    }

    [Fact]
    public async Task Retirement_sql_hard_deletes_keys_when_no_reports_exist()
    {
        await using var scope = await CreateSeededScopeAsync();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await SeedKeysCategoryAsync(context);

        var bagsBefore = await context.Categories.SingleAsync(category => category.Code == "bags");
        var bagsSortBefore = bagsBefore.SortOrder;

        await context.Database.ExecuteSqlRawAsync(KeysRetirementSql);
        context.ChangeTracker.Clear();

        Assert.False(await context.Categories.AnyAsync(category => category.Code == "keys"));

        var bagsAfter = await context.Categories.SingleAsync(category => category.Code == "bags");
        Assert.Equal(bagsSortBefore - 1, bagsAfter.SortOrder);
    }

    private async Task<AsyncServiceScope> CreateSeededScopeAsync()
    {
        var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await context.Database.MigrateAsync();
        await scope.ServiceProvider.GetRequiredService<CatalogSeeder>().SeedAsync();
        return scope;
    }

    private static async Task<Category> SeedKeysCategoryAsync(AppDbContext context)
    {
        // Shared fixture DB — wipe leftover keys rows (and dependent reports) from prior cases.
        var existingKeys = await context.Categories
            .Include(category => category.FieldDefinitions)
            .SingleOrDefaultAsync(category => category.Code == "keys");
        if (existingKeys is not null)
        {
            var keysReports = await context.Reports
                .Where(report => report.CategoryId == existingKeys.Id)
                .ToListAsync();
            context.Reports.RemoveRange(keysReports);
            context.Categories.Remove(existingKeys);
            await context.SaveChangesAsync();
            context.ChangeTracker.Clear();
        }

        // Restore post-seed sort layout (bags=4 … other=7) before re-inserting keys at 4.
        var seeded = await context.Categories
            .Where(category => category.Code != "keys")
            .OrderBy(category => category.Code == "phones" ? 0
                : category.Code == "documents-ids" ? 1
                : category.Code == "wallets" ? 2
                : category.Code == "bags" ? 3
                : category.Code == "electronics" ? 4
                : category.Code == "accessories" ? 5
                : 6)
            .ToListAsync();
        for (var i = 0; i < seeded.Count; i++)
        {
            seeded[i].SortOrder = i + 1;
        }

        var keys = new Category
        {
            Id = Guid.NewGuid(),
            Code = "keys",
            SortOrder = 4,
            PhotosPrivate = false,
            Active = true,
            FieldDefinitions =
            [
                new CategoryFieldDefinition
                {
                    Id = Guid.NewGuid(),
                    FieldKey = "key_type",
                    Type = CategoryFieldType.Text,
                    Required = true,
                    SortOrder = 1,
                    MinLength = 2,
                    MaxLength = 80,
                },
            ],
        };

        // Park bags+ at SortOrder >= 5 so retirement compact matches pre-removal layout.
        foreach (var category in seeded.Where(category => category.SortOrder >= 4))
        {
            category.SortOrder += 1;
        }

        context.Categories.Add(keys);
        await context.SaveChangesAsync();
        return keys;
    }

    private static async Task SeedReportForCategoryAsync(AppDbContext context, Guid categoryId)
    {
        var reporter = await context.Users.FirstAsync(user => user.Role == UserRole.User);
        var governorate = await context.Governorates.FirstAsync();

        context.Reports.Add(new Report
        {
            Id = Guid.NewGuid(),
            ReporterId = reporter.Id,
            Type = ReportType.Lost,
            CategoryId = categoryId,
            Title = "Lost house keys near the metro",
            Description = "A description long enough to satisfy the report schema.",
            DateLostOrFound = new DateOnly(2026, 9, 1),
            GovernorateId = governorate.Id,
            HiddenDetail = "hidden verification detail",
            Status = ReportStatus.PendingReview,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        });
        await context.SaveChangesAsync();
    }
}
