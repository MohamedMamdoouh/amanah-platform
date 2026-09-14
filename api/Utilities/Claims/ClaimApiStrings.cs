using Amanah.Api.Data.Entities;

namespace Amanah.Api.Utilities.Claims;

public static class ClaimApiStrings
{
    public static string ToStatus(ClaimStatus status) => status switch
    {
        ClaimStatus.Pending => "pending",
        ClaimStatus.Approved => "approved",
        ClaimStatus.Rejected => "rejected",
        ClaimStatus.Withdrawn => "withdrawn",
        ClaimStatus.Cancelled => "cancelled",
        _ => status.ToString().ToLowerInvariant(),
    };
}
