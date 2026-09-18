using Amanah.Api.Data;
using Amanah.Api.Data.Entities;
using Amanah.Api.Services.Lifecycle;
using Amanah.Api.Services.Storage;
using Amanah.Api.Tests.Infrastructure;
using Amanah.Api.Tests.Reports;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Amanah.Api.Tests.Lifecycle;

public class StorageDeletionOutboxTests(ApiWebApplicationFactory factory) : IClassFixture<ApiWebApplicationFactory>
{
    [Fact]
    public async Task WithdrawAsync_enqueues_pending_outbox_rows_before_storage_is_deleted()
    {
        await using var context = await ReportTestContext.CreateAsync(factory);
        const string originalKey = "public/reports/outbox/original.jpg";
        const string thumbnailKey = "public/reports/outbox/thumb.webp";

        await using var serviceScope = factory.Services.CreateAsyncScope();
        var storage = Assert.IsType<FakeBucketStorage>(
            serviceScope.ServiceProvider.GetRequiredService<IBucketStorage>());
        await storage.PutAsync(originalKey, new MemoryStream([1, 2, 3]), "image/jpeg");
        await storage.PutAsync(thumbnailKey, new MemoryStream([4, 5, 6]), "image/webp");

        var dbContext = serviceScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var lifecycleService = serviceScope.ServiceProvider.GetRequiredService<ReportLifecycleService>();
        var report = await SeedPublishedReportWithPhotosAsync(
            context,
            dbContext,
            originalKey,
            thumbnailKey);

        var result = await lifecycleService.WithdrawAsync(report, "Report withdrawn");
        Assert.True(result.IsSuccess);

        var outboxRows = await dbContext.StorageDeletionOutboxMessages
            .AsNoTracking()
            .Where(message => message.Status == StorageDeletionOutboxStatus.Pending)
            .ToListAsync();
        Assert.Equal(2, outboxRows.Count);
        Assert.All(outboxRows, message => Assert.Equal(StorageDeletionSource.ReportWithdraw, message.Source));
        Assert.True(storage.ContainsKey(originalKey));
        Assert.True(storage.ContainsKey(thumbnailKey));
    }

    [Fact]
    public async Task ProcessPendingOutbox_deletes_storage_and_marks_rows_sent()
    {
        await using var context = await ReportTestContext.CreateAsync(factory);
        const string originalKey = "public/reports/outbox/process-original.jpg";
        const string thumbnailKey = "public/reports/outbox/process-thumb.webp";

        await using var serviceScope = factory.Services.CreateAsyncScope();
        var storage = Assert.IsType<FakeBucketStorage>(
            serviceScope.ServiceProvider.GetRequiredService<IBucketStorage>());
        await storage.PutAsync(originalKey, new MemoryStream([1, 2, 3]), "image/jpeg");
        await storage.PutAsync(thumbnailKey, new MemoryStream([4, 5, 6]), "image/webp");

        var dbContext = serviceScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var lifecycleService = serviceScope.ServiceProvider.GetRequiredService<ReportLifecycleService>();
        var report = await SeedPublishedReportWithPhotosAsync(
            context,
            dbContext,
            originalKey,
            thumbnailKey);

        var result = await lifecycleService.WithdrawAsync(report, "Report withdrawn");
        Assert.True(result.IsSuccess);

        await StorageDeletionOutboxTestHelpers.ProcessPendingOutboxAsync(factory);

        Assert.False(storage.ContainsKey(originalKey));
        Assert.False(storage.ContainsKey(thumbnailKey));
        Assert.Equal(0, await dbContext.StorageDeletionOutboxMessages.CountAsync(
            message => message.Status == StorageDeletionOutboxStatus.Pending));
        Assert.Equal(2, await dbContext.StorageDeletionOutboxMessages.CountAsync(
            message => message.Status == StorageDeletionOutboxStatus.Sent));
    }

    [Fact]
    public async Task EnqueueAsync_deduplicates_pending_storage_keys()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var enqueueService = scope.ServiceProvider.GetRequiredService<StorageDeletionEnqueueService>();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        await enqueueService.EnqueueAsync(
            ["public/reports/dedupe/key.jpg", "public/reports/dedupe/key.jpg"],
            StorageDeletionSource.ReportWithdraw);
        await dbContext.SaveChangesAsync();

