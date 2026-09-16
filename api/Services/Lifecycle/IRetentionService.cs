using Amanah.Api.Data.Entities;

namespace Amanah.Api.Services.Lifecycle;

public interface IRetentionService
{
    IReadOnlyList<string> RemoveReportPhotos(Report report);

    Task DeleteReportPhotoStorageAsync(
        IReadOnlyList<string> storageKeys,
        CancellationToken cancellationToken = default);
}
