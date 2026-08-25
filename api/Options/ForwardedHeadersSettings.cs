namespace Amanah.Api.Options;

public class ForwardedHeadersOptions
{
    public const string SectionName = "ForwardedHeaders";

    public string[] KnownNetworks { get; set; } = [];
}
