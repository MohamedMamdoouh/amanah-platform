using Amanah.Api.Auth;
using Amanah.Api.Data;
using Amanah.Api.Data.Entities;
using Amanah.Api.Hubs;
using Amanah.Api.Models.Errors;
using Amanah.Api.Services.Notifications;
using Amanah.Api.Utilities.Notifications;
using Amanah.Contracts.Chats;
using Amanah.Contracts.Errors;
using Amanah.Contracts.Requests.Chats;
using Amanah.Contracts.Responses.Chats;
using Amanah.Contracts.Responses.Claims;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace Amanah.Api.Services.Chats;

public sealed class ChatService(
    AppDbContext dbContext,
    TimeProvider timeProvider,
    IHubContext<ChatHub> hubContext,
    ChatPresenceTracker presenceTracker)
{
    private const int DefaultMessageLimit = 50;
    private const int MaxMessageLimit = 100;
    private const int PreviewLength = 100;
    private const int MaxBodyLength = 2000;

    public async Task<Result<ChatThreadListResponse>> ListThreadsAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var threads = await dbContext.ChatThreads
            .AsNoTracking()
            .Include(thread => thread.Claim)
            .ThenInclude(claim => claim.Report)
            .Include(thread => thread.Claim)
            .ThenInclude(claim => claim.Claimant)
            .Include(thread => thread.Claim)
            .ThenInclude(claim => claim.Report)
            .ThenInclude(report => report.Reporter)
            .Where(thread =>
                thread.Claim.Report.ReporterId == userId
                || thread.Claim.ClaimantId == userId)
            .ToListAsync(cancellationToken);

        var threadIds = threads.Select(thread => thread.Id).ToList();
        var lastMessages = await LoadLastMessagesByThreadAsync(threadIds, cancellationToken);

        var items = threads
            .Select(thread =>
            {
                lastMessages.TryGetValue(thread.Id, out var lastMessage);
                return ToThreadSummary(thread, userId, lastMessage);
            })
            .OrderByDescending(summary => summary.LastMessageAt ?? summary.CreatedAt)
            .ToList();

        return new ChatThreadListResponse { Items = items };
    }

    public async Task<Result<ChatThreadDetailResponse>> GetThreadAsync(
        Guid threadId,
        Guid userId,
        UserRole role,
        Guid? beforeMessageId,
        int? limit,
        CancellationToken cancellationToken = default)
    {
        var thread = await LoadThreadAsync(threadId, cancellationToken);
        if (thread is null)
        {
            return ResultError.NotFound("Chat thread not found.");
        }

        var isParticipant = thread.Claim.Report.ReporterId == userId
            || thread.Claim.ClaimantId == userId;

        if (!isParticipant)
        {
            if (role != UserRole.Admin)
            {
                return ResultError.NotFound("Chat thread not found.");
            }

            var investigationOpen = await dbContext.AbuseReports
                .AsNoTracking()
                .AnyAsync(
                    abuseReport => abuseReport.ReportId == thread.Claim.ReportId
                        && abuseReport.Status == AbuseReportStatus.Open,
                    cancellationToken);

            if (!investigationOpen)
            {
                return ResultError.NotFound("Chat thread not found.");
            }
        }

        var messageLimit = NormalizeMessageLimit(limit);
        var messages = await LoadMessagesAsync(threadId, beforeMessageId, messageLimit, cancellationToken);
        var viewerUserId = isParticipant ? userId : thread.Claim.Report.ReporterId;

        return ToThreadDetail(thread, viewerUserId, messages);
    }

    public async Task<IReadOnlyList<ChatThreadDetailResponse>> GetInvestigationThreadsForReportAsync(
        Guid reportId,
        CancellationToken cancellationToken = default)
    {
        var threads = await dbContext.ChatThreads
            .AsNoTracking()
            .Include(thread => thread.Claim)
            .ThenInclude(claim => claim.Report)
            .ThenInclude(report => report.Resolution)
            .Include(thread => thread.Claim)
            .ThenInclude(claim => claim.Report)
            .ThenInclude(report => report.Reporter)
            .Include(thread => thread.Claim)
            .ThenInclude(claim => claim.Claimant)
            .Where(thread => thread.Claim.ReportId == reportId)
            .OrderBy(thread => thread.CreatedAt)
            .ToListAsync(cancellationToken);

        var details = new List<ChatThreadDetailResponse>(threads.Count);
        foreach (var thread in threads)
        {
            var messages = await LoadMessagesAsync(
                thread.Id,
                beforeMessageId: null,
                DefaultMessageLimit,
                cancellationToken);
            details.Add(ToThreadDetail(thread, thread.Claim.Report.ReporterId, messages));
        }

        return details;
    }

    public Task<bool> IsParticipantAsync(
        Guid threadId,
        Guid userId,
        CancellationToken cancellationToken = default) =>
        dbContext.ChatThreads
            .AsNoTracking()
            .AnyAsync(
                thread => thread.Id == threadId
                    && (thread.Claim.Report.ReporterId == userId
                        || thread.Claim.ClaimantId == userId),
                cancellationToken);

    public async Task<Result<ChatMessageResponse>> SendMessageAsync(
        Guid threadId,
        Guid userId,
        UserRole role,
        SendMessageRequest request,
        CancellationToken cancellationToken = default)
    {
        if (AdminParticipation.ForbidIfAdmin(role) is { } forbidden)
        {
            return forbidden;
        }

        ChatAttachment? attachment = null;
        if (request.AttachmentId is Guid attachmentId)
        {
            attachment = await dbContext.ChatAttachments
                .SingleOrDefaultAsync(
                    item => item.Id == attachmentId
                        && item.ChatThreadId == threadId
                        && item.UploaderId == userId
                        && item.MessageId == null,
                    cancellationToken);

            if (attachment is null)
            {
                return ResultError.NotFound("Attachment not found.");
            }
        }

        var normalizedBody = NormalizeBody(request.Body);
        if (string.IsNullOrEmpty(normalizedBody) && attachment is null)
        {
            return ResultError.BadRequest(
                "Message cannot be empty.",
                errors: new Dictionary<string, string[]>
                {
                    ["body"] = ["Message cannot be empty."],
                });
        }

        if (normalizedBody.Length > MaxBodyLength)
        {
            return ResultError.BadRequest(
                "Message is too long.",
                errors: new Dictionary<string, string[]>
                {
                    ["body"] = ["Message is too long."],
                });
        }

        var thread = await LoadThreadForWriteAsync(threadId, cancellationToken);
        if (thread is null
            || (thread.Claim.Report.ReporterId != userId && thread.Claim.ClaimantId != userId))
        {
            return ResultError.NotFound("Chat thread not found.");
        }

        if (thread.ReadOnlyAt is not null)
        {
            return ResultError.Conflict(
                "This chat is read-only.",
                ErrorCodes.ChatReadOnly);
        }

        var sender = thread.Claim.Report.ReporterId == userId
            ? thread.Claim.Report.Reporter
            : thread.Claim.Claimant;

        var now = timeProvider.GetUtcNow();
        var message = new Message
        {
            Id = Guid.NewGuid(),
            ChatThreadId = thread.Id,
            SenderId = userId,
            Body = normalizedBody,
            AttachmentStorageKey = attachment?.StorageKey,
            SentAt = now,
        };

        dbContext.Messages.Add(message);

        if (attachment is not null)
        {
            attachment.MessageId = message.Id;
        }

        var counterpartyId = thread.Claim.Report.ReporterId == userId
            ? thread.Claim.ClaimantId
            : thread.Claim.Report.ReporterId;

        if (!presenceTracker.IsViewing(counterpartyId, thread.Id))
        {
            dbContext.Notifications.Add(new Notification
            {
                Id = Guid.NewGuid(),
                UserId = counterpartyId,
                Type = NotificationTypes.NewChatMessage,
                PayloadJson = new NotificationPayload(
                    NotificationTypes.NewChatMessage,
                    now,
                    DeepLink: $"/my/chats/{thread.Id}",
                    ReportId: thread.Claim.ReportId,
                    ClaimId: thread.ClaimId,
                    ChatThreadId: thread.Id).ToJson(),
                IsRead = false,
                CreatedAt = now,
            });
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        var response = ToMessageResponse(
            message,
            sender.DisplayName ?? string.Empty,
            attachment?.Id);

        await hubContext.Clients
            .Group(ChatHubGroups.ForThread(thread.Id))
            .SendAsync(ChatHubEvents.MessageReceived, response, cancellationToken);

        return response;
    }

    private async Task<ChatThread?> LoadThreadAsync(
        Guid threadId,
        CancellationToken cancellationToken) =>
        await dbContext.ChatThreads
            .AsNoTracking()
            .Include(thread => thread.Claim)
            .ThenInclude(claim => claim.Report)
            .ThenInclude(report => report.Resolution)
            .Include(thread => thread.Claim)
            .ThenInclude(claim => claim.Report)
            .ThenInclude(report => report.Reporter)
            .Include(thread => thread.Claim)
            .ThenInclude(claim => claim.Claimant)
            .SingleOrDefaultAsync(thread => thread.Id == threadId, cancellationToken);

    private async Task<ChatThread?> LoadThreadForWriteAsync(
        Guid threadId,
        CancellationToken cancellationToken) =>
        await dbContext.ChatThreads
            .Include(thread => thread.Claim)
            .ThenInclude(claim => claim.Report)
            .ThenInclude(report => report.Reporter)
            .Include(thread => thread.Claim)
            .ThenInclude(claim => claim.Claimant)
            .SingleOrDefaultAsync(thread => thread.Id == threadId, cancellationToken);

    private async Task<IReadOnlyList<Message>> LoadMessagesAsync(
        Guid threadId,
        Guid? beforeMessageId,
        int limit,
        CancellationToken cancellationToken)
    {
        var messagesQuery = dbContext.Messages
            .AsNoTracking()
            .Include(message => message.Sender)
            .Include(message => message.Attachment)
            .Where(message => message.ChatThreadId == threadId);

        if (beforeMessageId is Guid cursorMessageId)
        {
            var cursorSentAt = await dbContext.Messages
                .AsNoTracking()
                .Where(message => message.Id == cursorMessageId && message.ChatThreadId == threadId)
                .Select(message => (DateTimeOffset?)message.SentAt)
                .SingleOrDefaultAsync(cancellationToken);

            if (cursorSentAt is null)
            {
                return [];
            }

            messagesQuery = messagesQuery.Where(message => message.SentAt < cursorSentAt);
        }

        var messages = await messagesQuery
            .OrderByDescending(message => message.SentAt)
            .Take(limit)
            .ToListAsync(cancellationToken);

        messages.Reverse();
        return messages;
    }

    private async Task<IReadOnlyDictionary<Guid, Message>> LoadLastMessagesByThreadAsync(
        IReadOnlyCollection<Guid> threadIds,
        CancellationToken cancellationToken)
    {
        if (threadIds.Count == 0)
        {
            return new Dictionary<Guid, Message>();
        }

        return await dbContext.Messages
            .AsNoTracking()
            .Where(message => threadIds.Contains(message.ChatThreadId))
            .GroupBy(message => message.ChatThreadId)
            .Select(group => group
                .OrderByDescending(message => message.SentAt)
                .First())
            .ToDictionaryAsync(message => message.ChatThreadId, cancellationToken);
    }

    private static int NormalizeMessageLimit(int? limit)
    {
        if (limit is null or < 1)
        {
            return DefaultMessageLimit;
        }

        return Math.Min(limit.Value, MaxMessageLimit);
    }

    private static string NormalizeBody(string? body) =>
        string.IsNullOrWhiteSpace(body) ? string.Empty : body.Trim();

    private static ChatThreadSummaryResponse ToThreadSummary(
        ChatThread thread,
        Guid userId,
        Message? lastMessage)
    {
        var isReporter = thread.Claim.Report.ReporterId == userId;
        var counterparty = isReporter ? thread.Claim.Claimant : thread.Claim.Report.Reporter;

        return new ChatThreadSummaryResponse
        {
            Id = thread.Id,
            ClaimId = thread.ClaimId,
            ReportId = thread.Claim.ReportId,
            ReportTitle = thread.Claim.Report.Title,
            ReportType = ToReportType(thread.Claim.Report.Type),
            CounterpartyDisplayName = counterparty.DisplayName ?? string.Empty,
            CreatedAt = thread.CreatedAt,
            ReadOnlyAt = thread.ReadOnlyAt,
            LastMessageAt = lastMessage?.SentAt,
            LastMessagePreview = lastMessage is null
                ? null
                : TruncatePreview(lastMessage.Body),
        };
    }

    private static ChatThreadDetailResponse ToThreadDetail(
        ChatThread thread,
        Guid userId,
        IReadOnlyList<Message> messages)
    {
        var claim = thread.Claim;
        var isReporter = claim.Report.ReporterId == userId;
        var counterparty = isReporter ? claim.Claimant : claim.Report.Reporter;
        ResolutionStateResponse? resolution = null;

        if (claim.Status is ClaimStatus.Approved or ClaimStatus.Cancelled)
        {
            var reportResolution = claim.Report.Resolution;
            var reporterConfirmed = reportResolution?.ReporterConfirmedAt is not null;
            var claimantConfirmed = reportResolution?.ClaimantConfirmedAt is not null;
            var currentUserHasConfirmed = isReporter ? reporterConfirmed : claimantConfirmed;

            resolution = new ResolutionStateResponse
            {
                ReporterConfirmedAt = reportResolution?.ReporterConfirmedAt,
                ClaimantConfirmedAt = reportResolution?.ClaimantConfirmedAt,
                ResolvedAt = reportResolution?.ResolvedAt,
                CurrentUserHasConfirmed = currentUserHasConfirmed,
                CurrentUserCanCancel = claim.Status == ClaimStatus.Approved
                    && !currentUserHasConfirmed,
            };
        }

        return new ChatThreadDetailResponse
        {
            Id = thread.Id,
            ClaimId = thread.ClaimId,
            ReportId = claim.ReportId,
            ReportTitle = claim.Report.Title,
            ReportType = ToReportType(claim.Report.Type),
            ReportStatus = ToReportStatus(claim.Report.Status),
            ClaimStatus = ToClaimStatus(claim.Status),
            CounterpartyDisplayName = counterparty.DisplayName ?? string.Empty,
            CreatedAt = thread.CreatedAt,
            ReadOnlyAt = thread.ReadOnlyAt,
            Resolution = resolution,
            Messages = messages
                .Select(message => ToMessageResponse(
                    message,
                    message.Sender.DisplayName ?? string.Empty,
                    message.Attachment?.Id))
                .ToList(),
        };
    }

    private static ChatMessageResponse ToMessageResponse(
        Message message,
        string senderDisplayName,
        Guid? attachmentId = null) =>
        new()
        {
            Id = message.Id,
            ThreadId = message.ChatThreadId,
            SenderId = message.SenderId,
            SenderDisplayName = senderDisplayName,
            Body = message.Body,
            AttachmentId = attachmentId ?? message.Attachment?.Id,
            SentAt = message.SentAt,
        };

    private static string TruncatePreview(string body) =>
        body.Length <= PreviewLength
            ? body
            : body[..PreviewLength];

    private static string ToReportType(ReportType type) => type switch
    {
        ReportType.Lost => "lost",
        ReportType.Found => "found",
        _ => type.ToString().ToLowerInvariant(),
    };

    private static string ToReportStatus(ReportStatus status) => status switch
    {
        ReportStatus.PendingReview => "pending_review",
        ReportStatus.Rejected => "rejected",
        ReportStatus.Published => "published",
        ReportStatus.ClaimInProgress => "claim_in_progress",
        ReportStatus.Resolved => "resolved",
        ReportStatus.Withdrawn => "withdrawn",
        ReportStatus.RemovedByAdmin => "removed_by_admin",
        _ => status.ToString().ToLowerInvariant(),
    };

    private static string ToClaimStatus(ClaimStatus status) => status switch
    {
        ClaimStatus.Pending => "pending",
        ClaimStatus.Approved => "approved",
        ClaimStatus.Rejected => "rejected",
        ClaimStatus.Withdrawn => "withdrawn",
        ClaimStatus.Cancelled => "cancelled",
        _ => status.ToString().ToLowerInvariant(),
    };
}
