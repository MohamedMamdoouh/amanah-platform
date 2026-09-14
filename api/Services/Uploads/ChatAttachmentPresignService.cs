using Amanah.Api.Data;
using Amanah.Api.Models.Errors;
using Amanah.Api.Services.Storage;
using Amanah.Api.Utilities.Chats;
using Amanah.Api.Utilities.Uploads;
using Amanah.Contracts.Responses.Uploads;
using Microsoft.EntityFrameworkCore;

namespace Amanah.Api.Services.Uploads;

public sealed class ChatAttachmentPresignService(
    AppDbContext dbContext,
    IBucketStorage bucketStorage)
{
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

        if (attachment is null || !ChatParticipantAuthorization.IsParticipant(attachment, userId))
        {
            return ResultError.NotFound("Attachment not found.");
        }

        var storageKey = await StorageKeyResolver.ResolvePreferExistingThumbnailAsync(
            bucketStorage,
            attachment.StorageKey,
            attachment.ThumbnailStorageKey,
            cancellationToken);

        var url = bucketStorage.GetPreSignedUrl(storageKey, PresignConstants.Lifetime);

        return new ChatAttachmentPresignResponse
        {
            Url = url.ToString(),
        };
    }

}
