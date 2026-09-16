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
        if (report.PublishedTimerResumedAt is null)
        {
            return;
        }

        var elapsedSinceResume = ElapsedSeconds(report.PublishedTimerResumedAt.Value, now);
        if (elapsedSinceResume > 0)
        {
            report.PublishedSecondsElapsed += elapsedSinceResume;
        }

        report.PublishedTimerResumedAt = null;
    }

    public void ResumePublishedTimer(Report report, DateTimeOffset now)
    {
        report.PublishedTimerResumedAt = now;
    }

    public int GetCumulativePublishedSeconds(Report report, DateTimeOffset now)
    {
        if (report.PublishedTimerResumedAt is null)
        {
            return report.PublishedSecondsElapsed;
        }

        return report.PublishedSecondsElapsed + ElapsedSeconds(report.PublishedTimerResumedAt.Value, now);
    }

    private static int ElapsedSeconds(DateTimeOffset startedAt, DateTimeOffset now)
    {
        var elapsed = (int)(now - startedAt).TotalSeconds;
        return Math.Max(0, elapsed);
    }
}
