using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Amanah.Api.Data.Entities;
using Amanah.Api.Tests.Infrastructure;
using Amanah.Api.Tests.Uploads;
using Amanah.Api.Utilities.Reports;
using Amanah.Contracts.Errors;
using Amanah.Contracts.Requests.Admin;
using Amanah.Contracts.Responses.Admin;
using Amanah.Contracts.Responses.Reports;
using Microsoft.EntityFrameworkCore;

namespace Amanah.Api.Tests.Reports;

public class ReportResubmitTests(ApiWebApplicationFactory factory) : IClassFixture<ApiWebApplicationFactory>
{
    [Fact]
    public async Task Resubmit_after_reject_sets_pending_review_without_consuming_daily_quota()
    {
        await using var context = await ReportTestContext.CreateAsync(factory);
        CreateReportResponse? rejectedReport = null;

        for (var i = 0; i < 3; i++)
        {
            var (_, created) = await context.SubmitReportAsync(
                TestReportHelpers.BuildValidLostRequest(title: $"Lost phone {i + 1}"));
            Assert.NotNull(created);

            if (i == 0)
            {
                rejectedReport = created;
            }
        }

        Assert.NotNull(rejectedReport);
        await RejectAsAdminAsync(context, rejectedReport.Id);

        var resubmitResponse = await context.ResubmitReportAsync(rejectedReport.Id);
        Assert.Equal(HttpStatusCode.NoContent, resubmitResponse.StatusCode);

        var report = await context.DbContext.Reports
            .AsNoTracking()
            .SingleAsync(item => item.Id == rejectedReport.Id);
        Assert.Equal(ReportStatus.PendingReview, report.Status);
        Assert.Equal(1, report.ResubmissionCount);

        var (fourthResponse, fourth) = await context.SubmitReportAsync(
            TestReportHelpers.BuildValidLostRequest(title: "Fourth report today"));
        Assert.Equal(HttpStatusCode.TooManyRequests, fourthResponse.StatusCode);
        Assert.Null(fourth);
    }

    [Fact]
    public async Task Resubmit_succeeds_when_open_cap_is_full()
    {
        await using var context = await ReportTestContext.CreateAsync(factory);
        var reporterId = context.Session.User.Id;
        var category = await context.DbContext.Categories.AsNoTracking().FirstAsync();
        var governorate = await context.DbContext.Governorates.AsNoTracking().FirstAsync();
        var now = DateTimeOffset.UtcNow;
        var rejectedId = Guid.NewGuid();

        for (var i = 0; i < 5; i++)
        {
            context.DbContext.Reports.Add(new Report
            {
                Id = Guid.NewGuid(),
                ReporterId = reporterId,
                Type = ReportType.Lost,
                CategoryId = category.Id,
                GovernorateId = governorate.Id,
                Title = $"Open report {i + 1}",
                Description = "Open report description with enough length for validation.",
                DateLostOrFound = DateOnly.FromDateTime(now.UtcDateTime),
                Status = ReportStatus.PendingReview,
                HiddenDetail = "Hidden verification detail with enough length.",
                CreatedAt = now.AddMinutes(-i),
                UpdatedAt = now.AddMinutes(-i),
            });
        }

        context.DbContext.Reports.Add(new Report
        {
            Id = rejectedId,
            ReporterId = reporterId,
            Type = ReportType.Lost,
            CategoryId = category.Id,
            GovernorateId = governorate.Id,
            Title = "Rejected report",
            Description = "Rejected report description with enough length for validation.",
            DateLostOrFound = DateOnly.FromDateTime(now.UtcDateTime),
            Status = ReportStatus.Rejected,
            HiddenDetail = "Hidden verification detail with enough length.",
            ResubmissionCount = 0,
            CreatedAt = now.AddMinutes(-10),
            UpdatedAt = now.AddMinutes(-1),
            CategoryFields =
            [
                new CategoryField
                {
                    Id = Guid.NewGuid(),
                    FieldKey = "brand_model",
                    Value = "iPhone 14",
                },
                new CategoryField
                {
                    Id = Guid.NewGuid(),
                    FieldKey = "colour",
                    Value = "black",
                },
            ],
        });

        await context.DbContext.SaveChangesAsync();

        var resubmitResponse = await context.ResubmitReportAsync(rejectedId);
        Assert.Equal(HttpStatusCode.NoContent, resubmitResponse.StatusCode);

        var report = await context.DbContext.Reports
            .AsNoTracking()
            .SingleAsync(item => item.Id == rejectedId);
        Assert.Equal(ReportStatus.PendingReview, report.Status);
    }

    [Fact]
    public async Task Resubmit_rejects_contact_info_in_public_fields()
    {
        await using var context = await ReportTestContext.CreateAsync(factory);
        var (_, created) = await context.SubmitReportAsync(TestReportHelpers.BuildValidLostRequest());
        Assert.NotNull(created);

        await RejectAsAdminAsync(context, created.Id);

        var report = await context.DbContext.Reports
            .SingleAsync(item => item.Id == created.Id);
        report.Title = "Call me 01012345678 please";
        await context.DbContext.SaveChangesAsync();

        var resubmitResponse = await context.ResubmitReportAsync(created.Id);
        var error = await context.ReadErrorAsync(resubmitResponse);

        Assert.Equal(HttpStatusCode.BadRequest, resubmitResponse.StatusCode);
        Assert.Contains(
            ContactInfoDetector.ContactInfoMessage,
            error!.Errors![ReportContentValidator.TitleField]);
    }

