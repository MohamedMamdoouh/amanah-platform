using System.Net;
using System.Net.Http.Json;
using Amanah.Api.Tests.Chats;
using Amanah.Api.Tests.Claims;
using Amanah.Api.Tests.Infrastructure;
using Amanah.Api.Tests.Reports;
using Amanah.Api.Tests.Resolution;
using Amanah.Contracts.Requests.Chats;
using Amanah.Contracts.Responses.Uploads;
using Microsoft.EntityFrameworkCore;

namespace Amanah.Api.Tests.Uploads;

public class ChatAttachmentTests(ApiWebApplicationFactory factory) : IClassFixture<ApiWebApplicationFactory>
{
    [Fact]
    public async Task Upload_persists_staging_row_and_returns_id()
    {
        await using var context = await ReportTestContext.CreateAsync(factory);
        var scenario = await ResolutionTestHelpers.CreateApprovedClaimScenarioAsync(context);
        var threadId = await ChatTestHelpers.GetThreadIdAsync(context.Client, scenario.ClaimId);

        ClaimTestHelpers.AuthenticateReporter(context.Client, context);
        var (response, body) = await ChatAttachmentTestHelpers.UploadAsync(
            context.Client,
            threadId,
            TestImageFactory.CreateMinimalJpeg());

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(body);

        var stored = await context.DbContext.ChatAttachments
            .AsNoTracking()
            .SingleAsync(attachment => attachment.Id == body.Id);
        Assert.Equal(threadId, stored.ChatThreadId);
        Assert.Equal(context.Session.User.Id, stored.UploaderId);
        Assert.Null(stored.MessageId);
    }

    [Fact]
    public async Task Send_message_with_attachment_binds_storage_key()
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

        var (sendResponse, message) = await ChatTestHelpers.SendMessageAsync(
            context.Client,
            threadId,
            new SendMessageRequest
            {
                Body = "Here is the photo",
                AttachmentId = upload.Body.Id,
            });

        Assert.Equal(HttpStatusCode.Created, sendResponse.StatusCode);
        Assert.NotNull(message);
        Assert.Equal(upload.Body.Id, message.AttachmentId);

        var storedMessage = await context.DbContext.Messages
            .AsNoTracking()
            .SingleAsync(item => item.Id == message.Id);
        Assert.False(string.IsNullOrWhiteSpace(storedMessage.AttachmentStorageKey));

