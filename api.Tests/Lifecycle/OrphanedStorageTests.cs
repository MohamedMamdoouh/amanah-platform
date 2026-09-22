using System.Data.Common;
using System.Net;
using Amanah.Api.Data;
using Amanah.Api.Data.Entities;
using Amanah.Api.Services.Storage;
using Amanah.Api.Services.Uploads;
using Amanah.Api.Tests.Claims;
using Amanah.Api.Tests.Infrastructure;
using Amanah.Api.Tests.Reports;
using Amanah.Api.Tests.Uploads;
using Amanah.Contracts.Errors;
using Amanah.Contracts.Requests.Claims;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace Amanah.Api.Tests.Lifecycle;

public sealed class OrphanedStorageWebApplicationFactory : ApiWebApplicationFactory
{
    public FailSaveChangesInterceptor SaveChangesInterceptor { get; } = new();

    public ClaimSubmitRaceCommandInterceptor RaceCommandInterceptor { get; private set; } = null!;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);

        RaceCommandInterceptor = new ClaimSubmitRaceCommandInterceptor(ConnectionString);

        builder.ConfigureTestServices(services =>
        {
            services.AddSingleton(SaveChangesInterceptor);
            services.AddSingleton(RaceCommandInterceptor);
            services.AddDbContext<AppDbContext>((_, options) =>
            {
                options.UseNpgsql(ConnectionString);
                options.AddInterceptors(SaveChangesInterceptor, RaceCommandInterceptor);
            });
        });
    }
}

