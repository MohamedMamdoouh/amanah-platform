using System.Net;
using System.Net.Http.Json;
using Amanah.Api.Services.Storage;
using Amanah.Api.Tests.Chats;
using Amanah.Api.Tests.Claims;
using Amanah.Api.Tests.Infrastructure;
using Amanah.Api.Tests.Reports;
using Amanah.Api.Tests.Resolution;
using Amanah.Api.Tests.Uploads;
using Amanah.Contracts.Requests.Chats;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Amanah.Api.Tests.Lifecycle;

public sealed class ChatRetentionWebApplicationFactory : ApiWebApplicationFactory
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.UseSetting("Lifecycle:RetentionDays", "30");
    }
}

public class ChatRetentionTests(ChatRetentionWebApplicationFactory factory)
    : IClassFixture<ChatRetentionWebApplicationFactory>
{
    [Fact]
    public async Task ChatRetention_deletes_expired_read_only_thread_messages_and_attachments()
    {
        await using var context = await ReportTestContext.CreateAsync(factory);
        var scenario = await ResolutionTestHelpers.CreateApprovedClaimScenarioAsync(context);
        var threadId = await ChatTestHelpers.GetThreadIdAsync(context.Client, scenario.ClaimId);

        ClaimTestHelpers.AuthenticateReporter(context.Client, context);

        var upload = await ChatAttachmentTestHelpers.UploadAsync(
            context.Client,
            threadId,
            TestImageFactory.CreateMinimalJpeg());
        Assert.Equal(HttpStatusCode.Created, upload.Response.StatusCode);
        Assert.NotNull(upload.Body);

        var (sendResponse, _) = await ChatTestHelpers.SendMessageAsync(
            context.Client,
            threadId,
            new SendMessageRequest
            {
                Body = "Photo attached",
                AttachmentId = upload.Body.Id,
            });
        Assert.Equal(HttpStatusCode.Created, sendResponse.StatusCode);

        var attachment = await context.DbContext.ChatAttachments
            .AsNoTracking()
            .SingleAsync(item => item.Id == upload.Body.Id);

        var cancelResponse = await ResolutionTestHelpers.CancelClaimAsync(context.Client, scenario.ClaimId);
        Assert.Equal(HttpStatusCode.NoContent, cancelResponse.StatusCode);

        await BackdateReadOnlyAtAsync(context, threadId, daysAgo: 31);

        var storage = Assert.IsType<FakeBucketStorage>(
            factory.Services.GetRequiredService<IBucketStorage>());
        Assert.True(storage.ContainsKey(attachment.StorageKey));
        Assert.True(storage.ContainsKey(attachment.ThumbnailStorageKey!));

        var response = await RunJobAsync(context, "ChatRetention");
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        Assert.False(await context.DbContext.ChatThreads.AnyAsync(item => item.Id == threadId));
        Assert.Equal(0, await context.DbContext.Messages.CountAsync(message => message.ChatThreadId == threadId));
        Assert.Equal(0, await context.DbContext.ChatAttachments.CountAsync(item => item.ChatThreadId == threadId));
        Assert.True(storage.ContainsKey(attachment.StorageKey));
        await StorageDeletionOutboxTestHelpers.ProcessPendingOutboxAsync(factory);
        Assert.False(storage.ContainsKey(attachment.StorageKey));
        Assert.False(storage.ContainsKey(attachment.ThumbnailStorageKey!));

        var claim = await context.DbContext.Claims
            .AsNoTracking()
            .SingleAsync(item => item.Id == scenario.ClaimId);
        Assert.NotNull(claim);
    }

    [Fact]
    public async Task ChatRetention_leaves_active_chat_unchanged()
    {
        await using var context = await ReportTestContext.CreateAsync(factory);
        var scenario = await ResolutionTestHelpers.CreateApprovedClaimScenarioAsync(context);
        var threadId = await ChatTestHelpers.GetThreadIdAsync(context.Client, scenario.ClaimId);

        ClaimTestHelpers.AuthenticateReporter(context.Client, context);
        var (sendResponse, _) = await ChatTestHelpers.SendMessageAsync(
            context.Client,
            threadId,
            "Still active");
        Assert.Equal(HttpStatusCode.Created, sendResponse.StatusCode);

        var response = await RunJobAsync(context, "ChatRetention");
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        var thread = await context.DbContext.ChatThreads
            .AsNoTracking()
            .SingleAsync(item => item.Id == threadId);
        Assert.Null(thread.ReadOnlyAt);
        Assert.Equal(1, await context.DbContext.Messages.CountAsync(message => message.ChatThreadId == threadId));
    }

    [Fact]
    public async Task ChatRetention_leaves_fresh_read_only_chat_unchanged()
    {
        await using var context = await ReportTestContext.CreateAsync(factory);
        var scenario = await ResolutionTestHelpers.CreateApprovedClaimScenarioAsync(context);
        var threadId = await ChatTestHelpers.GetThreadIdAsync(context.Client, scenario.ClaimId);

        ClaimTestHelpers.AuthenticateReporter(context.Client, context);
        var cancelResponse = await ResolutionTestHelpers.CancelClaimAsync(context.Client, scenario.ClaimId);
        Assert.Equal(HttpStatusCode.NoContent, cancelResponse.StatusCode);

        var response = await RunJobAsync(context, "ChatRetention");
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        var thread = await context.DbContext.ChatThreads
            .AsNoTracking()
            .SingleAsync(item => item.Id == threadId);
        Assert.NotNull(thread.ReadOnlyAt);
        Assert.Equal(0, await context.DbContext.Messages.CountAsync(message => message.ChatThreadId == threadId));
    }

    private static async Task BackdateReadOnlyAtAsync(
        ReportTestContext context,
        Guid threadId,
        int daysAgo)
    {
        var thread = await context.DbContext.ChatThreads.SingleAsync(item => item.Id == threadId);
        thread.ReadOnlyAt = DateTimeOffset.UtcNow.AddDays(-daysAgo);
        await context.DbContext.SaveChangesAsync();
    }

    private static async Task<HttpResponseMessage> RunJobAsync(ReportTestContext context, string jobName)
    {
        await HttpTestHelpers.LoginAsAdminAsync(context);
        return await context.Client.PostAsync($"/api/v1/admin/test/run-job/{jobName}", null);
    }
}
