namespace Amanah.Api.Observability;

public sealed class AppMetrics(ILogger<AppMetrics> logger)
{
    public void RecordHttpRequest(double durationMs, string method, string path, int statusCode)
    {
        var tags = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["method"] = method,
            ["path"] = path,
            ["status"] = statusCode.ToString(),
        };

        LogMetric("http.server.request.duration", durationMs, "ms", tags);

        if (statusCode >= 500)
        {
            LogMetric("http.server.request.errors", 1, "count", tags);
        }
    }

    public void RecordRateLimitRejected(string? policy = null)
    {
        Dictionary<string, object?>? tags = null;
        if (!string.IsNullOrWhiteSpace(policy))
        {
            tags = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["policy"] = policy,
            };
        }

        LogMetric("rate_limit.rejected", 1, "count", tags);
    }

    public void RecordReportSubmitted() => LogMetric("report.submitted", 1, "count");

    public void RecordUploadCompleted() => LogMetric("upload.photo.completed", 1, "count");

    public void RecordUploadFailed() => LogMetric("upload.photo.failed", 1, "count");

    public void RecordSmsCompleted() => LogMetric("sms.send.completed", 1, "count");

    public void RecordSmsFailed() => LogMetric("sms.send.failed", 1, "count");

    public void SetOtpOutboxBacklog(long count) => LogMetric("otp.outbox.backlog", count, "count");

    private static void LogMetric(
        ILogger logger,
        string name,
        double value,
        string unit,
        IReadOnlyDictionary<string, object?>? tags = null)
    {
        using (logger.BeginScope(new Dictionary<string, object?>
        {
            ["event"] = "metric",
            ["name"] = name,
            ["value"] = value,
            ["unit"] = unit,
            ["tags"] = tags ?? new Dictionary<string, object?>(StringComparer.Ordinal),
        }))
        {
            logger.LogInformation(
                "Metric {MetricName}={MetricValue} {MetricUnit}",
                name,
                value,
                unit);
        }
    }

    private void LogMetric(
        string name,
        double value,
        string unit,
        IReadOnlyDictionary<string, object?>? tags = null) =>
        LogMetric(logger, name, value, unit, tags);
}
