using Amanah.Api.Data.Entities;

namespace Amanah.Api.Utilities.Reports;

public static class ReportApiStrings
{
    public static string ToType(ReportType type) => type switch
    {
        ReportType.Lost => "lost",
        ReportType.Found => "found",
        _ => type.ToString().ToLowerInvariant(),
    };

    public static string ToStatus(ReportStatus status) => status switch
    {
        ReportStatus.PendingReview => "pending_review",
        ReportStatus.Rejected => "rejected",
        ReportStatus.Published => "published",
        ReportStatus.ClaimInProgress => "claim_in_progress",
        ReportStatus.Resolved => "resolved",
        ReportStatus.Withdrawn => "withdrawn",
        ReportStatus.RemovedByAdmin => "removed_by_admin",
        _ => status.ToString().ToLowerInvariant(),
    };

    public static bool TryParseType(string type, out ReportType reportType)
    {
        switch (type.Trim().ToLowerInvariant())
        {
            case "lost":
                reportType = ReportType.Lost;
                return true;
            case "found":
                reportType = ReportType.Found;
                return true;
            default:
                reportType = default;
                return false;
        }
    }

    public static ReportStatus? ParseStatusFilter(string? status) => status switch
    {
        null or "" or "pending_review" => ReportStatus.PendingReview,
        "rejected" => ReportStatus.Rejected,
        "published" => ReportStatus.Published,
        "claim_in_progress" => ReportStatus.ClaimInProgress,
        "resolved" => ReportStatus.Resolved,
        "withdrawn" => ReportStatus.Withdrawn,
        "removed_by_admin" => ReportStatus.RemovedByAdmin,
        _ => null,
    };
}