        await enqueueService.EnqueueAsync(
            ["public/reports/dedupe/key.jpg"],
            StorageDeletionSource.ReportWithdraw);
        await dbContext.SaveChangesAsync();

        Assert.Equal(1, await dbContext.StorageDeletionOutboxMessages.CountAsync());
    }

    [Fact]
    public async Task Rolled_back_transaction_does_not_persist_outbox_rows()
    {
        await using var context = await ReportTestContext.CreateAsync(factory);
        const string originalKey = "public/reports/outbox/rollback.jpg";

        await using var serviceScope = factory.Services.CreateAsyncScope();
        var storage = Assert.IsType<FakeBucketStorage>(
            serviceScope.ServiceProvider.GetRequiredService<IBucketStorage>());
        await storage.PutAsync(originalKey, new MemoryStream([1, 2, 3]), "image/jpeg");

        var dbContext = serviceScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var enqueueService = serviceScope.ServiceProvider.GetRequiredService<StorageDeletionEnqueueService>();
        var report = await SeedPublishedReportWithPhotosAsync(
            context,
            dbContext,
            originalKey,
            thumbnailKey: null);

        await using var transaction = await dbContext.Database.BeginTransactionAsync();
        report.Status = ReportStatus.Withdrawn;
        dbContext.ReportPhotos.RemoveRange(report.Photos);
        await enqueueService.EnqueueAsync([originalKey], StorageDeletionSource.ReportWithdraw);
        await dbContext.SaveChangesAsync();
        await transaction.RollbackAsync();

        Assert.Equal(0, await dbContext.StorageDeletionOutboxMessages.CountAsync());
        Assert.True(storage.ContainsKey(originalKey));
    }

    [Fact]
    public async Task StorageDeletionOutboxCleanup_removes_old_processed_rows()
    {
        await using var context = await ReportTestContext.CreateAsync(factory);
        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        dbContext.StorageDeletionOutboxMessages.Add(new StorageDeletionOutboxMessage
        {
            Id = Guid.NewGuid(),
            StorageKey = "public/reports/cleanup/old.jpg",
            Status = StorageDeletionOutboxStatus.Sent,
            Source = StorageDeletionSource.ReportWithdraw,
            CreatedAt = DateTimeOffset.UtcNow.AddDays(-40),
            ProcessedAt = DateTimeOffset.UtcNow.AddDays(-31),
            AttemptCount = 1,
        });
        await dbContext.SaveChangesAsync();

        var response = await StorageDeletionOutboxTestHelpers.RunCleanupJobAsync(context);
        Assert.Equal(System.Net.HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(0, await dbContext.StorageDeletionOutboxMessages.CountAsync());
    }

    private static async Task<Report> SeedPublishedReportWithPhotosAsync(
        ReportTestContext context,
        AppDbContext dbContext,
        string originalKey,
        string? thumbnailKey)
    {
        var reportId = Guid.NewGuid();
        var category = await dbContext.Categories.SingleAsync(item => item.Code == "phones");
        var governorate = await dbContext.Governorates.SingleAsync(item => item.Code == "cairo");

        var report = new Report
        {
            Id = reportId,
            ReporterId = context.Session.User.Id,
            Type = ReportType.Lost,
            CategoryId = category.Id,
            Title = "Outbox cleanup test",
            Description = "Description long enough for validation",
            DateLostOrFound = DateOnly.FromDateTime(DateTime.UtcNow),
            GovernorateId = governorate.Id,
            Status = ReportStatus.Published,
            HiddenDetail = "Hidden detail text",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };

        report.Photos.Add(new ReportPhoto
        {
            Id = Guid.NewGuid(),
            ReportId = reportId,
            StorageKey = originalKey,
            ThumbnailStorageKey = thumbnailKey,
            ContentType = "image/jpeg",
            SizeBytes = 1024,
            SortOrder = 0,
        });

        dbContext.Reports.Add(report);
        await dbContext.SaveChangesAsync();

        return await dbContext.Reports
            .Include(item => item.Photos)
            .SingleAsync(item => item.Id == reportId);
    }
}
