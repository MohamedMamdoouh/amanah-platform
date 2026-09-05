using Amanah.Api.Data;
using Amanah.Api.Data.Entities;
using Amanah.Api.Services.Auth;
using Amanah.Api.Services.Reports;
using Amanah.Api.Tests.Auth;
using Amanah.Api.Utilities.Common;
using Microsoft.EntityFrameworkCore;

namespace Amanah.Api.Tests.Services;

public class ReportQuotaServiceTests
{
    [Fact]
    public async Task CheckNewSubmissionAsync_allows_when_under_daily_quota_and_open_cap()
    {
        await using var context = CreateContext();
        var reporter = await SeedReporterAsync(context);
        var quotaService = CreateService(context);

        await CreateReportAsync(context, reporter.Id, ReportStatus.PendingReview);
        await CreateReportAsync(context, reporter.Id, ReportStatus.PendingReview);

        var result = await quotaService.CheckNewSubmissionAsync(reporter.Id);

        Assert.Equal(QuotaFailureKind.None, result.Kind);
    }

    [Fact]
    public async Task CheckNewSubmissionAsync_returns_daily_quota_when_three_reports_created_today()
    {
        await using var context = CreateContext();
        var reporter = await SeedReporterAsync(context);
        var quotaService = CreateService(context);

        for (var i = 0; i < 3; i++)
        {
            await CreateReportAsync(context, reporter.Id, ReportStatus.PendingReview);
        }

        var result = await quotaService.CheckNewSubmissionAsync(reporter.Id);

        Assert.Equal(QuotaFailureKind.DailyQuota, result.Kind);
        Assert.True(result.RetryAfterSeconds > 0);
    }

    [Fact]
    public async Task CheckNewSubmissionAsync_returns_open_cap_when_five_open_reports_exist()
    {
        var yesterday = CairoTime.CairoDayStartUtc(DateTimeOffset.UtcNow).AddDays(-1);
        await using var context = CreateContext();
        var reporter = await SeedReporterAsync(context);
        var quotaService = CreateService(context);

        for (var i = 0; i < 5; i++)
        {
            await CreateReportAsync(
                context,
                reporter.Id,
                ReportStatus.Published,
                createdAt: yesterday.AddHours(i + 1));
        }

        var result = await quotaService.CheckNewSubmissionAsync(reporter.Id);

        Assert.Equal(QuotaFailureKind.OpenCap, result.Kind);
        Assert.Null(result.RetryAfterSeconds);
    }

    [Fact]
    public async Task CheckNewSubmissionAsync_skips_limits_when_resubmission()
    {
        await using var context = CreateContext();
        var reporter = await SeedReporterAsync(context);
        var quotaService = CreateService(context);

        for (var i = 0; i < 5; i++)
        {
            await CreateReportAsync(context, reporter.Id, ReportStatus.Published);
        }

        var result = await quotaService.CheckNewSubmissionAsync(reporter.Id, isResubmission: true);

        Assert.Equal(QuotaFailureKind.None, result.Kind);
    }

    [Fact]
    public async Task CheckNewSubmissionAsync_ignores_withdrawn_reports_for_open_cap()
    {
        var yesterday = CairoTime.CairoDayStartUtc(DateTimeOffset.UtcNow).AddDays(-1);
        await using var context = CreateContext();
        var reporter = await SeedReporterAsync(context);
        var quotaService = CreateService(context);

        for (var i = 0; i < 5; i++)
        {
            await CreateReportAsync(
                context,
                reporter.Id,
                ReportStatus.Withdrawn,
                createdAt: yesterday.AddHours(i + 1));
        }

        var result = await quotaService.CheckNewSubmissionAsync(reporter.Id);

        Assert.Equal(QuotaFailureKind.None, result.Kind);
    }

    private static ReportQuotaService CreateService(
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

        context.SaveChanges();
    }

    private static async Task<User> SeedReporterAsync(AppDbContext context)
    {
        var passwordHasher = new UserPasswordHasher();
        var reporter = TestAuthHelpers.CreateUser(
            passwordHasher,
            $"+2010{Random.Shared.Next(10000000, 99999999)}",
            "Reporter");

        context.Users.Add(reporter);
        await context.SaveChangesAsync();

        return reporter;
    }

    private static async Task CreateReportAsync(
        AppDbContext context,
        Guid reporterId,
        ReportStatus status,
        DateTimeOffset? createdAt = null)
    {
        var category = await context.Categories.AsNoTracking().FirstAsync();
        var governorate = await context.Governorates.AsNoTracking().FirstAsync();
        var now = createdAt ?? DateTimeOffset.UtcNow;

        context.Reports.Add(new Report
        {
            ReporterId = reporterId,
            Type = ReportType.Lost,
            CategoryId = category.Id,
            GovernorateId = governorate.Id,
            Title = "Lost item report title",
            Description = "Detailed description of the lost item for testing.",
            DateLostOrFound = CairoTime.TodayInCairo(),
            Status = status,
            HiddenDetail = "Hidden verification detail for testing purposes.",
            CreatedAt = now,
            UpdatedAt = now,
        });

        await context.SaveChangesAsync();
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
