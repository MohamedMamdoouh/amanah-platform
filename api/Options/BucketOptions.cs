namespace Amanah.Api.Options;

public sealed class BucketOptions
{
    public const string SectionName = "Bucket";

    public string? Endpoint { get; init; }

    public string? AccessKey { get; init; }

    public string? SecretKey { get; init; }

    public string? Name { get; init; }

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(Endpoint)
        && !string.IsNullOrWhiteSpace(AccessKey)
        && !string.IsNullOrWhiteSpace(SecretKey)
        && !string.IsNullOrWhiteSpace(Name);
}
