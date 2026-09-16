using Amanah.Api.Tests.Claims;
using Amanah.Api.Tests.Infrastructure;
using Amanah.Api.Tests.Reports;
using Amanah.Api.Tests.Resolution;
using Amanah.Contracts.Chats;
using Amanah.Contracts.Errors;
using Amanah.Contracts.Responses.Chats;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.EntityFrameworkCore;

namespace Amanah.Api.Tests.Chats;

public class ChatHubTests(ApiWebApplicationFactory factory) : IClassFixture<ApiWebApplicationFactory>
{
    [Fact]
    public async Task Hub_send_broadcasts_MessageReceived_to_thread_members()
    {
        await using var context = await ReportTestContext.CreateAsync(factory);
        var scenario = await ResolutionTestHelpers.CreateApprovedClaimScenarioAsync(context);
        var threadId = await ChatTestHelpers.GetThreadIdAsync(context.Client, scenario.ClaimId);

        var reporterConnection = await ChatTestHelpers.ConnectHubAsync(factory, context.Session.AccessToken);
        var claimantConnection = await ChatTestHelpers.ConnectHubAsync(factory, scenario.ClaimantSession.AccessToken);

        try
        {
            var received = new TaskCompletionSource<ChatMessageResponse>(TaskCreationOptions.RunContinuationsAsynchronously);
            claimantConnection.On<ChatMessageResponse>(ChatHubEvents.MessageReceived, message =>
            {
                received.TrySetResult(message);
            });

            await reporterConnection.InvokeAsync(ChatHubMethods.JoinThread, threadId.ToString());
            await claimantConnection.InvokeAsync(ChatHubMethods.JoinThread, threadId.ToString());

            var sent = await reporterConnection.InvokeAsync<ChatMessageResponse>(
                ChatHubMethods.SendMessage,
                threadId.ToString(),
                "Hello via hub",
                null);

            Assert.Equal(threadId, sent.ThreadId);
            Assert.Equal(context.Session.User.Id, sent.SenderId);
            Assert.Equal("Hello via hub", sent.Body);

            var message = await received.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Equal(sent.Id, message.Id);
            Assert.Equal(threadId, message.ThreadId);
            Assert.Equal(context.Session.User.Id, message.SenderId);
            Assert.Equal("Hello via hub", message.Body);
        }
        finally
        {
            await reporterConnection.DisposeAsync();
            await claimantConnection.DisposeAsync();
        }
    }

    [Fact]
    public async Task NewChatMessage_suppressed_when_recipient_has_joined_thread()
    {
        await using var context = await ReportTestContext.CreateAsync(factory);
        var scenario = await ResolutionTestHelpers.CreateApprovedClaimScenarioAsync(context);
        var threadId = await ChatTestHelpers.GetThreadIdAsync(context.Client, scenario.ClaimId);

        var reporterConnection = await ChatTestHelpers.ConnectHubAsync(factory, context.Session.AccessToken);
        var claimantConnection = await ChatTestHelpers.ConnectHubAsync(factory, scenario.ClaimantSession.AccessToken);

        try
        {
            await reporterConnection.InvokeAsync(ChatHubMethods.JoinThread, threadId.ToString());
            await claimantConnection.InvokeAsync(ChatHubMethods.JoinThread, threadId.ToString());

            await reporterConnection.InvokeAsync(
                ChatHubMethods.SendMessage,
                threadId.ToString(),
                "No notification please",
                null);

            var notificationCount = await context.DbContext.Notifications
                .AsNoTracking()
                .CountAsync(item =>
                    item.UserId == scenario.ClaimantSession.User.Id
                    && item.Type == "NewChatMessage");

            Assert.Equal(0, notificationCount);
        }
        finally
        {
            await reporterConnection.DisposeAsync();
            await claimantConnection.DisposeAsync();
        }
    }

    [Fact]
    public async Task JoinThread_rejects_non_participant()
    {
        await using var context = await ReportTestContext.CreateAsync(factory);
        var scenario = await ResolutionTestHelpers.CreateApprovedClaimScenarioAsync(context);
        var threadId = await ChatTestHelpers.GetThreadIdAsync(context.Client, scenario.ClaimId);

        var outsider = await ClaimTestHelpers.CreateAndLoginClaimantAsync(context);
        var connection = await ChatTestHelpers.ConnectHubAsync(factory, outsider.AccessToken);

        try
        {
            var exception = await Assert.ThrowsAsync<Microsoft.AspNetCore.SignalR.HubException>(() =>
                connection.InvokeAsync(ChatHubMethods.JoinThread, threadId.ToString()));

            Assert.Contains(ErrorCodes.NotFound, exception.Message);
        }
        finally
        {
            await connection.DisposeAsync();
        }
    }

    [Fact]
    public async Task Claim_cancel_broadcasts_ThreadReadOnly_to_joined_members()
    {
        await using var context = await ReportTestContext.CreateAsync(factory);
        var scenario = await ResolutionTestHelpers.CreateApprovedClaimScenarioAsync(context);
        var threadId = await ChatTestHelpers.GetThreadIdAsync(context.Client, scenario.ClaimId);

        var reporterConnection = await ChatTestHelpers.ConnectHubAsync(factory, context.Session.AccessToken);
        var claimantConnection = await ChatTestHelpers.ConnectHubAsync(factory, scenario.ClaimantSession.AccessToken);

        try
        {
            var received = new TaskCompletionSource<ChatThreadReadOnlyResponse>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            claimantConnection.On<ChatThreadReadOnlyResponse>(ChatHubEvents.ThreadReadOnly, payload =>
            {
                received.TrySetResult(payload);
            });

            await reporterConnection.InvokeAsync(ChatHubMethods.JoinThread, threadId.ToString());
            await claimantConnection.InvokeAsync(ChatHubMethods.JoinThread, threadId.ToString());

            ClaimTestHelpers.AuthenticateReporter(context.Client, context);
            var cancelResponse = await ResolutionTestHelpers.CancelClaimAsync(context.Client, scenario.ClaimId);
            Assert.Equal(System.Net.HttpStatusCode.NoContent, cancelResponse.StatusCode);

                        var payload = await received.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Equal(threadId, payload.ThreadId);
            Assert.NotEqual(default, payload.ReadOnlyAt);

            var thread = await context.DbContext.ChatThreads
                .AsNoTracking()
                .SingleAsync(item => item.Id == threadId);
            Assert.NotNull(thread.ReadOnlyAt);
            Assert.True(
                (thread.ReadOnlyAt.Value - payload.ReadOnlyAt).Duration() < TimeSpan.FromSeconds(1));
        }
        finally
        {
            await reporterConnection.DisposeAsync();
            await claimantConnection.DisposeAsync();
        }
    }
}
