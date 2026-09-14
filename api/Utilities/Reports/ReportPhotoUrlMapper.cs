using Amanah.Api.Services.Storage;

namespace Amanah.Api.Utilities.Reports;

public static class ReportPhotoUrlMapper
{
    public static string? ToThumbnailUrl(
        IBucketStorage bucketStorage,
        bool photosPrivate,
        string? thumbnailStorageKey) =>
        photosPrivate || thumbnailStorageKey is null
            ? null
            : bucketStorage.GetPublicUrl(thumbnailStorageKey);
}
