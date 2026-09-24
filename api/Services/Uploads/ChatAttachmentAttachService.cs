using Amanah.Api.Auth;
using Amanah.Api.Data;
using Amanah.Api.Data.Entities;
using Amanah.Api.Models.Errors;
using Amanah.Api.Observability;
using Amanah.Api.Services.Storage;
using Amanah.Contracts.Errors;
using Amanah.Contracts.Responses.Uploads;
using Microsoft.EntityFrameworkCore;

namespace Amanah.Api.Services.Uploads;

public sealed class ChatAttachmentAttachService(
    AppDbContext dbContext,
    IBucketStorage bucketStorage,
    ReportImageProcessor imageProcessor,
    AppMetrics metrics,
    TimeProvider timeProvider)
{
    public async Task<Result<ChatAttachmentUploadResponse>> UploadAsync(
        Guid threadId,
        Guid userId,
        UserRole role,
        IFormFile photo,
        CancellationToken cancellationToken = default)
    {
        if (AdminParticipation.ForbidIfAdmin(role) is { } forbidden)
        {
            return forbidden;
        }

        var thread = await dbContext.ChatThreads
            .AsNoTracking()
            .Include(existingThread => existingThread.Claim)
            .ThenInclude(claim => claim.Report)
            .SingleOrDefaultAsync(existingThread => existingThread.Id == threadId, cancellationToken);

        if (thread is null
            || (thread.Claim.Report.ReporterId != userId && thread.Claim.ClaimantId != userId))
        {
            return ResultError.NotFound("Chat thread not found.");
        }

        if (thread.ReadOnlyAt is not null)
        {
            return ResultError.Conflict("This chat is read-only.");
        }

        ProcessedReportImage processed;
        try
        {
            await using var stream = photo.OpenReadStream();
            processed = imageProcessor.Process(stream, photo.Length, photo.ContentType);
        }
        catch (ReportImageProcessingException ex)
        {
            metrics.RecordUploadFailed();
            return ResultError.BadRequest(
                ex.Message,
                ex.Code,
                errors: new Dictionary<string, string[]>
                {
                    ["photo"] = [ex.Message],
                });
        }

        var uploadId = Guid.NewGuid();
        var originalKey = ChatAttachmentStorageKeys.Original(threadId, uploadId);
        var thumbnailKey = ChatAttachmentStorageKeys.Thumbnail(threadId, uploadId);
        var promotedKeys = new List<string>();

        try
        {
            await bucketStorage.PutAsync(
                originalKey,
                processed.OriginalStream,
                processed.ContentType,
                cancellationToken);
            promotedKeys.Add(originalKey);

            await bucketStorage.PutAsync(
                thumbnailKey,
                processed.ThumbnailStream,
                "image/webp",
                cancellationToken);
            promotedKeys.Add(thumbnailKey);
        }
        catch (Exception)
        {
            await bucketStorage.DeleteManyAsync(promotedKeys, cancellationToken);
            metrics.RecordUploadFailed();
            return ResultError.ServiceUnavailable(
                "Photo upload is temporarily unavailable. Please try again later.",
                ErrorCodes.UploadStorageFailed);
        }
        finally
        {
            await processed.OriginalStream.DisposeAsync();
            await processed.ThumbnailStream.DisposeAsync();
        }

        var attachment = new ChatAttachment
        {
            Id = uploadId,
            ChatThreadId = threadId,
            UploaderId = userId,
            StorageKey = originalKey,
            ThumbnailStorageKey = thumbnailKey,
            ContentType = processed.ContentType,
            SizeBytes = processed.SizeBytes,
            CreatedAt = timeProvider.GetUtcNow(),
        };

        dbContext.ChatAttachments.Add(attachment);
        await dbContext.SaveChangesAsync(cancellationToken);

        metrics.RecordUploadCompleted();
        return new ChatAttachmentUploadResponse { Id = attachment.Id };
    }

}
