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
        _objects[key] = new StoredObject(
            memory.ToArray(),
            contentType,
            DateTimeOffset.UtcNow);
        return Task.CompletedTask;
    }

    public Task CopyAsync(string sourceKey, string destKey, CancellationToken cancellationToken = default)
    {
        if (!_objects.TryGetValue(sourceKey, out var source))
        {
            throw new KeyNotFoundException($"Object '{sourceKey}' was not found.");
        }

        _objects[destKey] = source with
        {
            Data = source.Data.ToArray(),
            LastModified = DateTimeOffset.UtcNow,
        };
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

    public Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default) =>
        Task.FromResult(_objects.ContainsKey(key));

    public string GetPublicUrl(string key) => $"https://fake.local/{key}";

    public Uri GetPreSignedUrl(string key, TimeSpan expiry)
    {
        var expiresAt = DateTimeOffset.UtcNow.Add(expiry).ToUnixTimeSeconds();
        return new Uri($"{GetPublicUrl(key)}?expires={expiresAt}");
    }

    public Task<bool> PingAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(true);

    public Task<IReadOnlyList<BucketObject>> ListAsync(
        string prefix,
        CancellationToken cancellationToken = default)
    {
        var results = _objects
            .Where(entry => entry.Key.StartsWith(prefix, StringComparison.Ordinal))
            .Select(entry => new BucketObject(entry.Key, entry.Value.LastModified))
            .OrderBy(entry => entry.Key, StringComparer.Ordinal)
            .ToList();

        return Task.FromResult<IReadOnlyList<BucketObject>>(results);
    }

    public bool ContainsKey(string key) => _objects.ContainsKey(key);

    public void SetLastModifiedForTesting(string key, DateTimeOffset lastModified)
    {
        if (!_objects.TryGetValue(key, out var storedObject))
        {
            throw new KeyNotFoundException($"Object '{key}' was not found.");
        }

        _objects[key] = storedObject with { LastModified = lastModified };
    }

    private sealed record StoredObject(byte[] Data, string ContentType, DateTimeOffset LastModified);
}
