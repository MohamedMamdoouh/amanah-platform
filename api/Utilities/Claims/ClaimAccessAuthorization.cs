using Amanah.Api.Data.Entities;

namespace Amanah.Api.Utilities.Claims;

public static class ClaimAccessAuthorization
{
    public static bool IsReporter(Claim claim, Guid userId) =>
        claim.Report.ReporterId == userId;

    public static bool IsClaimant(Claim claim, Guid userId) =>
        claim.ClaimantId == userId;

    public static bool IsReporterOrClaimant(Claim claim, Guid userId) =>
        IsReporter(claim, userId) || IsClaimant(claim, userId);
}
