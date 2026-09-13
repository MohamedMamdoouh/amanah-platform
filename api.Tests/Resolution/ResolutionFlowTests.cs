using System.Net;
using Amanah.Api.Data.Entities;
using Amanah.Api.Tests.Claims;
using Amanah.Api.Tests.Infrastructure;
using Amanah.Api.Tests.Reports;
using Amanah.Contracts.Errors;
using Microsoft.EntityFrameworkCore;

namespace Amanah.Api.Tests.Resolution;

public class ResolutionFlowTests(ApiWebApplicationFactory factory) : IClassFixture<ApiWebApplicationFactory>
{
    [Fact]
    public async Task First_confirm_keeps_report_claim_in_progress_and_notifies_counterparty()
    {
        await using var context = await ReportTestContext.CreateAsync(factory);
        var scenario = await ResolutionTestHelpers.CreateApprovedClaimScenarioAsync(context);

        ResolutionTestHelpers.AuthenticateReporter(context.Client, context);
        var response = await ResolutionTestHelpers.ConfirmResolutionAsync(context.Client, scenario.ClaimId);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        var report = await context.DbContext.Reports
            .AsNoTracking()
            .SingleAsync(existingReport => existingReport.Id == scenario.ReportId);
        Assert.Equal(ReportStatus.ClaimInProgress, report.Status);

        var resolution = await context.DbContext.Resolutions
            .AsNoTracking()
            .SingleAsync(existingResolution => existingResolution.ReportId == scenario.ReportId);
        Assert.NotNull(resolution.ReporterConfirmedAt);
        Assert.Null(resolution.ClaimantConfirmedAt);
        Assert.Null(resolution.ResolvedAt);

        var notification = await context.DbContext.Notifications
            .AsNoTracking()
            .SingleAsync(item =>
                item.UserId == scenario.ClaimantSession.User.Id
                && item.Type == "CounterpartyConfirmedResolution");
        Assert.Contains($"/lost/{scenario.ReportId}", notification.PayloadJson, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Second_confirm_resolves_report_notifies_both_and_makes_chat_read_only()
    {
        await using var context = await ReportTestContext.CreateAsync(factory);
        var scenario = await ResolutionTestHelpers.CreateApprovedClaimScenarioAsync(context);

        ResolutionTestHelpers.AuthenticateReporter(context.Client, context);
        var firstConfirm = await ResolutionTestHelpers.ConfirmResolutionAsync(context.Client, scenario.ClaimId);
        Assert.Equal(HttpStatusCode.NoContent, firstConfirm.StatusCode);

        ResolutionTestHelpers.AuthenticateClaimant(context.Client, scenario.ClaimantSession);
        var secondConfirm = await ResolutionTestHelpers.ConfirmResolutionAsync(context.Client, scenario.ClaimId);
        Assert.Equal(HttpStatusCode.NoContent, secondConfirm.StatusCode);

        var report = await context.DbContext.Reports
            .AsNoTracking()
            .SingleAsync(existingReport => existingReport.Id == scenario.ReportId);
        Assert.Equal(ReportStatus.Resolved, report.Status);

        var resolution = await context.DbContext.Resolutions
            .AsNoTracking()
            .SingleAsync(existingResolution => existingResolution.ReportId == scenario.ReportId);
        Assert.NotNull(resolution.ReporterConfirmedAt);
        Assert.NotNull(resolution.ClaimantConfirmedAt);
        Assert.NotNull(resolution.ResolvedAt);

        var chatThread = await context.DbContext.ChatThreads
            .AsNoTracking()
            .SingleAsync(thread => thread.ClaimId == scenario.ClaimId);
        Assert.NotNull(chatThread.ReadOnlyAt);

        var resolvedNotifications = await context.DbContext.Notifications
            .AsNoTracking()
            .Where(item => item.Type == "ReportResolved")
            .ToListAsync();
        Assert.Equal(2, resolvedNotifications.Count);
        Assert.Contains(resolvedNotifications, item => item.UserId == context.Session.User.Id);
        Assert.Contains(resolvedNotifications, item => item.UserId == scenario.ClaimantSession.User.Id);
    }

    [Fact]
    public async Task Confirmer_cannot_cancel_after_confirming()
    {
        await using var context = await ReportTestContext.CreateAsync(factory);
        var scenario = await ResolutionTestHelpers.CreateApprovedClaimScenarioAsync(context);

        ResolutionTestHelpers.AuthenticateReporter(context.Client, context);
        var confirmResponse = await ResolutionTestHelpers.ConfirmResolutionAsync(context.Client, scenario.ClaimId);
        Assert.Equal(HttpStatusCode.NoContent, confirmResponse.StatusCode);

        var cancelResponse = await ResolutionTestHelpers.CancelClaimAsync(context.Client, scenario.ClaimId);
        Assert.Equal(HttpStatusCode.Conflict, cancelResponse.StatusCode);

        var error = await ResolutionTestHelpers.ReadErrorAsync(cancelResponse);
        Assert.NotNull(error);
        Assert.Equal(ErrorCodes.ClaimInvalidStatus, error.Code);
    }

    [Fact]
    public async Task Unconfirmed_party_can_still_cancel_after_counterparty_confirms()
    {
        await using var context = await ReportTestContext.CreateAsync(factory);
        var scenario = await ResolutionTestHelpers.CreateApprovedClaimScenarioAsync(context);

        ResolutionTestHelpers.AuthenticateReporter(context.Client, context);
        var confirmResponse = await ResolutionTestHelpers.ConfirmResolutionAsync(context.Client, scenario.ClaimId);
        Assert.Equal(HttpStatusCode.NoContent, confirmResponse.StatusCode);

        ResolutionTestHelpers.AuthenticateClaimant(context.Client, scenario.ClaimantSession);
        var cancelResponse = await ResolutionTestHelpers.CancelClaimAsync(context.Client, scenario.ClaimId);
        Assert.Equal(HttpStatusCode.NoContent, cancelResponse.StatusCode);

        var claim = await context.DbContext.Claims
            .AsNoTracking()
            .SingleAsync(existingClaim => existingClaim.Id == scenario.ClaimId);
        Assert.Equal(ClaimStatus.Cancelled, claim.Status);

        var report = await context.DbContext.Reports
            .AsNoTracking()
            .SingleAsync(existingReport => existingReport.Id == scenario.ReportId);
        Assert.Equal(ReportStatus.Published, report.Status);
    }

    [Fact]
    public async Task Cancel_before_confirm_restores_published_sets_read_only_and_notifies_counterparty()
    {
        await using var context = await ReportTestContext.CreateAsync(factory);
        var scenario = await ResolutionTestHelpers.CreateApprovedClaimScenarioAsync(context);

        ResolutionTestHelpers.AuthenticateReporter(context.Client, context);
        var cancelResponse = await ResolutionTestHelpers.CancelClaimAsync(context.Client, scenario.ClaimId);
        Assert.Equal(HttpStatusCode.NoContent, cancelResponse.StatusCode);

        var claim = await context.DbContext.Claims
            .AsNoTracking()
            .SingleAsync(existingClaim => existingClaim.Id == scenario.ClaimId);
        Assert.Equal(ClaimStatus.Cancelled, claim.Status);
        Assert.Equal(context.Session.User.Id, claim.CancelledByUserId);

        var report = await context.DbContext.Reports
            .AsNoTracking()
            .SingleAsync(existingReport => existingReport.Id == scenario.ReportId);
        Assert.Equal(ReportStatus.Published, report.Status);

        var chatThread = await context.DbContext.ChatThreads
            .AsNoTracking()
            .SingleAsync(thread => thread.ClaimId == scenario.ClaimId);
        Assert.NotNull(chatThread.ReadOnlyAt);

        var notification = await context.DbContext.Notifications
            .AsNoTracking()
            .SingleAsync(item =>
                item.UserId == scenario.ClaimantSession.User.Id
                && item.Type == "ClaimCancelledByCounterparty");
        Assert.Contains($"/lost/{scenario.ReportId}", notification.PayloadJson, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Confirm_resolution_returns_not_found_for_non_participant()
    {
        await using var context = await ReportTestContext.CreateAsync(factory);
        var scenario = await ResolutionTestHelpers.CreateApprovedClaimScenarioAsync(context);

        var outsider = await ClaimTestHelpers.CreateAndLoginClaimantAsync(context);
        ClaimTestHelpers.Authenticate(context.Client, outsider.AccessToken);

        var response = await ResolutionTestHelpers.ConfirmResolutionAsync(context.Client, scenario.ClaimId);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Cancel_claim_returns_not_found_for_non_participant()
    {
        await using var context = await ReportTestContext.CreateAsync(factory);
        var scenario = await ResolutionTestHelpers.CreateApprovedClaimScenarioAsync(context);

        var outsider = await ClaimTestHelpers.CreateAndLoginClaimantAsync(context);
        ClaimTestHelpers.Authenticate(context.Client, outsider.AccessToken);

        var response = await ResolutionTestHelpers.CancelClaimAsync(context.Client, scenario.ClaimId);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Confirm_resolution_uses_found_report_deep_link_for_found_reports()
    {
        await using var context = await ReportTestContext.CreateAsync(factory);
        var scenario = await ResolutionTestHelpers.CreateApprovedClaimScenarioAsync(context, foundReport: true);

        ResolutionTestHelpers.AuthenticateReporter(context.Client, context);
        var response = await ResolutionTestHelpers.ConfirmResolutionAsync(context.Client, scenario.ClaimId);
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        var notification = await context.DbContext.Notifications
            .AsNoTracking()
            .SingleAsync(item =>
                item.UserId == scenario.ClaimantSession.User.Id
                && item.Type == "CounterpartyConfirmedResolution");
        Assert.Contains($"/found/{scenario.ReportId}", notification.PayloadJson, StringComparison.Ordinal);
    }
}
