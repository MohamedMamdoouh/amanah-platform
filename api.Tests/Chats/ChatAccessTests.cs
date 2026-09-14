using System.Net;
using System.Net.Http.Json;
using Amanah.Api.Tests.Claims;
using Amanah.Api.Tests.Infrastructure;
using Amanah.Api.Tests.Reports;
using Amanah.Api.Tests.Resolution;
using Amanah.Contracts.Responses.Chats;
using Microsoft.EntityFrameworkCore;

namespace Amanah.Api.Tests.Chats;

public class ChatAccessTests(ApiWebApplicationFactory factory) : IClassFixture<ApiWebApplicationFactory>
{
    [Fact]
    public async Task Get_thread_returns_not_found_for_non_participant()
    {
        await using var context = await ReportTestContext.CreateAsync(factory);
        var scenario = await ResolutionTestHelpers.CreateApprovedClaimScenarioAsync(context);
        var threadId = await ChatTestHelpers.GetThreadIdAsync(context.Client, scenario.ClaimId);

        var outsider = await ClaimTestHelpers.CreateAndLoginClaimantAsync(context);
        ClaimTestHelpers.Authenticate(context.Client, outsider.AccessToken);

        var response = await ChatTestHelpers.GetThreadAsync(context.Client, threadId);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Send_message_returns_not_found_for_non_participant()
    {
        await using var context = await ReportTestContext.CreateAsync(factory);
        var scenario = await ResolutionTestHelpers.CreateApprovedClaimScenarioAsync(context);
        var threadId = await ChatTestHelpers.GetThreadIdAsync(context.Client, scenario.ClaimId);

        var outsider = await ClaimTestHelpers.CreateAndLoginClaimantAsync(context);
        ClaimTestHelpers.Authenticate(context.Client, outsider.AccessToken);

        var (response, _) = await ChatTestHelpers.SendMessageAsync(
            context.Client,
            threadId,
            "Hello from outsider");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task List_includes_read_only_threads_after_claim_cancel()
    {
        await using var context = await ReportTestContext.CreateAsync(factory);
        var scenario = await ResolutionTestHelpers.CreateApprovedClaimScenarioAsync(context);
        var threadId = await ChatTestHelpers.GetThreadIdAsync(context.Client, scenario.ClaimId);

        ClaimTestHelpers.AuthenticateReporter(context.Client, context);
        var cancelResponse = await ResolutionTestHelpers.CancelClaimAsync(context.Client, scenario.ClaimId);
        Assert.Equal(HttpStatusCode.NoContent, cancelResponse.StatusCode);

        var listResponse = await ChatTestHelpers.ListChatsAsync(context.Client);
        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);

        var list = await listResponse.Content.ReadFromJsonAsync<ChatThreadListResponse>();
        Assert.NotNull(list);
        Assert.Contains(list.Items, item => item.Id == threadId && item.ReadOnlyAt is not null);
    }

    [Fact]
    public async Task List_includes_read_only_threads_after_resolution()
    {
        await using var context = await ReportTestContext.CreateAsync(factory);
        var scenario = await ResolutionTestHelpers.CreateApprovedClaimScenarioAsync(context);
        var threadId = await ChatTestHelpers.GetThreadIdAsync(context.Client, scenario.ClaimId);

        ClaimTestHelpers.AuthenticateReporter(context.Client, context);
        var reporterConfirm = await ResolutionTestHelpers.ConfirmResolutionAsync(context.Client, scenario.ClaimId);
        Assert.Equal(HttpStatusCode.NoContent, reporterConfirm.StatusCode);

        ClaimTestHelpers.Authenticate(context.Client, scenario.ClaimantSession.AccessToken);
        var claimantConfirm = await ResolutionTestHelpers.ConfirmResolutionAsync(context.Client, scenario.ClaimId);
        Assert.Equal(HttpStatusCode.NoContent, claimantConfirm.StatusCode);

        var listResponse = await ChatTestHelpers.ListChatsAsync(context.Client);
        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);

        var list = await listResponse.Content.ReadFromJsonAsync<ChatThreadListResponse>();
        Assert.NotNull(list);
        Assert.Contains(list.Items, item => item.Id == threadId && item.ReadOnlyAt is not null);
    }

    [Fact]
    public async Task Participant_can_open_read_only_thread_after_cancel()
    {
        await using var context = await ReportTestContext.CreateAsync(factory);
        var scenario = await ResolutionTestHelpers.CreateApprovedClaimScenarioAsync(context);
        var threadId = await ChatTestHelpers.GetThreadIdAsync(context.Client, scenario.ClaimId);

        ClaimTestHelpers.AuthenticateReporter(context.Client, context);
        var cancelResponse = await ResolutionTestHelpers.CancelClaimAsync(context.Client, scenario.ClaimId);
        Assert.Equal(HttpStatusCode.NoContent, cancelResponse.StatusCode);

        var threadResponse = await ChatTestHelpers.GetThreadAsync(context.Client, threadId);
        Assert.Equal(HttpStatusCode.OK, threadResponse.StatusCode);

        var thread = await threadResponse.Content.ReadFromJsonAsync<ChatThreadDetailResponse>();
        Assert.NotNull(thread);
        Assert.NotNull(thread.ReadOnlyAt);
        Assert.Equal("cancelled", thread.ClaimStatus);
    }

    [Fact]
    public async Task List_shows_counterparty_display_name_and_report_title()
    {
        await using var context = await ReportTestContext.CreateAsync(factory);
        var scenario = await ResolutionTestHelpers.CreateApprovedClaimScenarioAsync(context);

        ClaimTestHelpers.Authenticate(context.Client, scenario.ClaimantSession.AccessToken);
        var listResponse = await ChatTestHelpers.ListChatsAsync(context.Client);
        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);

        var list = await listResponse.Content.ReadFromJsonAsync<ChatThreadListResponse>();
        Assert.NotNull(list);
        Assert.Single(list.Items);

        var summary = list.Items[0];
        Assert.Equal(scenario.ReportId, summary.ReportId);
        Assert.False(string.IsNullOrWhiteSpace(summary.ReportTitle));
        Assert.Equal(context.Session.User.DisplayName, summary.CounterpartyDisplayName);
    }
}
