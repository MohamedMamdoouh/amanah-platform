using Amanah.Api.Data;
using Amanah.Api.Data.Entities;
using Amanah.Api.Services.Admin;
using Amanah.Api.Services.Infrastructure;
using Amanah.Contracts.Requests.Admin;
using Microsoft.EntityFrameworkCore;

namespace Amanah.Api.Tests.Catalog;

public class CategoryAdminServiceTests
{
    [Fact]
    public async Task UpdateCategory_rejects_photos_private_change_when_reports_exist()
    {
        await using var context = CreateContext();
        var category = await SeedCategoryAsync(context);
        await SeedReportAsync(context, category.Id);

        var service = new CategoryAdminService(context, new NoopCache());
        var result = await service.UpdateCategoryAsync(
            category.Id,
            new UpdateCategoryRequest
            {
                Code = category.Code,
                SortOrder = category.SortOrder,
                PhotosPrivate = true,
                IsActive = true,
            });

        Assert.False(result.IsSuccess);
        Assert.Equal(409, result.Error!.StatusCode);
        await context.Entry(category).ReloadAsync();
        Assert.False(category.PhotosPrivate);
    }

    [Fact]
    public async Task UpdateCategory_allows_photos_private_change_when_no_reports_exist()
    {
        await using var context = CreateContext();
        var category = await SeedCategoryAsync(context);
        var service = new CategoryAdminService(context, new NoopCache());

        var result = await service.UpdateCategoryAsync(
            category.Id,
            new UpdateCategoryRequest
            {
                Code = category.Code,
                SortOrder = category.SortOrder,
                PhotosPrivate = true,
                IsActive = true,
            });

        Assert.True(result.IsSuccess);
        Assert.True(result.Value!.PhotosPrivate);
    }

    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new AppDbContext(options);
    }

    private static async Task<Category> SeedCategoryAsync(AppDbContext context)
    {
        var category = new Category
        {
            Code = "phones",
            SortOrder = 1,
            PhotosPrivate = false,
            Active = true,
        };
        context.Categories.Add(category);
        await context.SaveChangesAsync();
        return category;
    }

    private static async Task SeedReportAsync(AppDbContext context, Guid categoryId)
    {
        var reporter = new User
        {
            NormalizedPhone = "+201011111111",
            DisplayName = "Reporter",
            Role = UserRole.User,
            CreatedAt = DateTimeOffset.UtcNow,
            PasswordHash = "hash",
        };
        var governorate = new Governorate { Code = "cairo", SortOrder = 1 };
        context.Users.Add(reporter);
        context.Governorates.Add(governorate);
        await context.SaveChangesAsync();

        context.Reports.Add(new Report
        {
            ReporterId = reporter.Id,
            Type = ReportType.Lost,
            CategoryId = categoryId,
            Title = "Lost phone that needs a title",
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

    private sealed class NoopCache : ICacheService
    {
        public Task<T> GetOrSetAsync<T>(
            string key,
            Func<CancellationToken, Task<T>> factory,
            TimeSpan ttl,
            CancellationToken cancellationToken = default) =>
            factory(cancellationToken);

        public Task RemoveAsync(string key, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }
}
