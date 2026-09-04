using Amanah.Api.Data;
using Amanah.Api.Data.Entities;
using Amanah.Api.Models.Errors;
using Amanah.Api.Services.Storage;
using Amanah.Contracts.Errors;
using Amanah.Contracts.Responses.Uploads;
using Microsoft.EntityFrameworkCore;

namespace Amanah.Api.Services.Uploads;

public sealed class ReportPhotoPresignService(
    AppDbContext dbContext,
    IBucketStorage bucketStorage)
{
    private static readonly TimeSpan PresignLifetime = TimeSpan.FromMinutes(5);

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

        var storageKey = photo.ThumbnailStorageKey ?? photo.StorageKey;
        var url = bucketStorage.GetPreSignedUrl(storageKey, PresignLifetime);

        return new ReportPhotoPresignResponse
        {
            Url = url.ToString(),
        };
    }
}
