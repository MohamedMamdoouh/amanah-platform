using Amanah.Api.Data.Entities;
using Amanah.Api.Services.Lifecycle;

namespace Amanah.Api.Tests.Lifecycle;

public class ReportLifecycleServiceTests
{
    [Fact]
    public void GetCumulativePublishedSeconds_includes_running_segment_when_timer_is_active()
    {
        var lifecycleService = new ReportLifecycleService();
        var startedAt = new DateTimeOffset(2026, 9, 1, 12, 0, 0, TimeSpan.Zero);
        var now = startedAt.AddHours(2);

        var report = CreateReport(
            ReportStatus.Published,
            publishedAt: startedAt,
            resumedAt: startedAt,
            elapsedSeconds: 3_600);

        var cumulativeSeconds = lifecycleService.GetCumulativePublishedSeconds(report, now);

        Assert.Equal(3_600 + 7_200, cumulativeSeconds);
    }

    [Fact]
    public void GetCumulativePublishedSeconds_uses_frozen_elapsed_seconds_when_timer_is_paused()
    {
        var lifecycleService = new ReportLifecycleService();

        var report = CreateReport(
            ReportStatus.ClaimInProgress,
            publishedAt: DateTimeOffset.UtcNow.AddDays(-2),
            resumedAt: null,
            elapsedSeconds: 1_800);

        var cumulativeSeconds = lifecycleService.GetCumulativePublishedSeconds(
            report,
            DateTimeOffset.UtcNow.AddDays(30));

        Assert.Equal(1_800, cumulativeSeconds);
    }

    [Fact]
    public void PausePublishedTimer_falls_back_to_PublishedAt_when_resumed_at_is_null()
    {
        var lifecycleService = new ReportLifecycleService();
        var publishedAt = new DateTimeOffset(2026, 7, 1, 12, 0, 0, TimeSpan.Zero);
        var now = publishedAt.AddDays(60);

        var report = CreateReport(
            ReportStatus.ClaimInProgress,
            publishedAt: publishedAt,
            resumedAt: null,
            elapsedSeconds: 0);

        lifecycleService.PausePublishedTimer(report, now);

        Assert.Null(report.PublishedTimerResumedAt);
        Assert.Equal(60 * 24 * 60 * 60, report.PublishedSecondsElapsed);
    }

    [Fact]
    public void GetCumulativePublishedSeconds_uses_PublishedAt_for_legacy_published_rows()
    {
        var lifecycleService = new ReportLifecycleService();
        var publishedAt = new DateTimeOffset(2026, 8, 1, 12, 0, 0, TimeSpan.Zero);
        var now = publishedAt.AddDays(10);

        var report = CreateReport(
            ReportStatus.Published,
            publishedAt: publishedAt,
            resumedAt: null,
            elapsedSeconds: 0);

        var cumulativeSeconds = lifecycleService.GetCumulativePublishedSeconds(report, now);

        Assert.Equal(10 * 24 * 60 * 60, cumulativeSeconds);
    }

    [Fact]
    public void ResumePublishedTimer_does_not_reset_an_already_running_segment()
    {
        var lifecycleService = new ReportLifecycleService();
        var resumedAt = new DateTimeOffset(2026, 9, 1, 12, 0, 0, TimeSpan.Zero);

        var report = CreateReport(
            ReportStatus.Published,
            publishedAt: resumedAt,
            resumedAt: resumedAt,
            elapsedSeconds: 1_200);

        lifecycleService.ResumePublishedTimer(report, resumedAt.AddHours(3));

        Assert.Equal(resumedAt, report.PublishedTimerResumedAt);
        Assert.Equal(1_200, report.PublishedSecondsElapsed);
    }

    private static Report CreateReport(
        ReportStatus status,
        DateTimeOffset? publishedAt,
        DateTimeOffset? resumedAt,
        int elapsedSeconds) =>
        new()
        {
            ReporterId = Guid.NewGuid(),
            Type = ReportType.Lost,
            CategoryId = Guid.NewGuid(),
            Title = "Timer test",
            Description = "Description",
            DateLostOrFound = DateOnly.FromDateTime(DateTime.UtcNow),
            GovernorateId = Guid.NewGuid(),
            Status = status,
            HiddenDetail = "Hidden",
            PublishedAt = publishedAt,
            PublishedTimerResumedAt = resumedAt,
            PublishedSecondsElapsed = elapsedSeconds,
        };
}
