using Amanah.Api.Data.Entities;
using Amanah.Api.Services.Lifecycle;
using Amanah.Api.Services.Storage;
using Amanah.Api.Tests.Infrastructure;
using Amanah.Api.Tests.Reports;
using Amanah.Api.Tests.Uploads;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Amanah.Api.Tests.Lifecycle;

public class TerminalPhotoCleanupTests(ApiWebApplicationFactory factory) : IClassFixture<ApiWebApplicationFactory>
{
    [Fact]
    public async Task WithdrawAsync_deletes_attached_photos_and_storage_keys()
    {
        await using var context = await ReportTestContext.CreateAsync(factory);
        var reportId = Guid.NewGuid();
        const string originalKey = "public/reports/test/original.jpg";
        const string thumbnailKey = "public/reports/test/thumb.webp";

        var category = await context.DbContext.Categories
            .SingleAsync(item => item.Code == "phones");
        var governorate = await context.DbContext.Governorates
            .SingleAsync(item => item.Code == "cairo");

        context.DbContext.Reports.Add(new Report
        {
            Id = reportId,
            ReporterId = context.Session.User.Id,
            Type = ReportType.Lost,
            CategoryId = category.Id,
            Title = "Terminal cleanup test",
            Description = "Description long enough for validation",
            DateLostOrFound = DateOnly.FromDateTime(DateTime.UtcNow),
            GovernorateId = governorate.Id,
            Status = ReportStatus.Published,
            HiddenDetail = "Hidden detail text",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        });

        context.DbContext.ReportPhotos.Add(new ReportPhoto
        {
            Id = Guid.NewGuid(),
            ReportId = reportId,
            StorageKey = originalKey,
            ThumbnailStorageKey = thumbnailKey,
            ContentType = "image/jpeg",
            SizeBytes = 1024,
            SortOrder = 0,
        });
        await context.DbContext.SaveChangesAsync();

        await using var serviceScope = factory.Services.CreateAsyncScope();
        var storage = Assert.IsType<FakeBucketStorage>(
            serviceScope.ServiceProvider.GetRequiredService<IBucketStorage>());
        await storage.PutAsync(originalKey, new MemoryStream([1, 2, 3]), "image/jpeg");
        await storage.PutAsync(thumbnailKey, new MemoryStream([4, 5, 6]), "image/webp");

        var lifecycleService = serviceScope.ServiceProvider.GetRequiredService<ReportLifecycleService>();
        var dbContext = serviceScope.ServiceProvider.GetRequiredService<Amanah.Api.Data.AppDbContext>();
        var report = await dbContext.Reports
            .Include(item => item.Photos)
            .SingleAsync(item => item.Id == reportId);

        var result = await lifecycleService.WithdrawAsync(report, "Report withdrawn");

        Assert.True(result.IsSuccess);
        Assert.Equal(0, await dbContext.ReportPhotos.CountAsync());
        Assert.False(storage.ContainsKey(originalKey));
        Assert.False(storage.ContainsKey(thumbnailKey));
    }

    [Fact]
    public async Task Withdraw_pending_report_deletes_attached_photos()
    {
        await using var context = await ReportTestContext.CreateAsync(factory);
        var (_, created) = await context.SubmitReportAsync(
            TestReportHelpers.BuildValidLostRequest(),
            [TestImageFactory.CreateMinimalJpeg()]);
        Assert.NotNull(created);

        var photo = await context.DbContext.ReportPhotos
            .AsNoTracking()
            .SingleAsync(item => item.ReportId == created.Id);
        var storage = Assert.IsType<FakeBucketStorage>(
            factory.Services.GetRequiredService<IBucketStorage>());
        Assert.True(storage.ContainsKey(photo.StorageKey));

        var response = await context.WithdrawReportAsync(
            created.Id,
            new() { Reason = "posted_by_mistake" });
        Assert.Equal(System.Net.HttpStatusCode.NoContent, response.StatusCode);

        Assert.Equal(ReportStatus.Withdrawn, (
            await context.DbContext.Reports.AsNoTracking().SingleAsync(item => item.Id == created.Id)).Status);
        Assert.Equal(0, await context.DbContext.ReportPhotos.CountAsync());
        Assert.False(storage.ContainsKey(photo.StorageKey));
        Assert.False(storage.ContainsKey(photo.ThumbnailStorageKey!));
    }
}
