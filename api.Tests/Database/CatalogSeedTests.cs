using Amanah.Api.Data;
using Amanah.Api.Data.Entities;
using Amanah.Api.Data.Seeds;
using Amanah.Api.Services.Auth;
using Amanah.Api.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Amanah.Api.Tests.Database;

public class CatalogSeedTests(ApiWebApplicationFactory factory) : IClassFixture<ApiWebApplicationFactory>
{
  private const string AdminPhone = "+201011111111";

  [Fact]
  public async Task Full_schema_migration_applies_on_database_with_auth_migration()
  {
    await RunWithSeededContextAsync(async context =>
    {
      var appliedMigrations = await context.Database.GetAppliedMigrationsAsync();
      Assert.Contains("20260823215341_InitialAuth", appliedMigrations);
      Assert.Contains("20260825211141_FullSchema", appliedMigrations);
    });
  }

  [Fact]
  public async Task Seed_creates_eight_categories_with_field_definitions()
  {
    await RunWithSeededContextAsync(async context =>
    {
      var categories = await context.Categories
        .Include(category => category.FieldDefinitions)
        .OrderBy(category => category.SortOrder)
        .ToListAsync();

      Assert.Equal(8, categories.Count);

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
        categories.Select(category => category.Code));

      Assert.Equal([2, 2, 2, 2, 2, 2, 1, 1], categories.Select(category => category.FieldDefinitions.Count));
    });
  }

  [Fact]
  public async Task Documents_ids_category_has_photos_private_enabled()
  {
    await RunWithSeededContextAsync(async context =>
    {
      var documentsCategory = await context.Categories
        .SingleAsync(category => category.Code == "documents-ids");

      Assert.True(documentsCategory.PhotosPrivate);
    });
  }

  [Fact]
  public async Task Seed_creates_twenty_seven_governorates()
  {
    await RunWithSeededContextAsync(async context =>
    {
      var governorates = await context.Governorates
        .OrderBy(governorate => governorate.SortOrder)
        .ToListAsync();

      Assert.Equal(27, governorates.Count);
      Assert.Equal(1, governorates[0].SortOrder);
      Assert.Equal(27, governorates[^1].SortOrder);
    });
  }

  [Fact]
  public async Task Seed_bootstraps_admin_user_from_admin_phone()
  {
    await RunWithSeededContextAsync(async context =>
    {
      PhoneNormalizer.TryNormalize(AdminPhone, out var normalizedPhone);

      var admin = await context.Users
        .SingleAsync(user => user.NormalizedPhone == normalizedPhone);

      Assert.Equal(UserRole.Admin, admin.Role);
      Assert.Equal("Admin", admin.DisplayName);
    });
  }

  [Fact]
  public async Task Re_running_seed_is_idempotent()
  {
    await using var scope = factory.Services.CreateAsyncScope();
    var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var seeder = scope.ServiceProvider.GetRequiredService<CatalogSeeder>();

    await context.Database.MigrateAsync();
    await seeder.SeedAsync();
    await seeder.SeedAsync();

    Assert.Equal(8, await context.Categories.CountAsync());
    Assert.Equal(27, await context.Governorates.CountAsync());
    Assert.Equal(1, await context.Users.CountAsync(user => user.Role == UserRole.Admin));
  }

  private async Task RunWithSeededContextAsync(Func<AppDbContext, Task> test)
  {
    await using var scope = factory.Services.CreateAsyncScope();
    var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await context.Database.MigrateAsync();
    await scope.ServiceProvider.GetRequiredService<CatalogSeeder>().SeedAsync();
    await test(context);
  }
}
