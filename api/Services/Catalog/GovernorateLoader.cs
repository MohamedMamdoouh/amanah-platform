using Amanah.Api.Data;
using Amanah.Contracts.Responses.Catalog;
using Microsoft.EntityFrameworkCore;

namespace Amanah.Api.Services.Catalog;

public sealed class GovernorateLoader(AppDbContext dbContext) : IGovernorateLoader
{
    public async Task<GovernorateListResponse> LoadGovernoratesAsync(CancellationToken cancellationToken = default)
    {
        var governorates = await dbContext.Governorates
            .AsNoTracking()
            .OrderBy(governorate => governorate.SortOrder)
            .Select(governorate => new GovernorateResponse
            {
                Code = governorate.Code,
                SortOrder = governorate.SortOrder,
            })
            .ToListAsync(cancellationToken);

        return new GovernorateListResponse { Items = governorates };
    }
}
