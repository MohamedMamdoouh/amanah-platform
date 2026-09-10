namespace Amanah.Api.Services.Uploads;

public static class ClaimPhotoStorageKeys
{
    private const string Prefix = "private/claims/";

    public static string Original(Guid claimId, Guid uploadId) =>
        $"{Prefix}{claimId:N}/{uploadId:N}";

    public static string Thumbnail(Guid claimId, Guid uploadId) =>
        $"{Original(claimId, uploadId)}_thumb.webp";

    public static string ThumbnailForOriginal(string originalKey) =>
        $"{originalKey}_thumb.webp";
}
