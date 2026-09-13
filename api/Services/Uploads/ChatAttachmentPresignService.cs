using Amanah.Api.Data;
using Amanah.Api.Models.Errors;
using Amanah.Api.Services.Storage;
using Amanah.Contracts.Responses.Uploads;
using Microsoft.EntityFrameworkCore;

namespace Amanah.Api.Services.Uploads;

public sealed class ChatAttachmentPresignService(
    AppDbContext dbContext,
    IBucketStorage bucketStorage)
{
    private static readonly TimeSpan PresignLifetime = TimeSpan.FromMinutes(5);

    public async Task<Result<ChatAttachmentPresignResponse>> GetAttachmentUrlAsync(
        Guid attachmentId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var attachment = await dbContext.ChatAttachments
            .AsNoTracking()
            .Include(existingAttachment => existingAttachment.ChatThread)
            .ThenInclude(thread => thread.Claim)
            .ThenInclude(claim => claim.Report)
            .SingleOrDefaultAsync(existingAttachment => existingAttachment.Id == attachmentId, cancellationToken);

        if (attachment is null || !IsParticipant(attachment, userId))
        {
            return ResultError.NotFound("Attachment not found.");
        }

        var thumbnailKey = attachment.ThumbnailStorageKey;
        var storageKey = thumbnailKey is not null
            && await bucketStorage.ExistsAsync(thumbnailKey, cancellationToken)
            ? thumbnailKey
            : attachment.StorageKey;

        var url = bucketStorage.GetPreSignedUrl(storageKey, PresignLifetime);

        return new ChatAttachmentPresignResponse
        {
            Url = url.ToString(),
        };
    }

    private static bool IsParticipant(Data.Entities.ChatAttachment attachment, Guid userId)
    {
        var thread = attachment.ChatThread;
        return thread.Claim.Report.ReporterId == userId || thread.Claim.ClaimantId == userId;
    }
}