        var storedAttachment = await context.DbContext.ChatAttachments
            .AsNoTracking()
            .SingleAsync(item => item.Id == upload.Body.Id);
        Assert.Equal(message.Id, storedAttachment.MessageId);
    }

    [Fact]
    public async Task Attachment_only_message_is_allowed()
    {
        await using var context = await ReportTestContext.CreateAsync(factory);
        var scenario = await ResolutionTestHelpers.CreateApprovedClaimScenarioAsync(context);
        var threadId = await ChatTestHelpers.GetThreadIdAsync(context.Client, scenario.ClaimId);

        ClaimTestHelpers.AuthenticateReporter(context.Client, context);
        var upload = await ChatAttachmentTestHelpers.UploadAsync(
            context.Client,
            threadId,
            TestImageFactory.CreateMinimalJpeg());
        Assert.NotNull(upload.Body);

        var (sendResponse, message) = await ChatTestHelpers.SendMessageAsync(
            context.Client,
            threadId,
            new SendMessageRequest { AttachmentId = upload.Body.Id });

        Assert.Equal(HttpStatusCode.Created, sendResponse.StatusCode);
        Assert.NotNull(message);
        Assert.Equal(string.Empty, message.Body);
        Assert.Equal(upload.Body.Id, message.AttachmentId);
    }

    [Fact]
    public async Task Reused_attachment_id_is_rejected()
    {
        await using var context = await ReportTestContext.CreateAsync(factory);
        var scenario = await ResolutionTestHelpers.CreateApprovedClaimScenarioAsync(context);
        var threadId = await ChatTestHelpers.GetThreadIdAsync(context.Client, scenario.ClaimId);

        ClaimTestHelpers.AuthenticateReporter(context.Client, context);
        var upload = await ChatAttachmentTestHelpers.UploadAsync(
            context.Client,
            threadId,
            TestImageFactory.CreateMinimalJpeg());
        Assert.NotNull(upload.Body);

        var firstSend = await ChatTestHelpers.SendMessageAsync(
            context.Client,
            threadId,
            new SendMessageRequest { AttachmentId = upload.Body.Id });
        Assert.Equal(HttpStatusCode.Created, firstSend.Response.StatusCode);

        var secondSend = await ChatTestHelpers.SendMessageAsync(
            context.Client,
            threadId,
            new SendMessageRequest { AttachmentId = upload.Body.Id });

        Assert.Equal(HttpStatusCode.NotFound, secondSend.Response.StatusCode);
    }

    [Fact]
    public async Task Non_participant_cannot_upload_attachment()
    {
        await using var context = await ReportTestContext.CreateAsync(factory);
        var scenario = await ResolutionTestHelpers.CreateApprovedClaimScenarioAsync(context);
        var threadId = await ChatTestHelpers.GetThreadIdAsync(context.Client, scenario.ClaimId);

        var outsider = await ClaimTestHelpers.CreateAndLoginClaimantAsync(context);
        ClaimTestHelpers.Authenticate(context.Client, outsider.AccessToken);
        var (response, _) = await ChatAttachmentTestHelpers.UploadAsync(
            context.Client,
            threadId,
            TestImageFactory.CreateMinimalJpeg());

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Participant_can_presign_bound_attachment()
    {
        await using var context = await ReportTestContext.CreateAsync(factory);
        var scenario = await ResolutionTestHelpers.CreateApprovedClaimScenarioAsync(context);
        var threadId = await ChatTestHelpers.GetThreadIdAsync(context.Client, scenario.ClaimId);

        ClaimTestHelpers.AuthenticateReporter(context.Client, context);
        var upload = await ChatAttachmentTestHelpers.UploadAsync(
            context.Client,
            threadId,
            TestImageFactory.CreateMinimalJpeg());
        Assert.NotNull(upload.Body);

        var send = await ChatTestHelpers.SendMessageAsync(
            context.Client,
            threadId,
            new SendMessageRequest { AttachmentId = upload.Body.Id });
        Assert.Equal(HttpStatusCode.Created, send.Response.StatusCode);

        ClaimTestHelpers.Authenticate(context.Client, scenario.ClaimantSession.AccessToken);
        var presignResponse = await ChatAttachmentTestHelpers.GetPresignedUrlAsync(
            context.Client,
            upload.Body.Id);

        Assert.Equal(HttpStatusCode.OK, presignResponse.StatusCode);

        var presign = await presignResponse.Content.ReadFromJsonAsync<ChatAttachmentPresignResponse>();
        Assert.NotNull(presign);
        Assert.StartsWith("https://fake.local/", presign.Url, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Non_participant_cannot_presign_attachment()
    {
        await using var context = await ReportTestContext.CreateAsync(factory);
        var scenario = await ResolutionTestHelpers.CreateApprovedClaimScenarioAsync(context);
        var threadId = await ChatTestHelpers.GetThreadIdAsync(context.Client, scenario.ClaimId);

        ClaimTestHelpers.AuthenticateReporter(context.Client, context);
        var upload = await ChatAttachmentTestHelpers.UploadAsync(
            context.Client,
            threadId,
            TestImageFactory.CreateMinimalJpeg());
        Assert.NotNull(upload.Body);

        var outsider = await ClaimTestHelpers.CreateAndLoginClaimantAsync(context);
        ClaimTestHelpers.Authenticate(context.Client, outsider.AccessToken);
        var presignResponse = await ChatAttachmentTestHelpers.GetPresignedUrlAsync(
            context.Client,
            upload.Body.Id);

        Assert.Equal(HttpStatusCode.NotFound, presignResponse.StatusCode);
    }
}
