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

        if (attachment is null
            || (attachment.ChatThread.Claim.Report.ReporterId != userId
                && attachment.ChatThread.Claim.ClaimantId != userId))
        {
            return ResultError.NotFound("Attachment not found.");
        }

        var storageKey = attachment.ThumbnailStorageKey is not null
            && await bucketStorage.ExistsAsync(attachment.ThumbnailStorageKey, cancellationToken)
            ? attachment.ThumbnailStorageKey
            : attachment.StorageKey;

        var url = bucketStorage.GetPreSignedUrl(storageKey, TimeSpan.FromMinutes(5));

        return new ChatAttachmentPresignResponse
        {
            Url = url.ToString(),
        };
    }

}
