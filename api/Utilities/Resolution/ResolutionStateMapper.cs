using Amanah.Api.Data.Entities;
using Amanah.Contracts.Responses.Claims;

namespace Amanah.Api.Utilities.Resolution;

public static class ResolutionStateMapper
{
    public static ResolutionStateResponse? FromClaim(Claim claim, bool isReporter)
    {
        if (claim.Status is not (ClaimStatus.Approved or ClaimStatus.Cancelled))
        {
            return null;
        }

        var resolution = claim.Report.Resolution;
        var reporterConfirmed = resolution?.ReporterConfirmedAt is not null;
        var claimantConfirmed = resolution?.ClaimantConfirmedAt is not null;
        var currentUserHasConfirmed = isReporter ? reporterConfirmed : claimantConfirmed;

        return new ResolutionStateResponse
        {
            ReporterConfirmedAt = resolution?.ReporterConfirmedAt,
            ClaimantConfirmedAt = resolution?.ClaimantConfirmedAt,
            ResolvedAt = resolution?.ResolvedAt,
            CurrentUserHasConfirmed = currentUserHasConfirmed,
            CurrentUserCanCancel = claim.Status == ClaimStatus.Approved
                && !currentUserHasConfirmed,
        };
    }
}
