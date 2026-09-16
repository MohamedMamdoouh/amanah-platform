using Amanah.Api.Services.Jobs;
using Microsoft.Extensions.Logging.Abstractions;

namespace Amanah.Api.Tests.Jobs;

public class JobRunnerTests
{
    [Fact]
    public async Task RunAsync_unknown_job_returns_not_found()
    {
        var runner = new JobRunner([], NullLogger<JobRunner>.Instance);

        var result = await runner.RunAsync("MissingJob", CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Error);
        Assert.Equal(404, result.Error.StatusCode);
    }

    [Fact]
    public async Task RunAsync_executes_registered_job()
    {
        var job = new RecordingLifecycleJob("ListingAutoExpiry");
        var runner = new JobRunner([job], NullLogger<JobRunner>.Instance);

        var result = await runner.RunAsync("ListingAutoExpiry", CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, job.ExecutionCount);
    }

    [Fact]
    public void GetRegisteredJobNames_returns_sorted_job_names()
    {
        var runner = new JobRunner(
            [
                new RecordingLifecycleJob("ChatRetention"),
                new RecordingLifecycleJob("ListingAutoExpiry"),
            ],
            NullLogger<JobRunner>.Instance);

        var names = runner.GetRegisteredJobNames();

        Assert.Equal(["ChatRetention", "ListingAutoExpiry"], names);
    }

    private sealed class RecordingLifecycleJob(string name) : ILifecycleJob
    {
        public string Name { get; } = name;

        public int ExecutionCount { get; private set; }

        public Task ExecuteAsync(CancellationToken cancellationToken)
        {
            ExecutionCount++;
            return Task.CompletedTask;
        }
    }
}
