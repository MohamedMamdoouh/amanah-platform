using Amanah.Api.Models.Errors;
using Amanah.Contracts.Requests.Claims;
using Amanah.Contracts.Responses.Browse;
using Amanah.Contracts.Responses.Claims;

namespace Amanah.Api.Services.Claims;

public interface IClaimService
{
    Task<Result<SubmitClaimResponse>> SubmitAsync(
        Guid reportId,
        Guid claimantId,
        SubmitClaimRequest request,
        IFormFile? photo,
        CancellationToken cancellationToken = default);

    Task<Result> ApproveAsync(
        Guid claimId,
        Guid reporterId,
        CancellationToken cancellationToken = default);

    Task<Result> RejectAsync(
        Guid claimId,
        Guid reporterId,
        CancellationToken cancellationToken = default);

    Task<Result> WithdrawAsync(
        Guid claimId,
        Guid claimantId,
        CancellationToken cancellationToken = default);

    Task<Result<PaginatedResponse<MyClaimSummaryResponse>>> GetMineAsync(
        Guid claimantId,
        MyClaimsQuery query,
        CancellationToken cancellationToken = default);

    Task<Result<ClaimDetailResponse>> GetByIdAsync(
        Guid claimId,
        Guid userId,
        CancellationToken cancellationToken = default);
}
