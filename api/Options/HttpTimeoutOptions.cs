namespace Amanah.Api.Options;

public sealed class HttpTimeoutOptions
{
    public const string SectionName = "HttpTimeouts";

    public int IncomingRequestSeconds { get; init; } = 30;

    public int OutgoingHttpSeconds { get; init; } = 10;
}
