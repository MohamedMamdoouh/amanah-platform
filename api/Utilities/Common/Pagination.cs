using Amanah.Contracts.Responses.Browse;

namespace Amanah.Api.Utilities.Common;

public static class Pagination
{
    public static int ComputeTotalPages(int totalCount, int pageSize) =>
        totalCount == 0
            ? 0
            : (int)Math.Ceiling(totalCount / (double)pageSize);

    public static PaginatedResponse<TItem> Create<TItem>(
        IReadOnlyList<TItem> items,
        int page,
        int pageSize,
        int totalCount) =>
        new()
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount,
            TotalPages = ComputeTotalPages(totalCount, pageSize),
        };
}
