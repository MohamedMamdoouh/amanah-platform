using Amanah.Api.Data;
using Amanah.Api.Data.Entities;
using Amanah.Api.Models.Errors;
using Amanah.Api.Services.Storage;
using Amanah.Api.Utilities.Uploads;
using Amanah.Contracts.Errors;
using Amanah.Contracts.Responses.Uploads;
using Microsoft.EntityFrameworkCore;

namespace Amanah.Api.Services.Uploads;

public sealed class ReportPhotoPresignService(
    AppDbContext dbContext,
    IBucketStorage bucketStorage)
{
    public async Task<Result<ReportPhotoPresignResponse>> GetReportPhotoUrlAsync(
        Guid photoId,
        Guid userId,
        UserRole role,
        CancellationToken cancellationToken = default)
    {
        var photo = await dbContext.ReportPhotos
            .AsNoTracking()
            .Include(reportPhoto => reportPhoto.Report)
            .ThenInclude(report => report.Category)
            .SingleOrDefaultAsync(reportPhoto => reportPhoto.Id == photoId, cancellationToken);

        if (photo is null)
        {
            return ResultError.NotFound("Photo not found.");
        }

        if (!photo.Report.Category.PhotosPrivate)
        {
            return ResultError.NotFound("Photo not found.");
        }

        var isReporter = photo.Report.ReporterId == userId;
        var isAdmin = role == UserRole.Admin;
        if (!isReporter && !isAdmin)
        {
            return ResultError.NotFound("Photo not found.");
        }

        if (!isReporter && photo.Report.Status is not ReportStatus.PendingReview and not ReportStatus.Rejected)
        {
            return ResultError.NotFound("Photo not found.");
        }

        var storageKey = StorageKeyResolver.ResolvePreferThumbnail(
            photo.StorageKey,
            photo.ThumbnailStorageKey);
        var url = bucketStorage.GetPreSignedUrl(storageKey, PresignConstants.Lifetime);

        return new ReportPhotoPresignResponse
        {
            Url = url.ToString(),
        };
    }
}
