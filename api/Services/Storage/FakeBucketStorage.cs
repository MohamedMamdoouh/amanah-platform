using System.Collections.Concurrent;

namespace Amanah.Api.Services.Storage;

public sealed class FakeBucketStorage : IBucketStorage
{
    private readonly ConcurrentDictionary<string, StoredObject> _objects = new(StringComparer.Ordinal);

    public Task PutAsync(
        string key,
        Stream content,
        string contentType,
        CancellationToken cancellationToken = default)
    {
        using var memory = new MemoryStream();
        content.CopyTo(memory);
        _objects[key] = new StoredObject(memory.ToArray(), contentType);
        return Task.CompletedTask;
    }

    public Task CopyAsync(string sourceKey, string destKey, CancellationToken cancellationToken = default)
    {
        if (!_objects.TryGetValue(sourceKey, out var source))
        {
            throw new KeyNotFoundException($"Object '{sourceKey}' was not found.");
        }

        _objects[destKey] = source with { Data = source.Data.ToArray() };
        return Task.CompletedTask;
    }

    public Task DeleteAsync(string key, CancellationToken cancellationToken = default)
    {
        _objects.TryRemove(key, out _);
        return Task.CompletedTask;
    }

    public Task DeleteManyAsync(IEnumerable<string> keys, CancellationToken cancellationToken = default)
    {
        foreach (var key in keys)
        {
            _objects.TryRemove(key, out _);
        }

        return Task.CompletedTask;
    }

    public string GetPublicUrl(string key) => $"https://fake.local/{key}";

    public Uri GetPreSignedUrl(string key, TimeSpan expiry)
    {
        var expiresAt = DateTimeOffset.UtcNow.Add(expiry).ToUnixTimeSeconds();
        return new Uri($"{GetPublicUrl(key)}?expires={expiresAt}");
    }

    public Task<bool> PingAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(true);

    public bool ContainsKey(string key) => _objects.ContainsKey(key);

    private sealed record StoredObject(byte[] Data, string ContentType);
}
