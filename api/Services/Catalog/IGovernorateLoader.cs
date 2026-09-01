using Amanah.Contracts.Responses.Catalog;

namespace Amanah.Api.Services.Catalog;

public interface IGovernorateLoader
{
    Task<GovernorateListResponse> LoadGovernoratesAsync(CancellationToken cancellationToken = default);
}
