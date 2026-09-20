namespace Amanah.Api.Utilities.Abuse;

public static class AbuseFlagReasons
{
    public const string ScamFraud = "abuse.scam_fraud";
    public const string Spam = "abuse.spam";
    public const string IllegalProhibited = "abuse.illegal_prohibited";
    public const string HarassmentThreat = "abuse.harassment_threat";
    public const string Other = "abuse.other";

    public static readonly IReadOnlyList<string> All =
    [
        ScamFraud,
        Spam,
        IllegalProhibited,
        HarassmentThreat,
        Other,
    ];
}
