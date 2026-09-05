namespace Amanah.Api.Utilities.Reports;

public static class RejectionReasonCodes
{
    public const string UnclearPhotos = "rejection.unclear_photos";
    public const string SpamOrScam = "rejection.spam_or_scam";
    public const string DuplicateReport = "rejection.duplicate_report";
    public const string InsufficientDescription = "rejection.insufficient_description";
    public const string ContactInfo = "rejection.contact_info";
    public const string ProhibitedItem = "rejection.prohibited_item";
    public const string WrongCategory = "rejection.wrong_category";
    public const string RawIdNumber = "rejection.raw_id_number";

    public static readonly IReadOnlyList<string> All =
    [
        UnclearPhotos,
        SpamOrScam,
        DuplicateReport,
        InsufficientDescription,
        ContactInfo,
        ProhibitedItem,
        WrongCategory,
        RawIdNumber,
    ];
}
