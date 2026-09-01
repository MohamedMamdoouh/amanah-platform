using Amanah.Contracts.Responses.Catalog;

namespace Amanah.Api.Services.Catalog;

public interface ICategoryLoader
{
    Task<CategoryListResponse> LoadCategoriesAsync(CancellationToken cancellationToken = default);
}
