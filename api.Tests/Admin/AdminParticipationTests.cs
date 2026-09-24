using System.Net;
using System.Net.Http.Json;
using Amanah.Api.Data.Entities;
using Amanah.Api.Services.Admin;
using Amanah.Api.Tests.Browse;
using Amanah.Api.Tests.Chats;
using Amanah.Api.Tests.Claims;
using Amanah.Api.Tests.Infrastructure;
using Amanah.Api.Tests.Reports;
using Amanah.Api.Utilities.Abuse;
using Amanah.Contracts.Errors;
using Amanah.Contracts.Requests.Abuse;
using Amanah.Contracts.Requests.Chats;
using Amanah.Contracts.Responses.Browse;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Amanah.Api.Tests.Admin;

public class AdminParticipationTests(ApiWebApplicationFactory factory) : IClassFixture<ApiWebApplicationFactory>
{
    [Fact]
    public async Task Admin_cannot_submit_report()
    {
        await using var context = await ReportTestContext.CreateAsync(factory);
        await HttpTestHelpers.LoginAsAdminAsync(context);

        var (response, _) = await context.SubmitReportAsync(TestReportHelpers.BuildValidLostRequest());
        var error = await HttpTestHelpers.ReadErrorAsync(response);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(ErrorCodes.AdminParticipationForbidden, error?.Code);
    }

    [Fact]
    public async Task Admin_cannot_flag_or_claim_published_listing()
    {
        await using var context = await ReportTestContext.CreateAsync(factory);
        var reportId = await ClaimTestHelpers.PublishLostReportAsync(context);

        await HttpTestHelpers.LoginAsAdminAsync(context);

        var flagResponse = await context.Client.PostAsJsonAsync(
            $"/api/v1/reports/{reportId}/flag",
            new FlagListingRequest { Reason = AbuseFlagReasons.Spam });
        var flagError = await HttpTestHelpers.ReadErrorAsync(flagResponse);

        Assert.Equal(HttpStatusCode.Forbidden, flagResponse.StatusCode);
        Assert.Equal(ErrorCodes.AdminParticipationForbidden, flagError?.Code);

        var (claimResponse, _) = await ClaimTestHelpers.SubmitClaimAsync(context.Client, reportId);
        var claimError = await HttpTestHelpers.ReadErrorAsync(claimResponse);

        Assert.Equal(HttpStatusCode.Forbidden, claimResponse.StatusCode);
        Assert.Equal(ErrorCodes.AdminParticipationForbidden, claimError?.Code);
    }

    [Fact]
    public async Task Admin_cannot_send_chat_message()
    {
        await using var context = await ReportTestContext.CreateAsync(factory);
        var reportId = await ClaimTestHelpers.PublishLostReportAsync(context);

        var claimantSession = await ClaimTestHelpers.CreateAndLoginClaimantAsync(context);
        ClaimTestHelpers.Authenticate(context.Client, claimantSession.AccessToken);
        var (_, claimBody) = await ClaimTestHelpers.SubmitClaimAsync(context.Client, reportId);
        Assert.NotNull(claimBody);

        ClaimTestHelpers.Authenticate(context.Client, context.Session.AccessToken);
        await ClaimTestHelpers.ApproveClaimAsync(context.Client, claimBody.Id);
        var threadId = await ChatTestHelpers.GetThreadIdAsync(context.Client, claimBody.Id);

        await HttpTestHelpers.LoginAsAdminAsync(context);

        var sendResponse = await context.Client.PostAsJsonAsync(
            $"/api/v1/chats/{threadId}/messages",
            new SendMessageRequest { Body = "Admin should not send this." });
        var sendError = await HttpTestHelpers.ReadErrorAsync(sendResponse);

        Assert.Equal(HttpStatusCode.Forbidden, sendResponse.StatusCode);
        Assert.Equal(ErrorCodes.AdminParticipationForbidden, sendError?.Code);
    }

