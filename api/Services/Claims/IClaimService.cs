using Amanah.Api.Models.Errors;
using Amanah.Contracts.Requests.Claims;
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
}
