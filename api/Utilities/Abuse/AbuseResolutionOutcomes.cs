namespace Amanah.Api.Utilities.Abuse;

public static class AbuseResolutionOutcomes
{
    public const string NoAction = "no_action";

    public const string Takedown = "takedown";

    public const string Ban = "ban";

    public static readonly HashSet<string> All =
    [
        NoAction,
        Takedown,
        Ban,
    ];
}
