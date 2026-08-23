namespace Amanah.Api.Configuration;

public class ForwardedHeadersSettings
{
    public const string SectionName = "ForwardedHeaders";

    public string[] KnownNetworks { get; set; } = [];
}
