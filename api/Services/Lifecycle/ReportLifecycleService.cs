using Amanah.Api.Data.Entities;

namespace Amanah.Api.Services.Lifecycle;

public sealed class ReportLifecycleService : IReportLifecycleService
{
    public void InitializePublishedTimer(Report report, DateTimeOffset now)
    {
        report.PublishedAt = now;
        report.PublishedTimerResumedAt = now;
        report.PublishedSecondsElapsed = 0;
    }

    public void PausePublishedTimer(Report report, DateTimeOffset now)
    {
        // Reports published before InitializePublishedTimer only have PublishedAt set.
        // Falling back avoids silently dropping that wall-clock time on the first pause.
        var segmentStartedAt = report.PublishedTimerResumedAt
            ?? (report.PublishedSecondsElapsed == 0 ? report.PublishedAt : null);

        if (segmentStartedAt is null)
        {
            return;
        }

        var elapsedSinceResume = ElapsedSeconds(segmentStartedAt.Value, now);
        if (elapsedSinceResume > 0)
        {
            report.PublishedSecondsElapsed += elapsedSinceResume;
        }

        report.PublishedTimerResumedAt = null;
    }

    public void ResumePublishedTimer(Report report, DateTimeOffset now)
    {
        // Already running — resetting ResumedAt would discard the current segment.
        if (report.PublishedTimerResumedAt is not null)
        {
            return;
        }

        report.PublishedTimerResumedAt = now;
    }

    public int GetCumulativePublishedSeconds(Report report, DateTimeOffset now)
    {
        var segmentStartedAt = report.PublishedTimerResumedAt
            ?? (report.Status == ReportStatus.Published
                && report.PublishedSecondsElapsed == 0
                    ? report.PublishedAt
                    : null);

        if (segmentStartedAt is null)
        {
            return report.PublishedSecondsElapsed;
        }

        return report.PublishedSecondsElapsed + ElapsedSeconds(segmentStartedAt.Value, now);
    }

    private static int ElapsedSeconds(DateTimeOffset startedAt, DateTimeOffset now)
    {
        var elapsed = (int)(now - startedAt).TotalSeconds;
        return Math.Max(0, elapsed);
    }
}
