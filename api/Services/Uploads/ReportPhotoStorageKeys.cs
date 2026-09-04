namespace Amanah.Api.Services.Uploads;

public static class ReportPhotoStorageKeys
{
    public static string PromotedOriginal(bool photosPrivate, Guid reportId, Guid photoId) =>
        $"{VisibilityPrefix(photosPrivate)}reports/{reportId:N}/{photoId:N}";

    public static string PromotedThumbnail(bool photosPrivate, Guid reportId, Guid photoId) =>
        $"{VisibilityPrefix(photosPrivate)}reports/{reportId:N}/{photoId:N}_thumb.webp";

    private static string VisibilityPrefix(bool photosPrivate) =>
        photosPrivate ? "private/" : "public/";
}