    [Fact]
    public async Task Admin_can_browse_public_detail_and_use_moderation_queue()
    {
        await using var context = await ReportTestContext.CreateAsync(factory);
        var reportId = await ClaimTestHelpers.PublishLostReportAsync(context);

        await HttpTestHelpers.LoginAsAdminAsync(context);

        var detailResponse = await context.Client.GetAsync($"/api/v1/reports/{reportId}/public");
        var detail = await detailResponse.Content.ReadFromJsonAsync<PublicReportDetailResponse>();

        Assert.Equal(HttpStatusCode.OK, detailResponse.StatusCode);
        Assert.NotNull(detail);
        Assert.Equal(reportId, detail.Id);

        var queueResponse = await context.Client.GetAsync("/api/v1/admin/moderation/queue");
        Assert.Equal(HttpStatusCode.OK, queueResponse.StatusCode);
    }

    [Fact]
    public async Task Admin_can_read_investigation_chat_but_cannot_send()
    {
        await using var context = await ReportTestContext.CreateAsync(factory);
        var reportId = await ClaimTestHelpers.PublishLostReportAsync(context);

        var claimantSession = await ClaimTestHelpers.CreateAndLoginClaimantAsync(context);
        ClaimTestHelpers.Authenticate(context.Client, claimantSession.AccessToken);
        var (_, claimBody) = await ClaimTestHelpers.SubmitClaimAsync(context.Client, reportId);
        Assert.NotNull(claimBody);

        ClaimTestHelpers.Authenticate(context.Client, context.Session.AccessToken);
        await ClaimTestHelpers.ApproveClaimAsync(context.Client, claimBody.Id);
        var threadId = await ChatTestHelpers.GetThreadIdAsync(context.Client, claimBody.Id);

        ClaimTestHelpers.Authenticate(context.Client, claimantSession.AccessToken);
        await ChatTestHelpers.SendMessageAsync(context.Client, threadId, "User message");

        var flaggerSession = await ClaimTestHelpers.CreateAndLoginClaimantAsync(context);
        ClaimTestHelpers.Authenticate(context.Client, flaggerSession.AccessToken);
        var flagResponse = await context.Client.PostAsJsonAsync(
            $"/api/v1/reports/{reportId}/flag",
            new FlagListingRequest { Reason = AbuseFlagReasons.Other });
        Assert.Equal(HttpStatusCode.OK, flagResponse.StatusCode);

        await HttpTestHelpers.LoginAsAdminAsync(context);

        var investigationResponse = await context.Client.GetAsync(
            $"/api/v1/admin/investigations/{reportId}/chat");
        Assert.Equal(HttpStatusCode.OK, investigationResponse.StatusCode);

        var threadResponse = await context.Client.GetAsync($"/api/v1/chats/{threadId}");
        Assert.Equal(HttpStatusCode.OK, threadResponse.StatusCode);

        var sendResponse = await context.Client.PostAsJsonAsync(
            $"/api/v1/chats/{threadId}/messages",
            new SendMessageRequest { Body = "Admin reply." });
        var sendError = await HttpTestHelpers.ReadErrorAsync(sendResponse);

        Assert.Equal(HttpStatusCode.Forbidden, sendResponse.StatusCode);
        Assert.Equal(ErrorCodes.AdminParticipationForbidden, sendError?.Code);
    }

