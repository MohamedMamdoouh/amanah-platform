using Amanah.Api.Data;
using Amanah.Api.Data.Entities;
using Amanah.Api.Data.Seeds;
using Amanah.Api.Services.Auth;
using Amanah.Api.Tests.Auth;
using Amanah.Api.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Amanah.Api.Tests.Database;

public class CatalogSeedTests(ApiWebApplicationFactory factory) : IClassFixture<ApiWebApplicationFactory>
{
  private const string AdminPhone = "+201011111111";
  private const string User1Phone = "+201022222222";
  private const string User2Phone = "+201033333333";

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
  public async Task Seed_creates_seven_categories_with_field_definitions()
  {
    await RunWithSeededContextAsync(async context =>
    {
      var categories = await context.Categories
        .Include(category => category.FieldDefinitions)
        .OrderBy(category => category.SortOrder)
        .ToListAsync();

      Assert.Equal(7, categories.Count);

      Assert.Equal(
        [
          "phones",
          "documents-ids",
          "wallets",
          "bags",
          "electronics",
          "accessories",
          "other",
        ],
        categories.Select(category => category.Code));

      Assert.Equal([2, 2, 2, 2, 2, 1, 1], categories.Select(category => category.FieldDefinitions.Count));
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
  public async Task Seed_bootstraps_admin_user_from_admin_phone_and_password()
  {
    await RunWithSeededContextAsync(async context =>
    {
      PhoneNormalizer.TryNormalize(AdminPhone, out var normalizedPhone);

      var admin = await context.Users
        .SingleAsync(user => user.NormalizedPhone == normalizedPhone);

      Assert.Equal(UserRole.Admin, admin.Role);
      Assert.Equal("Admin", admin.DisplayName);
      Assert.False(string.IsNullOrWhiteSpace(admin.PasswordHash));
    });
  }

  [Fact]
  public async Task Seeded_admin_can_login_with_admin_password()
  {
    await using var scope = factory.Services.CreateAsyncScope();
    var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await context.Database.MigrateAsync();
    await scope.ServiceProvider.GetRequiredService<CatalogSeeder>().SeedAsync();

    var client = factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
    {
      HandleCookies = true,
    });

    await using var authContext = new OtpSendTestContext(
      client,
      factory.SmsSender,
      factory.OtpEmailSender,
      factory.CaptchaVerifier,
      factory.Services.CreateAsyncScope());

    var (response, session) = await authContext.LoginAsync("01011111111", "AdminPass123");

    Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
    Assert.Equal("Admin", session?.User.Role);
  }

  [Fact]
  public async Task Seed_bootstraps_user1_and_user2_from_shared_password()
  {
    await RunWithSeededContextAsync(async context =>
    {
      PhoneNormalizer.TryNormalize(User1Phone, out var user1Phone);
      PhoneNormalizer.TryNormalize(User2Phone, out var user2Phone);

      var user1 = await context.Users.SingleAsync(user => user.NormalizedPhone == user1Phone);
      var user2 = await context.Users.SingleAsync(user => user.NormalizedPhone == user2Phone);

      Assert.Equal(UserRole.User, user1.Role);
      Assert.Equal("User1", user1.DisplayName);
      Assert.Equal(UserRole.User, user2.Role);
      Assert.Equal("User2", user2.DisplayName);
      Assert.False(string.IsNullOrWhiteSpace(user1.PasswordHash));
      Assert.False(string.IsNullOrWhiteSpace(user2.PasswordHash));
    });
  }

  [Fact]
  public async Task Seeded_normal_user_can_login_with_seed_user_password()
  {
    await using var scope = factory.Services.CreateAsyncScope();
    var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await context.Database.MigrateAsync();
    await scope.ServiceProvider.GetRequiredService<CatalogSeeder>().SeedAsync();

    var client = factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
    {
      HandleCookies = true,
    });

    await using var authContext = new OtpSendTestContext(
      client,
      factory.SmsSender,
      factory.OtpEmailSender,
      factory.CaptchaVerifier,
      factory.Services.CreateAsyncScope());

    var (user1Response, user1Session) = await authContext.LoginAsync("01022222222", "UserPass123");
    var (user2Response, user2Session) = await authContext.LoginAsync("01033333333", "UserPass123");

    Assert.Equal(System.Net.HttpStatusCode.OK, user1Response.StatusCode);
    Assert.Equal("User", user1Session?.User.Role);
    Assert.Equal("User1", user1Session?.User.DisplayName);
    Assert.Equal(System.Net.HttpStatusCode.OK, user2Response.StatusCode);
    Assert.Equal("User", user2Session?.User.Role);
    Assert.Equal("User2", user2Session?.User.DisplayName);
  }

  [Fact]
  public async Task Documents_ids_first_name_field_has_letters_and_spaces_text_format()
  {
    await RunWithSeededContextAsync(async context =>
    {
      var firstNameField = await context.CategoryFieldDefinitions
        .SingleAsync(field => field.FieldKey == "first_name_on_document");

      Assert.Equal(CategoryTextFormat.LettersAndSpaces, firstNameField.TextFormat);
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

    Assert.Equal(7, await context.Categories.CountAsync());
    Assert.Equal(27, await context.Governorates.CountAsync());
    Assert.Equal(1, await context.Users.CountAsync(user => user.Role == UserRole.Admin));
    Assert.Equal(2, await context.Users.CountAsync(user => user.Role == UserRole.User));
    Assert.Equal(3, await context.Users.CountAsync());
  }

  [Fact]
  public async Task Re_running_seed_preserves_admin_category_and_field_edits()
  {
    await using var scope = factory.Services.CreateAsyncScope();
    var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var seeder = scope.ServiceProvider.GetRequiredService<CatalogSeeder>();

    await context.Database.MigrateAsync();
    await seeder.SeedAsync();

    var other = await context.Categories.SingleAsync(category => category.Code == "other");
    other.Active = false;
    other.SortOrder = 99;
    other.PhotosPrivate = true;

    var phonesColour = await context.CategoryFieldDefinitions
      .SingleAsync(field => field.FieldKey == "colour" && field.Category.Code == "phones");
    phonesColour.MaxLength = 10;
    phonesColour.Required = false;

    var phonesBrandModel = await context.CategoryFieldDefinitions
      .SingleAsync(field => field.FieldKey == "brand_model" && field.Category.Code == "phones");
    context.CategoryFieldDefinitions.Remove(phonesBrandModel);

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
    Assert.True(
      await context.CategoryFieldDefinitions.AnyAsync(field =>
        field.FieldKey == "brand_model" && field.Category.Code == "phones"));
    Assert.Equal(7, await context.Categories.CountAsync());
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