public class OrphanedStorageTests(OrphanedStorageWebApplicationFactory factory)
    : IClassFixture<OrphanedStorageWebApplicationFactory>
{
    [Fact]
    public async Task CreateAsync_compensating_delete_removes_photos_when_save_fails()
    {
        await using var context = await ReportTestContext.CreateAsync(factory);
        var storage = Assert.IsType<FakeBucketStorage>(
            factory.Services.GetRequiredService<IBucketStorage>());
        factory.SaveChangesInterceptor.FailNextReportSave = true;

        var (response, _) = await context.SubmitReportAsync(
            TestReportHelpers.BuildValidLostRequest(),
            [TestImageFactory.CreateMinimalJpeg()]);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Empty(await storage.ListAsync("public/reports/"));
        Assert.Empty(await storage.ListAsync("private/reports/"));
        Assert.Equal(0, await context.DbContext.Reports.CountAsync());
        Assert.Equal(0, await context.DbContext.ReportPhotos.CountAsync());
    }

    [Fact]
    public async Task OrphanedStorageCleanup_deletes_unreferenced_objects_past_grace_window()
    {
        await using var context = await ReportTestContext.CreateAsync(factory);
        var storage = Assert.IsType<FakeBucketStorage>(
            factory.Services.GetRequiredService<IBucketStorage>());
        const string orphanOriginalKey = "public/reports/orphan-report/orphan-photo";
        const string orphanThumbnailKey = "public/reports/orphan-report/orphan-photo_thumb.webp";

        await storage.PutAsync(orphanOriginalKey, new MemoryStream([1, 2, 3]), "image/jpeg");
        await storage.PutAsync(orphanThumbnailKey, new MemoryStream([4, 5, 6]), "image/webp");
        storage.SetLastModifiedForTesting(orphanOriginalKey, DateTimeOffset.UtcNow.AddHours(-2));
        storage.SetLastModifiedForTesting(orphanThumbnailKey, DateTimeOffset.UtcNow.AddHours(-2));

        var response = await RunJobAsync(context, "OrphanedStorageCleanup");
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        Assert.False(storage.ContainsKey(orphanOriginalKey));
        Assert.False(storage.ContainsKey(orphanThumbnailKey));
    }

    [Fact]
    public async Task SubmitClaim_compensating_delete_removes_photos_when_save_fails()
    {
        await using var context = await ReportTestContext.CreateAsync(factory);
        var storage = Assert.IsType<FakeBucketStorage>(
            factory.Services.GetRequiredService<IBucketStorage>());
        var reportId = await ClaimTestHelpers.PublishLostReportAsync(context);
        var claimant = await ClaimTestHelpers.CreateAndLoginClaimantAsync(context);
        ClaimTestHelpers.Authenticate(context.Client, claimant.AccessToken);
        factory.SaveChangesInterceptor.FailNextClaimSave = true;

        var (response, _) = await ClaimTestHelpers.SubmitClaimAsync(
            context.Client,
            reportId,
            new SubmitClaimRequest
            {
                SubmittedAnswer = ClaimTestHelpers.ValidAnswer,
            },
            [TestImageFactory.CreateMinimalJpeg()]);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Empty(await storage.ListAsync("private/claims/"));
        Assert.Equal(0, await context.DbContext.Claims.CountAsync());
    }

    [Fact]
    public async Task SubmitClaim_conflict_after_withdraw_race_deletes_photos_and_skips_insert()
    {
        await using var context = await ReportTestContext.CreateAsync(factory);
        var storage = Assert.IsType<FakeBucketStorage>(
            factory.Services.GetRequiredService<IBucketStorage>());
        var reportId = await ClaimTestHelpers.PublishLostReportAsync(context);
        var claimant = await ClaimTestHelpers.CreateAndLoginClaimantAsync(context);
        ClaimTestHelpers.Authenticate(context.Client, claimant.AccessToken);

        factory.RaceCommandInterceptor.TargetReportId = reportId;
        factory.RaceCommandInterceptor.Injection = ClaimSubmitRaceInjection.WithdrawReport;

        var (response, _) = await ClaimTestHelpers.SubmitClaimAsync(
            context.Client,
            reportId,
            new SubmitClaimRequest
            {
                SubmittedAnswer = ClaimTestHelpers.ValidAnswer,
            },
            [TestImageFactory.CreateMinimalJpeg()]);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var error = await HttpTestHelpers.ReadErrorAsync(response);

        Assert.Equal(ErrorCodes.ClaimInvalidStatus, error?.Code);
        Assert.Empty(await storage.ListAsync("private/claims/"));
        Assert.Equal(0, await ClaimTestHelpers.CountClaimsAsync(context, reportId, claimant.User.Id));
    }

    [Fact]
    public async Task SubmitClaim_conflict_when_pending_exists_after_race_deletes_photos()
    {
        await using var context = await ReportTestContext.CreateAsync(factory);
        var storage = Assert.IsType<FakeBucketStorage>(
            factory.Services.GetRequiredService<IBucketStorage>());
        var reportId = await ClaimTestHelpers.PublishLostReportAsync(context);
        var claimant = await ClaimTestHelpers.CreateAndLoginClaimantAsync(context);
        ClaimTestHelpers.Authenticate(context.Client, claimant.AccessToken);

        factory.RaceCommandInterceptor.TargetReportId = reportId;
        factory.RaceCommandInterceptor.TargetClaimantId = claimant.User.Id;
        factory.RaceCommandInterceptor.Injection = ClaimSubmitRaceInjection.InsertPendingClaim;

        var (response, _) = await ClaimTestHelpers.SubmitClaimAsync(
            context.Client,
            reportId,
            new SubmitClaimRequest
            {
                SubmittedAnswer = ClaimTestHelpers.ValidAnswer,
            },
            [TestImageFactory.CreateMinimalJpeg()]);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var error = await HttpTestHelpers.ReadErrorAsync(response);

        Assert.Equal(ErrorCodes.ClaimPendingExists, error?.Code);
        Assert.Empty(await storage.ListAsync("private/claims/"));
        Assert.Equal(1, await ClaimTestHelpers.CountClaimsAsync(context, reportId, claimant.User.Id));
    }

    [Fact]
    public async Task OrphanedStorageCleanup_deletes_unreferenced_claim_photos_past_grace_window()
    {
        await using var context = await ReportTestContext.CreateAsync(factory);
        var storage = Assert.IsType<FakeBucketStorage>(
            factory.Services.GetRequiredService<IBucketStorage>());
        const string orphanOriginalKey = "private/claims/orphanclaim/orphanphoto";
        const string orphanThumbnailKey = "private/claims/orphanclaim/orphanphoto_thumb.webp";

        await storage.PutAsync(orphanOriginalKey, new MemoryStream([1, 2, 3]), "image/jpeg");
        await storage.PutAsync(orphanThumbnailKey, new MemoryStream([4, 5, 6]), "image/webp");
        storage.SetLastModifiedForTesting(orphanOriginalKey, DateTimeOffset.UtcNow.AddHours(-2));
        storage.SetLastModifiedForTesting(orphanThumbnailKey, DateTimeOffset.UtcNow.AddHours(-2));

        var response = await RunJobAsync(context, "OrphanedStorageCleanup");
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        Assert.False(storage.ContainsKey(orphanOriginalKey));
        Assert.False(storage.ContainsKey(orphanThumbnailKey));
    }

    [Fact]
    public async Task OrphanedStorageCleanup_keeps_referenced_claim_photo_keys()
    {
        await using var context = await ReportTestContext.CreateAsync(factory);
        var storage = Assert.IsType<FakeBucketStorage>(
            factory.Services.GetRequiredService<IBucketStorage>());
        var reportId = await ClaimTestHelpers.PublishLostReportAsync(context);
        const string referencedOriginalKey = "private/claims/referenced/original";
        const string referencedThumbnailKey = "private/claims/referenced/original_thumb.webp";

        context.DbContext.Claims.Add(new Claim
        {
            Id = Guid.NewGuid(),
            ReportId = reportId,
            ClaimantId = context.Session.User.Id,
            Status = ClaimStatus.Pending,
            SubmittedAnswer = ClaimTestHelpers.ValidAnswer,
            SubmittedAt = DateTimeOffset.UtcNow,
            AttemptNumber = 1,
            CountsAsFailure = false,
            PhotoStorageKey = referencedOriginalKey,
        });
        await context.DbContext.SaveChangesAsync();

        await storage.PutAsync(referencedOriginalKey, new MemoryStream([1]), "image/jpeg");
        await storage.PutAsync(referencedThumbnailKey, new MemoryStream([2]), "image/webp");
        storage.SetLastModifiedForTesting(referencedOriginalKey, DateTimeOffset.UtcNow.AddHours(-2));
        storage.SetLastModifiedForTesting(referencedThumbnailKey, DateTimeOffset.UtcNow.AddHours(-2));

        var response = await RunJobAsync(context, "OrphanedStorageCleanup");
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        Assert.True(storage.ContainsKey(referencedOriginalKey));
        Assert.True(storage.ContainsKey(referencedThumbnailKey));
    }

    [Fact]
    public async Task OrphanedStorageCleanup_keeps_referenced_keys_and_recent_orphans()
    {
        await using var context = await ReportTestContext.CreateAsync(factory);
        var storage = Assert.IsType<FakeBucketStorage>(
            factory.Services.GetRequiredService<IBucketStorage>());
        var reportId = Guid.NewGuid();
        var photoId = Guid.NewGuid();
        const string referencedOriginalKey = "public/reports/referenced/original";
        const string referencedThumbnailKey = "public/reports/referenced/thumb.webp";
        const string recentOrphanKey = "public/reports/recent/orphan";

        var category = await context.DbContext.Categories.SingleAsync(item => item.Code == "phones");
        var governorate = await context.DbContext.Governorates.SingleAsync(item => item.Code == "cairo");

        context.DbContext.Reports.Add(new Report
        {
            Id = reportId,
            ReporterId = context.Session.User.Id,
            Type = ReportType.Lost,
            CategoryId = category.Id,
            Title = "Referenced photo retention test",
            Description = "Description long enough for validation",
            DateLostOrFound = DateOnly.FromDateTime(DateTime.UtcNow),
            GovernorateId = governorate.Id,
            Status = ReportStatus.PendingReview,
            HiddenDetail = "Hidden detail text",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        });
        context.DbContext.ReportPhotos.Add(new ReportPhoto
        {
            Id = photoId,
            ReportId = reportId,
            StorageKey = referencedOriginalKey,
            ThumbnailStorageKey = referencedThumbnailKey,
            ContentType = "image/jpeg",
            SizeBytes = 1024,
            SortOrder = 0,
        });
        await context.DbContext.SaveChangesAsync();

        await storage.PutAsync(referencedOriginalKey, new MemoryStream([1]), "image/jpeg");
        await storage.PutAsync(referencedThumbnailKey, new MemoryStream([2]), "image/webp");
        await storage.PutAsync(recentOrphanKey, new MemoryStream([3]), "image/jpeg");
        storage.SetLastModifiedForTesting(referencedOriginalKey, DateTimeOffset.UtcNow.AddHours(-2));
        storage.SetLastModifiedForTesting(referencedThumbnailKey, DateTimeOffset.UtcNow.AddHours(-2));
        storage.SetLastModifiedForTesting(recentOrphanKey, DateTimeOffset.UtcNow.AddMinutes(-10));

        var response = await RunJobAsync(context, "OrphanedStorageCleanup");
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        Assert.True(storage.ContainsKey(referencedOriginalKey));
        Assert.True(storage.ContainsKey(referencedThumbnailKey));
        Assert.True(storage.ContainsKey(recentOrphanKey));
    }

    private static async Task<HttpResponseMessage> RunJobAsync(ReportTestContext context, string jobName)
    {
        await HttpTestHelpers.LoginAsAdminAsync(context);
        return await context.Client.PostAsync($"/api/v1/admin/test/run-job/{jobName}", null);
    }
}

