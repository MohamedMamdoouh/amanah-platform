using Amanah.Api.Utilities;
using Amanah.Contracts.Requests.Reports;

namespace Amanah.Api.Tests.Reports;

public static class TestReportHelpers
{
    public static CreateReportRequest BuildValidLostRequest(
        string categoryCode = "phones",
        string governorateCode = "cairo",
        DateOnly? dateLostOrFound = null,
        string? title = null,
        string? description = null,
        string? hiddenDetail = null,
        string? areaText = "Ramses station platform 2",
        string? heldLocation = null,
        Dictionary<string, string>? categoryFields = null) =>
        new()
        {
            Type = "lost",
            CategoryCode = categoryCode,
            Title = title ?? "Lost black iPhone",
            Description = description ?? "I lost my phone near Ramses station yesterday evening.",
            DateLostOrFound = dateLostOrFound ?? CairoTime.TodayInCairo(),
            GovernorateCode = governorateCode,
            AreaText = areaText,
            HeldLocation = heldLocation,
            HasReward = false,
            HiddenDetail = hiddenDetail ?? "Contains a photo of my family inside.",
            CategoryFields = categoryFields ?? new Dictionary<string, string>
            {
                ["brand_model"] = "iPhone 14",
                ["colour"] = "black",
            },
        };

    public static CreateReportRequest BuildValidFoundRequest(
        string categoryCode = "phones",
        string governorateCode = "cairo",
        DateOnly? dateLostOrFound = null,
        string? title = null,
        string? heldLocation = "At Ramses police station") =>
        new()
        {
            Type = "found",
            CategoryCode = categoryCode,
            Title = title ?? "Found black iPhone",
            Description = "I found a phone near Ramses station yesterday evening.",
            DateLostOrFound = dateLostOrFound ?? CairoTime.TodayInCairo(),
            GovernorateCode = governorateCode,
            AreaText = "Ramses station platform 2",
            HeldLocation = heldLocation,
            HasReward = false,
            HiddenDetail = "Contains a photo of a family inside.",
            CategoryFields = new Dictionary<string, string>
            {
                ["brand_model"] = "iPhone 14",
                ["colour"] = "black",
            },
        };
}
