namespace Amanah.Contracts.Requests.Browse;

public sealed class BrowseReportsQuery
{
    public string? Q { get; init; }

    public string? Category { get; init; }

    public string? Governorate { get; init; }

    public string? Type { get; init; }

    public DateOnly? DateFrom { get; init; }

    public DateOnly? DateTo { get; init; }

    public int Page { get; init; } = 1;

    public int PageSize { get; init; } = 20;
}