public sealed class FailSaveChangesInterceptor : SaveChangesInterceptor
{
    public bool FailNextReportSave { get; set; }

    public bool FailNextClaimSave { get; set; }

    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        MaybeThrowForTrackedEntities(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        MaybeThrowForTrackedEntities(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void MaybeThrowForTrackedEntities(DbContext? context)
    {
        if (context is null)
        {
            return;
        }

        if (FailNextReportSave
            && context.ChangeTracker.Entries<Report>().Any(entry => entry.State == EntityState.Added))
        {
            FailNextReportSave = false;
            throw new DbUpdateException("Forced report save failure for test.");
        }

        if (FailNextClaimSave
            && context.ChangeTracker.Entries<Claim>().Any(entry => entry.State == EntityState.Added))
        {
            FailNextClaimSave = false;
            throw new DbUpdateException("Forced claim save failure for test.");
        }
    }
}

public enum ClaimSubmitRaceInjection
{
    None,
    WithdrawReport,
    InsertPendingClaim,
}

public sealed class ClaimSubmitRaceCommandInterceptor(string connectionString) : DbCommandInterceptor
{
    public Guid? TargetReportId { get; set; }

    public Guid? TargetClaimantId { get; set; }

    public ClaimSubmitRaceInjection Injection { get; set; }

    public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<DbDataReader> result,
        CancellationToken cancellationToken = default)
    {
        MaybeInjectRace(command);
        return base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
    }

    public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        MaybeInjectRace(command);
        return base.NonQueryExecutingAsync(command, eventData, result, cancellationToken);
    }

