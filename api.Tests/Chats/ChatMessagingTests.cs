using System.Net;
using System.Net.Http.Json;
using Amanah.Api.Tests.Claims;
using Amanah.Api.Tests.Infrastructure;
using Amanah.Api.Tests.Reports;
using Amanah.Api.Tests.Resolution;
using Amanah.Contracts.Errors;
using Amanah.Contracts.Requests.Chats;
using Amanah.Contracts.Responses.Chats;
using Microsoft.EntityFrameworkCore;

namespace Amanah.Api.Tests.Chats;

public class ChatMessagingTests(ApiWebApplicationFactory factory) : IClassFixture<ApiWebApplicationFactory>
{
    [Fact]
    public async Task Send_message_persists_and_returns_created_message()
    {
        await using var context = await ReportTestContext.CreateAsync(factory);
        var scenario = await ResolutionTestHelpers.CreateApprovedClaimScenarioAsync(context);
        var threadId = await ChatTestHelpers.GetThreadIdAsync(context.Client, scenario.ClaimId);

        ClaimTestHelpers.AuthenticateReporter(context.Client, context);
        var (response, body) = await ChatTestHelpers.SendMessageAsync(
            context.Client,
            threadId,
            "Meet at the police station at 5pm.");

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(body);
        Assert.Equal(threadId, body.ThreadId);
        Assert.Equal(context.Session.User.Id, body.SenderId);
        Assert.Equal("Meet at the police station at 5pm.", body.Body);

        var stored = await context.DbContext.Messages
            .AsNoTracking()
            .SingleAsync(message => message.Id == body.Id);
        Assert.Equal("Meet at the police station at 5pm.", stored.Body);
    }

    [Fact]
    public async Task Thread_history_returns_messages_in_sent_at_order()
    {
        await using var context = await ReportTestContext.CreateAsync(factory);
        var scenario = await ResolutionTestHelpers.CreateApprovedClaimScenarioAsync(context);
        var threadId = await ChatTestHelpers.GetThreadIdAsync(context.Client, scenario.ClaimId);

        ClaimTestHelpers.AuthenticateReporter(context.Client, context);
        var first = await ChatTestHelpers.SendMessageAsync(context.Client, threadId, "First message");
        Assert.Equal(HttpStatusCode.Created, first.Response.StatusCode);

        ClaimTestHelpers.Authenticate(context.Client, scenario.ClaimantSession.AccessToken);
        var second = await ChatTestHelpers.SendMessageAsync(context.Client, threadId, "Second message");
        Assert.Equal(HttpStatusCode.Created, second.Response.StatusCode);

        var threadResponse = await ChatTestHelpers.GetThreadAsync(context.Client, threadId);
        Assert.Equal(HttpStatusCode.OK, threadResponse.StatusCode);

        var thread = await threadResponse.Content.ReadFromJsonAsync<ChatThreadDetailResponse>();
        Assert.NotNull(thread);
        Assert.Equal(2, thread.Messages.Count);
        Assert.Equal("First message", thread.Messages[0].Body);
        Assert.Equal("Second message", thread.Messages[1].Body);
        Assert.True(thread.Messages[0].SentAt <= thread.Messages[1].SentAt);
    }

    [Fact]
    public async Task Read_only_thread_rejects_send_with_conflict()
    {
        await using var context = await ReportTestContext.CreateAsync(factory);
        var scenario = await ResolutionTestHelpers.CreateApprovedClaimScenarioAsync(context);
        var threadId = await ChatTestHelpers.GetThreadIdAsync(context.Client, scenario.ClaimId);

        ClaimTestHelpers.AuthenticateReporter(context.Client, context);
        var cancelResponse = await ResolutionTestHelpers.CancelClaimAsync(context.Client, scenario.ClaimId);
        Assert.Equal(HttpStatusCode.NoContent, cancelResponse.StatusCode);

        var (response, _) = await ChatTestHelpers.SendMessageAsync(
            context.Client,
            threadId,
            "This should fail");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);

        var error = await HttpTestHelpers.ReadErrorAsync(response);
        Assert.NotNull(error);
        Assert.Equal(ErrorCodes.Conflict, error.Code);
    }

    [Fact]
    public async Task Empty_message_body_is_rejected()
    {
        await using var context = await ReportTestContext.CreateAsync(factory);
        var scenario = await ResolutionTestHelpers.CreateApprovedClaimScenarioAsync(context);
        var threadId = await ChatTestHelpers.GetThreadIdAsync(context.Client, scenario.ClaimId);

        ClaimTestHelpers.AuthenticateReporter(context.Client, context);
        var (response, _) = await ChatTestHelpers.SendMessageAsync(
            context.Client,
            threadId,
            new SendMessageRequest { Body = "   " });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var error = await HttpTestHelpers.ReadErrorAsync(response);
        Assert.NotNull(error);
        Assert.Equal(ErrorCodes.ValidationFailed, error.Code);
    }

    [Fact]
    public async Task Unknown_attachment_id_is_rejected()
    {
        await using var context = await ReportTestContext.CreateAsync(factory);
        var scenario = await ResolutionTestHelpers.CreateApprovedClaimScenarioAsync(context);
        var threadId = await ChatTestHelpers.GetThreadIdAsync(context.Client, scenario.ClaimId);

        ClaimTestHelpers.AuthenticateReporter(context.Client, context);
        var (response, _) = await ChatTestHelpers.SendMessageAsync(
            context.Client,
            threadId,
            new SendMessageRequest
            {
                Body = "Photo coming soon",
                AttachmentId = Guid.NewGuid(),
            });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Send_message_notifies_counterparty()
    {
        await using var context = await ReportTestContext.CreateAsync(factory);
        var scenario = await ResolutionTestHelpers.CreateApprovedClaimScenarioAsync(context);
        var threadId = await ChatTestHelpers.GetThreadIdAsync(context.Client, scenario.ClaimId);

        ClaimTestHelpers.AuthenticateReporter(context.Client, context);
        var (response, _) = await ChatTestHelpers.SendMessageAsync(
            context.Client,
            threadId,
            "Are you free tomorrow?");

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var notification = await context.DbContext.Notifications
            .AsNoTracking()
            .SingleAsync(item =>
                item.UserId == scenario.ClaimantSession.User.Id
                && item.Type == "NewChatMessage");
        Assert.Contains($"/my/chats/{threadId}", notification.PayloadJson, StringComparison.Ordinal);
    }

    [Fact]
    public async Task List_includes_last_message_preview_and_timestamp()
    {
        await using var context = await ReportTestContext.CreateAsync(factory);
        var scenario = await ResolutionTestHelpers.CreateApprovedClaimScenarioAsync(context);
        var threadId = await ChatTestHelpers.GetThreadIdAsync(context.Client, scenario.ClaimId);

        ClaimTestHelpers.AuthenticateReporter(context.Client, context);
        var send = await ChatTestHelpers.SendMessageAsync(context.Client, threadId, "Latest preview text");
        Assert.Equal(HttpStatusCode.Created, send.Response.StatusCode);

        var listResponse = await ChatTestHelpers.ListChatsAsync(context.Client);
        var list = await listResponse.Content.ReadFromJsonAsync<ChatThreadListResponse>();
        Assert.NotNull(list);

        var summary = Assert.Single(list.Items);
        Assert.Equal("Latest preview text", summary.LastMessagePreview);
        Assert.NotNull(summary.LastMessageAt);
    }
}