    [Fact]
    public async Task Resubmit_refused_after_third_resubmission()
    {
        await using var context = await ReportTestContext.CreateAsync(factory);
        var (_, created) = await context.SubmitReportAsync(TestReportHelpers.BuildValidLostRequest());
        Assert.NotNull(created);

        await RejectAsAdminAsync(context, created.Id);

        for (var cycle = 0; cycle < 3; cycle++)
        {
            var resubmitResponse = await context.ResubmitReportAsync(created.Id);
            Assert.Equal(HttpStatusCode.NoContent, resubmitResponse.StatusCode);
            await RejectAsAdminAsync(context, created.Id);
        }

        var fourthResubmit = await context.ResubmitReportAsync(created.Id);
        var error = await context.ReadErrorAsync(fourthResubmit);

        Assert.Equal(HttpStatusCode.Conflict, fourthResubmit.StatusCode);
        Assert.Equal(ErrorCodes.ReportResubmitCap, error?.Code);
    }

    [Fact]
    public async Task Update_changes_photo_storage_prefix_when_category_becomes_private()
    {
        await using var context = await ReportTestContext.CreateAsync(factory);
        var request = TestReportHelpers.BuildValidLostRequest();

        var (_, created) = await context.SubmitReportAsync(
            request,
            [TestImageFactory.CreateMinimalJpeg()]);
        Assert.NotNull(created);

        await RejectAsAdminAsync(context, created.Id);

        var updateResponse = await context.UpdateReportAsync(
            created.Id,
            TestReportHelpers.BuildValidUpdateRequest(
                categoryCode: "documents-ids",
                categoryFields: new Dictionary<string, string>
                {
                    ["document_type"] = "National ID",
                    ["first_name_on_document"] = "Ahmed",
                }));
        Assert.Equal(HttpStatusCode.NoContent, updateResponse.Response.StatusCode);

        var photo = await context.DbContext.ReportPhotos
            .AsNoTracking()
            .SingleAsync(item => item.ReportId == created.Id);

        Assert.StartsWith("private/", photo.StorageKey, StringComparison.Ordinal);
        Assert.StartsWith("private/", photo.ThumbnailStorageKey, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Update_refused_for_pending_review_report()
    {
        await using var context = await ReportTestContext.CreateAsync(factory);
        var (_, created) = await context.SubmitReportAsync(TestReportHelpers.BuildValidLostRequest());
        Assert.NotNull(created);

        var updateResponse = await context.UpdateReportAsync(
            created.Id,
            TestReportHelpers.BuildValidUpdateRequest(title: "Updated title"));
        var error = await context.ReadErrorAsync(updateResponse.Response);

        Assert.Equal(HttpStatusCode.Conflict, updateResponse.Response.StatusCode);
        Assert.Equal(ErrorCodes.Conflict, error?.Code);
    }

    [Fact]
    public async Task Resubmitted_report_reappears_in_admin_moderation_queue()
    {
        await using var context = await ReportTestContext.CreateAsync(factory);
        var (_, created) = await context.SubmitReportAsync(TestReportHelpers.BuildValidLostRequest());
        Assert.NotNull(created);

        await RejectAsAdminAsync(context, created.Id);

        var resubmitResponse = await context.ResubmitReportAsync(created.Id);
        Assert.Equal(HttpStatusCode.NoContent, resubmitResponse.StatusCode);

        var (loginResponse, adminSession) = await context.Auth.LoginAsync("01011111111", "AdminPass123");
        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);
        Assert.NotNull(adminSession);

        context.Client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", adminSession.AccessToken);

        var queueResponse = await context.Client.GetAsync("/api/v1/admin/moderation/queue");
        var queue = await queueResponse.Content.ReadFromJsonAsync<ModerationQueueResponse>();

        Assert.Equal(HttpStatusCode.OK, queueResponse.StatusCode);
        Assert.NotNull(queue);
        Assert.Contains(queue.Items, item => item.Id == created.Id);
    }

    [Fact]
    public async Task Get_mine_returns_rejected_reports_for_rejected_filter()
    {
        await using var context = await ReportTestContext.CreateAsync(factory);
        var (_, created) = await context.SubmitReportAsync(TestReportHelpers.BuildValidLostRequest());
        Assert.NotNull(created);

        await RejectAsAdminAsync(context, created.Id);

        var (response, body) = await context.GetMineAsync("rejected");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body);
        Assert.Single(body.Items);
        Assert.Equal(created.Id, body.Items[0].Id);
        Assert.Equal("rejected", body.Items[0].Status);
    }

    private static async Task RejectAsAdminAsync(ReportTestContext context, Guid reportId)
    {
        var (loginResponse, adminSession) = await context.Auth.LoginAsync("01011111111", "AdminPass123");
        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);
        Assert.NotNull(adminSession);

        context.Client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", adminSession.AccessToken);

        var rejectResponse = await context.Client.PostAsJsonAsync(
            $"/api/v1/admin/moderation/reports/{reportId}/reject",
            new RejectReportRequest
            {
                ReasonCode = "rejection.insufficient_description",
                Note = "Please add more detail.",
            });
        Assert.Equal(HttpStatusCode.NoContent, rejectResponse.StatusCode);

        context.Client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", context.Session.AccessToken);
    }
}
