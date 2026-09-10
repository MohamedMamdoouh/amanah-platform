using Amanah.Api.Data;
using Amanah.Api.Data.Entities;
using Amanah.Api.Services.Auth;
using Amanah.Api.Services.Claims;
using Amanah.Api.Tests.Auth;
using Amanah.Api.Utilities.Common;
using Microsoft.EntityFrameworkCore;

namespace Amanah.Api.Tests.Services;

public class ClaimQuotaServiceTests
{
    [Fact]
    public async Task CheckDailySubmissionAsync_allows_when_under_daily_quota()
    {
        await using var context = CreateContext();
        var claimant = await SeedUserAsync(context);
        var quotaService = CreateService(context);

        for (var i = 0; i < 4; i++)
        {
            await CreateClaimAsync(context, claimant.Id);
        }

        var result = await quotaService.CheckDailySubmissionAsync(claimant.Id);

        Assert.False(result.IsExceeded);
        Assert.Null(result.RetryAfterSeconds);
    }

    [Fact]
    public async Task CheckDailySubmissionAsync_returns_exceeded_when_five_claims_submitted_today()
    {
        await using var context = CreateContext();
        var claimant = await SeedUserAsync(context);
        var quotaService = CreateService(context);

        for (var i = 0; i < ClaimQuotaService.DailyQuotaLimit; i++)
        {
            await CreateClaimAsync(context, claimant.Id);
        }

        var result = await quotaService.CheckDailySubmissionAsync(claimant.Id);

        Assert.True(result.IsExceeded);
        Assert.True(result.RetryAfterSeconds > 0);
    }

    [Fact]
    public async Task CheckDailySubmissionAsync_ignores_claims_from_previous_cairo_day()
    {
        var yesterday = CairoTime.CairoDayStartUtc(DateTimeOffset.UtcNow).AddDays(-1).AddHours(12);
        await using var context = CreateContext();
        var claimant = await SeedUserAsync(context);
        var quotaService = CreateService(context);

        for (var i = 0; i < ClaimQuotaService.DailyQuotaLimit; i++)
        {
            await CreateClaimAsync(context, claimant.Id, submittedAt: yesterday.AddMinutes(i));
        }

        var result = await quotaService.CheckDailySubmissionAsync(claimant.Id);

        Assert.False(result.IsExceeded);
    }

    private static ClaimQuotaService CreateService(
        AppDbContext context,
        DateTimeOffset? utcNow = null) =>
        new(context, new FixedTimeProvider(utcNow ?? DateTimeOffset.UtcNow));

    private static AppDbContext CreateContext()
    {
        var databaseName = Guid.NewGuid().ToString();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName)
            .Options;

        var context = new AppDbContext(options);
        SeedCatalog(context);

        return context;
    }

    private static void SeedCatalog(AppDbContext context)
    {
        var categoryId = Guid.NewGuid();
        var governorateId = Guid.NewGuid();
        var passwordHasher = new UserPasswordHasher();
        var reporter = TestAuthHelpers.CreateUser(passwordHasher, "+201099999999", "Reporter");

        context.Users.Add(reporter);

        context.Categories.Add(new Category
        {
            Id = categoryId,
            Code = "phones",
            SortOrder = 1,
            PhotosPrivate = false,
            Active = true,
        });

        context.Governorates.Add(new Governorate
        {
            Id = governorateId,
            Code = "cairo",
            SortOrder = 1,
        });

        context.Reports.Add(new Report
        {
            Id = Guid.NewGuid(),
            ReporterId = reporter.Id,
            Type = ReportType.Lost,
            CategoryId = categoryId,
            GovernorateId = governorateId,
            Title = "Lost item report title",
            Description = "Detailed description of the lost item for testing.",
            DateLostOrFound = CairoTime.TodayInCairo(),
            Status = ReportStatus.Published,
            HiddenDetail = "Hidden verification detail for testing purposes.",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
            PublishedAt = DateTimeOffset.UtcNow,
        });

        context.SaveChanges();
    }

    private static async Task<User> SeedUserAsync(AppDbContext context)
    {
        var passwordHasher = new UserPasswordHasher();
        var user = TestAuthHelpers.CreateUser(
            passwordHasher,
            $"+2010{Random.Shared.Next(10000000, 99999999)}",
            "Claimant");

        context.Users.Add(user);
        await context.SaveChangesAsync();

        return user;
    }

    private static async Task CreateClaimAsync(
        AppDbContext context,
        Guid claimantId,
        DateTimeOffset? submittedAt = null)
    {
        var report = await context.Reports.AsNoTracking().FirstAsync();
        var now = submittedAt ?? DateTimeOffset.UtcNow;

        context.Claims.Add(new Claim
        {
            Id = Guid.NewGuid(),
            ReportId = report.Id,
            ClaimantId = claimantId,
            Status = ClaimStatus.Pending,
            SubmittedAnswer = "Black leather wallet with a red stripe inside.",
            SubmittedAt = now,
            AttemptNumber = 1,
            CountsAsFailure = false,
        });

        await context.SaveChangesAsync();
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