    private void MaybeInjectRace(DbCommand command)
    {
        if (Injection == ClaimSubmitRaceInjection.None
            || TargetReportId is null
            || !command.CommandText.Contains("FOR UPDATE", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var injection = Injection;
        Injection = ClaimSubmitRaceInjection.None;

        using var connection = new NpgsqlConnection(connectionString);
        connection.Open();
        using var transaction = connection.BeginTransaction();

        switch (injection)
        {
            case ClaimSubmitRaceInjection.WithdrawReport:
                using (var withdrawCommand = new NpgsqlCommand(
                    """
                    UPDATE reports
                    SET "Status" = 'Withdrawn', "UpdatedAt" = NOW()
                    WHERE "Id" = @reportId AND "Status" = 'Published'
                    """,
                    connection,
                    transaction))
                {
                    withdrawCommand.Parameters.AddWithValue("reportId", TargetReportId.Value);
                    withdrawCommand.ExecuteNonQuery();
                }

                break;
            case ClaimSubmitRaceInjection.InsertPendingClaim:
                if (TargetClaimantId is null)
                {
                    break;
                }

                using (var insertCommand = new NpgsqlCommand(
                    """
                    INSERT INTO claims (
                        "Id",
                        "ReportId",
                        "ClaimantId",
                        "Status",
                        "SubmittedAnswer",
                        "SubmittedAt",
                        "AttemptNumber",
                        "CountsAsFailure")
                    VALUES (
                        @claimId,
                        @reportId,
                        @claimantId,
                        'Pending',
                        @submittedAnswer,
                        NOW(),
                        1,
                        FALSE)
                    """,
                    connection,
                    transaction))
                {
                    insertCommand.Parameters.AddWithValue("claimId", Guid.NewGuid());
                    insertCommand.Parameters.AddWithValue("reportId", TargetReportId.Value);
                    insertCommand.Parameters.AddWithValue("claimantId", TargetClaimantId.Value);
                    insertCommand.Parameters.AddWithValue("submittedAnswer", ClaimTestHelpers.ValidAnswer);
                    insertCommand.ExecuteNonQuery();
                }

                break;
        }

        transaction.Commit();
    }
}
