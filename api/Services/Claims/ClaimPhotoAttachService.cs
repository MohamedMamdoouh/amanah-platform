using Amanah.Api.Models.Errors;
using Amanah.Api.Observability;
using Amanah.Api.Services.Storage;
using Amanah.Api.Services.Uploads;
using Amanah.Contracts.Errors;

namespace Amanah.Api.Services.Claims;

public sealed class ClaimPhotoAttachService(
    IBucketStorage bucketStorage,
    ReportImageProcessor imageProcessor,
    AppMetrics metrics)
{
    public async Task<Result<string>> AttachAsync(
        Guid claimId,
        IFormFile photo,
        CancellationToken cancellationToken = default)
    {
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
        var originalKey = ClaimPhotoStorageKeys.Original(claimId, uploadId);
        var thumbnailKey = ClaimPhotoStorageKeys.Thumbnail(claimId, uploadId);
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

        metrics.RecordUploadCompleted();
        return originalKey;
    }
}
