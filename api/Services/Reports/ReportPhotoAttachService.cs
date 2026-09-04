using Amanah.Api.Data.Entities;
using Amanah.Api.Models.Errors;
using Amanah.Api.Services.Storage;
using Amanah.Api.Services.Uploads;
using Amanah.Contracts.Errors;

namespace Amanah.Api.Services.Reports;

public sealed class ReportPhotoAttachService(
    IBucketStorage bucketStorage,
    ReportImageProcessor imageProcessor)
{
    public async Task<ResultError?> AttachAsync(
        Report report,
        IReadOnlyList<IFormFile> photos,
        bool photosPrivate,
        CancellationToken cancellationToken = default)
    {
        if (photos.Count == 0)
        {
            return null;
        }

        var promotedKeys = new List<string>();
        try
        {
            for (var sortOrder = 0; sortOrder < photos.Count; sortOrder++)
            {
                var photo = photos[sortOrder];
                ProcessedReportImage processed;
                try
                {
                    await using var stream = photo.OpenReadStream();
                    processed = imageProcessor.Process(stream, photo.Length, photo.ContentType);
                }
                catch (ReportImageProcessingException ex)
                {
                    await bucketStorage.DeleteManyAsync(promotedKeys, cancellationToken);
                    return ResultError.BadRequest(
                        ex.Message,
                        ex.Code,
                        errors: new Dictionary<string, string[]>
                        {
                            [$"photos[{sortOrder}]"] = [ex.Message],
                        });
                }

                var photoId = Guid.NewGuid();
                var originalKey = ReportPhotoStorageKeys.PromotedOriginal(photosPrivate, report.Id, photoId);
                var thumbnailKey = ReportPhotoStorageKeys.PromotedThumbnail(photosPrivate, report.Id, photoId);

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
                finally
                {
                    await processed.OriginalStream.DisposeAsync();
                    await processed.ThumbnailStream.DisposeAsync();
                }

                report.Photos.Add(new ReportPhoto
                {
                    Id = photoId,
                    ReportId = report.Id,
                    StorageKey = originalKey,
                    ThumbnailStorageKey = thumbnailKey,
                    ContentType = processed.ContentType,
                    SizeBytes = processed.SizeBytes,
                    SortOrder = sortOrder,
                });
            }
        }
        catch (Exception)
        {
            await bucketStorage.DeleteManyAsync(promotedKeys, cancellationToken);
            return ResultError.ServiceUnavailable(
                "Photo upload is temporarily unavailable. Please try again later.",
                ErrorCodes.UploadStorageFailed);
        }

        return null;
    }
}