    [Fact]
    public async Task Purge_removes_admin_participation_and_restores_others_report()
    {
        await using var context = await ReportTestContext.CreateAsync(factory);
        var userReportId = await ClaimTestHelpers.PublishLostReportAsync(context);

        var admin = await context.DbContext.Users
            .SingleAsync(user => user.Role == UserRole.Admin);
        var category = await context.DbContext.Categories.AsNoTracking().FirstAsync();
        var governorate = await context.DbContext.Governorates.AsNoTracking().FirstAsync();
        var now = DateTimeOffset.UtcNow;

        var adminOwnedReport = new Report
        {
            Id = Guid.NewGuid(),
            ReporterId = admin.Id,
            Type = ReportType.Lost,
            CategoryId = category.Id,
            GovernorateId = governorate.Id,
            Title = "Admin owned report to purge",
            Description = "Should be hard-deleted by participation purge.",
            DateLostOrFound = DateOnly.FromDateTime(now.UtcDateTime.AddDays(-1)),
            Status = ReportStatus.Published,
            PublishedAt = now,
            NormalizedSearchText = "admin owned report to purge",
            CreatedAt = now,
            UpdatedAt = now,
        };
        context.DbContext.Reports.Add(adminOwnedReport);

        var adminClaim = new Claim
        {
            Id = Guid.NewGuid(),
            ReportId = userReportId,
            ClaimantId = admin.Id,
            Status = ClaimStatus.Pending,
            SubmittedAnswer = ClaimTestHelpers.ValidAnswer,
            SubmittedAt = now,
            AttemptNumber = 1,
            CountsAsFailure = false,
        };
        context.DbContext.Claims.Add(adminClaim);

        var adminFlag = new AbuseReport
        {
            Id = Guid.NewGuid(),
            AbuseReporterId = admin.Id,
            ReportId = userReportId,
            Reason = AbuseFlagReasons.Spam,
            Status = AbuseReportStatus.Open,
            CreatedAt = now,
        };
        context.DbContext.AbuseReports.Add(adminFlag);

        context.DbContext.Notifications.Add(new Notification
        {
            Id = Guid.NewGuid(),
            UserId = admin.Id,
            Type = "NewClaimSubmitted",
            PayloadJson = "{}",
            IsRead = false,
            CreatedAt = now,
        });

        await context.DbContext.SaveChangesAsync();

        await using var purgeScope = factory.Services.CreateAsyncScope();
        await purgeScope.ServiceProvider
            .GetRequiredService<AdminParticipationPurgeService>()
            .PurgeAsync();

        context.DbContext.ChangeTracker.Clear();

        Assert.False(await context.DbContext.Reports.AnyAsync(report => report.Id == adminOwnedReport.Id));
        Assert.False(await context.DbContext.Claims.AnyAsync(claim => claim.ClaimantId == admin.Id));
        Assert.False(await context.DbContext.AbuseReports.AnyAsync(
            abuseReport => abuseReport.AbuseReporterId == admin.Id));
        Assert.False(await context.DbContext.Notifications.AnyAsync(
            notification => notification.UserId == admin.Id));

        var userReport = await context.DbContext.Reports
            .AsNoTracking()
            .SingleAsync(report => report.Id == userReportId);
        Assert.Equal(ReportStatus.Published, userReport.Status);
    }

    [Fact]
    public async Task Purge_cancels_admin_approved_claim_and_restores_report()
    {
        await using var context = await ReportTestContext.CreateAsync(factory);
        var reportId = await ClaimTestHelpers.PublishLostReportAsync(context);

        var claimantSession = await ClaimTestHelpers.CreateAndLoginClaimantAsync(context);
        ClaimTestHelpers.Authenticate(context.Client, claimantSession.AccessToken);
        var (_, claimBody) = await ClaimTestHelpers.SubmitClaimAsync(context.Client, reportId);
        Assert.NotNull(claimBody);

        ClaimTestHelpers.Authenticate(context.Client, context.Session.AccessToken);
        await ClaimTestHelpers.ApproveClaimAsync(context.Client, claimBody.Id);

        var admin = await context.DbContext.Users
            .SingleAsync(user => user.Role == UserRole.Admin);

        var claim = await context.DbContext.Claims
            .Include(existing => existing.ChatThread)
            .SingleAsync(existing => existing.Id == claimBody.Id);
        claim.ClaimantId = admin.Id;
        await context.DbContext.SaveChangesAsync();

        await using var purgeScope = factory.Services.CreateAsyncScope();
        await purgeScope.ServiceProvider
            .GetRequiredService<AdminParticipationPurgeService>()
            .PurgeAsync();

        context.DbContext.ChangeTracker.Clear();

        Assert.False(await context.DbContext.Claims.AnyAsync(existing => existing.Id == claimBody.Id));

        var report = await context.DbContext.Reports
            .AsNoTracking()
            .SingleAsync(existing => existing.Id == reportId);
        Assert.Equal(ReportStatus.Published, report.Status);
    }
}
