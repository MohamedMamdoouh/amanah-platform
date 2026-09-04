namespace Amanah.Api.Services.Storage;

public interface IBucketStorage
{
    Task PutAsync(string key, Stream content, string contentType, CancellationToken cancellationToken = default);

    Task CopyAsync(string sourceKey, string destKey, CancellationToken cancellationToken = default);

    Task DeleteAsync(string key, CancellationToken cancellationToken = default);

    Task DeleteManyAsync(IEnumerable<string> keys, CancellationToken cancellationToken = default);

    string GetPublicUrl(string key);

    Uri GetPreSignedUrl(string key, TimeSpan expiry);

    Task<bool> PingAsync(CancellationToken cancellationToken = default);
}
