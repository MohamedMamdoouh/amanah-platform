using System.Net;
using Amazon.S3;
using Amazon.S3.Model;
using Amanah.Api.Options;
using Microsoft.Extensions.Options;

namespace Amanah.Api.Services.Storage;

public sealed class R2BucketStorage(
    IOptions<BucketOptions> options,
    ILogger<R2BucketStorage> logger) : IBucketStorage
{
    private readonly BucketOptions _options = options.Value;
    private readonly AmazonS3Client _client = CreateClient(options.Value);

    public async Task PutAsync(
        string key,
        Stream content,
        string contentType,
        CancellationToken cancellationToken = default)
    {
        var request = new PutObjectRequest
        {
            BucketName = _options.Name,
            Key = key,
            InputStream = content,
            ContentType = contentType,
            DisablePayloadSigning = true,
            DisableDefaultChecksumValidation = true,
        };

        await _client.PutObjectAsync(request, cancellationToken);
    }

    public async Task CopyAsync(string sourceKey, string destKey, CancellationToken cancellationToken = default)
    {
        var request = new CopyObjectRequest
        {
            SourceBucket = _options.Name,
            SourceKey = sourceKey,
            DestinationBucket = _options.Name,
            DestinationKey = destKey,
        };

        await _client.CopyObjectAsync(request, cancellationToken);
    }

    public Task DeleteAsync(string key, CancellationToken cancellationToken = default) =>
        _client.DeleteObjectAsync(_options.Name, key, cancellationToken);

    public async Task DeleteManyAsync(IEnumerable<string> keys, CancellationToken cancellationToken = default)
    {
        foreach (var key in keys)
        {
            await DeleteAsync(key, cancellationToken);
        }
    }

    public async Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default)
    {
        try
        {
            await _client.GetObjectMetadataAsync(_options.Name, key, cancellationToken);
            return true;
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return false;
        }
    }

    public string GetPublicUrl(string key)
    {
        var endpoint = _options.Endpoint!.TrimEnd('/');
        return $"{endpoint}/{_options.Name}/{key}";
    }

    public Uri GetPreSignedUrl(string key, TimeSpan expiry)
    {
        var request = new GetPreSignedUrlRequest
        {
            BucketName = _options.Name,
            Key = key,
            Expires = DateTime.UtcNow.Add(expiry),
            Verb = HttpVerb.GET,
        };

        return new Uri(_client.GetPreSignedURL(request));
    }

    public async Task<IReadOnlyList<BucketObject>> ListAsync(
        string prefix,
        CancellationToken cancellationToken = default)
    {
        var results = new List<BucketObject>();
        string? continuationToken = null;

        do
        {
            var response = await _client.ListObjectsV2Async(
                new ListObjectsV2Request
                {
                    BucketName = _options.Name,
                    Prefix = prefix,
                    ContinuationToken = continuationToken,
                },
                cancellationToken);

            foreach (var entry in response.S3Objects)
            {
                results.Add(new BucketObject(
                    entry.Key,
                    new DateTimeOffset(DateTime.SpecifyKind(entry.LastModified, DateTimeKind.Utc))));
            }

            continuationToken = response.IsTruncated ? response.NextContinuationToken : null;
        }
        while (continuationToken is not null);

        return results;
    }

    public async Task<bool> PingAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await _client.ListObjectsV2Async(
                new ListObjectsV2Request
                {
                    BucketName = _options.Name,
                    MaxKeys = 1,
                },
                cancellationToken);
            return true;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Object storage health check failed.");
            return false;
        }
    }

    private static AmazonS3Client CreateClient(BucketOptions options)
    {
        var config = new AmazonS3Config
        {
            ServiceURL = options.Endpoint,
            ForcePathStyle = true,
            AuthenticationRegion = "auto",
        };

        return new AmazonS3Client(options.AccessKey, options.SecretKey, config);
    }
}
