namespace Amanah.Api.Services.Storage;

public sealed record BucketObject(string Key, DateTimeOffset LastModified);
