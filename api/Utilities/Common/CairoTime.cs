namespace Amanah.Api.Utilities.Common;

public static class CairoTime
{
    private const string CairoTimeZoneId = "Africa/Cairo";

    public static TimeZoneInfo CairoTimeZone { get; } =
        TimeZoneInfo.FindSystemTimeZoneById(CairoTimeZoneId);

    public static DateTimeOffset ToCairo(DateTimeOffset utc) =>
        TimeZoneInfo.ConvertTime(utc, CairoTimeZone);

    public static DateTimeOffset ToUtc(DateTimeOffset cairoLocal) =>
        TimeZoneInfo.ConvertTime(cairoLocal, TimeZoneInfo.Utc);

    public static DateOnly TodayInCairo() =>
        DateOnly.FromDateTime(ToCairo(DateTimeOffset.UtcNow).DateTime);

    public static DateTimeOffset CairoDayStartUtc(DateTimeOffset utc)
    {
        var cairo = ToCairo(utc);
        var midnight = DateTime.SpecifyKind(cairo.Date, DateTimeKind.Unspecified);
        var cairoMidnight = new DateTimeOffset(midnight, CairoTimeZone.GetUtcOffset(midnight));

        return ToUtc(cairoMidnight);
    }
}
