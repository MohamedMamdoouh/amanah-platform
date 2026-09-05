using Amanah.Api.Utilities.Common;

namespace Amanah.Api.Tests.Utilities;

public class CairoTimeTests
{
    [Fact]
    public void ToCairo_and_ToUtc_convert_between_utc_and_cairo_local_time()
    {
        var utc = new DateTimeOffset(2026, 1, 15, 10, 30, 0, TimeSpan.Zero);

        var cairo = CairoTime.ToCairo(utc);
        var backToUtc = CairoTime.ToUtc(cairo);

        Assert.Equal(12, cairo.Hour);
        Assert.Equal(30, cairo.Minute);
        Assert.Equal(utc, backToUtc);
    }

    [Fact]
    public void CairoDayStartUtc_at_23_59_cairo_returns_correct_utc_boundary()
    {
        var utc = new DateTimeOffset(2026, 1, 15, 21, 59, 0, TimeSpan.Zero);
        var cairoLocal = CairoTime.ToCairo(utc);

        Assert.Equal(23, cairoLocal.Hour);
        Assert.Equal(59, cairoLocal.Minute);

        var dayStart = CairoTime.CairoDayStartUtc(utc);
        var dayStartLocal = CairoTime.ToCairo(dayStart);

        Assert.Equal(new DateOnly(2026, 1, 15), DateOnly.FromDateTime(dayStartLocal.DateTime));
        Assert.Equal(0, dayStartLocal.Hour);
        Assert.Equal(0, dayStartLocal.Minute);
    }

    [Fact]
    public void CairoDayStartUtc_at_00_01_next_cairo_day_differs_from_23_59()
    {
        var lateNightUtc = new DateTimeOffset(2026, 1, 15, 21, 59, 0, TimeSpan.Zero);
        var afterMidnightUtc = new DateTimeOffset(2026, 1, 15, 22, 1, 0, TimeSpan.Zero);

        var lateNightDayStart = CairoTime.CairoDayStartUtc(lateNightUtc);
        var afterMidnightDayStart = CairoTime.CairoDayStartUtc(afterMidnightUtc);

        Assert.NotEqual(lateNightDayStart, afterMidnightDayStart);
        Assert.Equal(TimeSpan.FromDays(1), afterMidnightDayStart - lateNightDayStart);
    }

    [Fact]
    public void TodayInCairo_supports_report_date_validation_rules()
    {
        var today = CairoTime.TodayInCairo();
        var tomorrow = today.AddDays(1);
        var tooOld = today.AddMonths(-12).AddDays(-1);
        var exactlyTwelveMonthsAgo = today.AddMonths(-12);

        Assert.False(IsReportDateInValidRange(tomorrow, maxMonthsAgo: 12));
        Assert.False(IsReportDateInValidRange(tooOld, maxMonthsAgo: 12));
        Assert.True(IsReportDateInValidRange(today, maxMonthsAgo: 12));
        Assert.True(IsReportDateInValidRange(exactlyTwelveMonthsAgo, maxMonthsAgo: 12));
    }

    private static bool IsReportDateInValidRange(DateOnly date, int maxMonthsAgo)
    {
        var today = CairoTime.TodayInCairo();
        return date <= today && date >= today.AddMonths(-maxMonthsAgo);
    }
}
