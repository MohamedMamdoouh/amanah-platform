using System.Net;
using System.Net.Http.Json;
using Amanah.Api.Data.Entities;
using Amanah.Api.Services.Storage;
using Amanah.Api.Services.Uploads;
using Amanah.Api.Tests.Claims;
using Amanah.Api.Tests.Infrastructure;
using Amanah.Api.Tests.Reports;
using Amanah.Api.Tests.Uploads;
using Amanah.Contracts.Requests.Admin;
using Amanah.Contracts.Requests.Claims;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Amanah.Api.Tests.Lifecycle;

public sealed class RejectedReportRetentionWebApplicationFactory : ApiWebApplicationFactory
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.UseSetting("Lifecycle:RetentionDays", "30");
    }
}

public class RejectedReportRetentionTests(RejectedReportRetentionWebApplicationFactory factory)
    : IClassFixture<RejectedReportRetentionWebApplicationFactory>
{
    [Fact]
    public async Task RejectedReportCleanup_deletes_expired_report_and_photos_but_keeps_moderation_action()
    {
        await using var context = await ReportTestContext.CreateAsync(factory);
        var (_, created) = await context.SubmitReportAsync(
            TestReportHelpers.BuildValidLostRequest(),
            [TestImageFactory.CreateMinimalJpeg()]);
        Assert.NotNull(created);

        await HttpTestHelpers.LoginAsAdminAsync(context);

        var rejectResponse = await context.Client.PostAsJsonAsync(
            $"/api/v1/admin/moderation/reports/{created.Id}/reject",
            new RejectReportRequest
            {
                ReasonCode = "rejection.wrong_category",
            });
        Assert.Equal(HttpStatusCode.NoContent, rejectResponse.StatusCode);

        var photo = await context.DbContext.ReportPhotos
            .AsNoTracking()
            .SingleAsync(item => item.ReportId == created.Id);
        var actionId = await context.DbContext.ModerationActions
            .AsNoTracking()
            .Where(action => action.ReportId == created.Id)
            .Select(action => action.Id)
            .SingleAsync();

        await BackdateLatestRejectionAsync(context, created.Id, daysAgo: 31);

        var storage = Assert.IsType<FakeBucketStorage>(
            factory.Services.GetRequiredService<IBucketStorage>());
        Assert.True(storage.ContainsKey(photo.StorageKey));

        var response = await RunJobAsync(context, "RejectedReportCleanup");
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        Assert.False(await context.DbContext.Reports.AnyAsync(item => item.Id == created.Id));
        Assert.Equal(0, await context.DbContext.ReportPhotos.CountAsync());
        Assert.True(storage.ContainsKey(photo.StorageKey));
        await StorageDeletionOutboxTestHelpers.ProcessPendingOutboxAsync(factory);
        Assert.False(storage.ContainsKey(photo.StorageKey));
        Assert.False(storage.ContainsKey(photo.ThumbnailStorageKey!));

        var survivingAction = await context.DbContext.ModerationActions
            .AsNoTracking()
            .SingleAsync(action => action.Id == actionId);
        Assert.Null(survivingAction.ReportId);
        Assert.Equal(ModerationDecision.Reject, survivingAction.Decision);
        Assert.Equal("rejection.wrong_category", survivingAction.ReasonCode);
    }

    [Fact]
    public async Task RejectedReportCleanup_leaves_fresh_rejected_report_unchanged()
    {
        await using var context = await ReportTestContext.CreateAsync(factory);
        var (_, created) = await context.SubmitReportAsync(TestReportHelpers.BuildValidLostRequest());
        Assert.NotNull(created);

        await HttpTestHelpers.LoginAsAdminAsync(context);

        var rejectResponse = await context.Client.PostAsJsonAsync(
            $"/api/v1/admin/moderation/reports/{created.Id}/reject",
            new RejectReportRequest
            {
                ReasonCode = "rejection.insufficient_description",
            });
        Assert.Equal(HttpStatusCode.NoContent, rejectResponse.StatusCode);

        var response = await RunJobAsync(context, "RejectedReportCleanup");
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        var report = await context.DbContext.Reports
            .AsNoTracking()
            .SingleAsync(item => item.Id == created.Id);
        Assert.Equal(ReportStatus.Rejected, report.Status);
        Assert.Equal(1, await context.DbContext.ModerationActions.CountAsync(
            action => action.ReportId == created.Id));
    }

    [Fact]
    public async Task RejectClaim_deletes_claim_photo_from_storage_and_database()
    {
        await using var context = await ReportTestContext.CreateAsync(factory);
        var reportId = await ClaimTestHelpers.PublishLostReportAsync(context);
        var claimant = await ClaimTestHelpers.CreateAndLoginClaimantAsync(context);
        ClaimTestHelpers.Authenticate(context.Client, claimant.AccessToken);

        var (_, submitted) = await ClaimTestHelpers.SubmitClaimAsync(
            context.Client,
            reportId,
            new SubmitClaimRequest
            {
                SubmittedAnswer = ClaimTestHelpers.ValidAnswer,
            },
            [TestImageFactory.CreateMinimalJpeg()]);
        Assert.NotNull(submitted);

        var claim = await context.DbContext.Claims
            .AsNoTracking()
            .SingleAsync(item => item.Id == submitted.Id);
        Assert.False(string.IsNullOrWhiteSpace(claim.PhotoStorageKey));

        var storage = Assert.IsType<FakeBucketStorage>(
            factory.Services.GetRequiredService<IBucketStorage>());
        Assert.True(storage.ContainsKey(claim.PhotoStorageKey!));

        ClaimTestHelpers.AuthenticateReporter(context.Client, context);
        var rejectResponse = await ClaimTestHelpers.RejectClaimAsync(context.Client, submitted.Id);
        Assert.Equal(HttpStatusCode.NoContent, rejectResponse.StatusCode);

        var updatedClaim = await context.DbContext.Claims
            .AsNoTracking()
            .SingleAsync(item => item.Id == submitted.Id);
        Assert.Null(updatedClaim.PhotoStorageKey);
        Assert.True(storage.ContainsKey(claim.PhotoStorageKey!));
        await StorageDeletionOutboxTestHelpers.ProcessPendingOutboxAsync(factory);
        Assert.False(storage.ContainsKey(claim.PhotoStorageKey!));
        Assert.False(storage.ContainsKey(
            ClaimPhotoStorageKeys.ThumbnailForOriginal(claim.PhotoStorageKey!)));
    }

    [Fact]
    public async Task WithdrawClaim_deletes_claim_photo_from_storage_and_database()
    {
        await using var context = await ReportTestContext.CreateAsync(factory);
        var reportId = await ClaimTestHelpers.PublishLostReportAsync(context);
        var claimant = await ClaimTestHelpers.CreateAndLoginClaimantAsync(context);
        ClaimTestHelpers.Authenticate(context.Client, claimant.AccessToken);

        var (_, submitted) = await ClaimTestHelpers.SubmitClaimAsync(
            context.Client,
            reportId,
            new SubmitClaimRequest
            {
                SubmittedAnswer = ClaimTestHelpers.ValidAnswer,
            },
            [TestImageFactory.CreateMinimalJpeg()]);
        Assert.NotNull(submitted);

        var claim = await context.DbContext.Claims
            .AsNoTracking()
            .SingleAsync(item => item.Id == submitted.Id);
        Assert.False(string.IsNullOrWhiteSpace(claim.PhotoStorageKey));

        var storage = Assert.IsType<FakeBucketStorage>(
            factory.Services.GetRequiredService<IBucketStorage>());
        Assert.True(storage.ContainsKey(claim.PhotoStorageKey!));

        var withdrawResponse = await ClaimTestHelpers.WithdrawClaimAsync(context.Client, submitted.Id);
        Assert.Equal(HttpStatusCode.NoContent, withdrawResponse.StatusCode);

        var updatedClaim = await context.DbContext.Claims
            .AsNoTracking()
            .SingleAsync(item => item.Id == submitted.Id);
        Assert.Null(updatedClaim.PhotoStorageKey);
        Assert.True(storage.ContainsKey(claim.PhotoStorageKey!));
        await StorageDeletionOutboxTestHelpers.ProcessPendingOutboxAsync(factory);
        Assert.False(storage.ContainsKey(claim.PhotoStorageKey!));
    }

    private static async Task BackdateLatestRejectionAsync(
        ReportTestContext context,
        Guid reportId,
        int daysAgo)
    {
        var action = await context.DbContext.ModerationActions
            .SingleAsync(item => item.ReportId == reportId && item.Decision == ModerationDecision.Reject);
        action.CreatedAt = DateTimeOffset.UtcNow.AddDays(-daysAgo);
        await context.DbContext.SaveChangesAsync();
    }

    private static async Task<HttpResponseMessage> RunJobAsync(ReportTestContext context, string jobName)
    {
        await HttpTestHelpers.LoginAsAdminAsync(context);
        return await context.Client.PostAsync($"/api/v1/admin/test/run-job/{jobName}", null);
    }
}
