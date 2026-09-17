using Amanah.Api.Models.Errors;

namespace Amanah.Api.Services.Jobs;

public sealed class JobRunner(
    IEnumerable<ILifecycleJob> jobs,
    ILogger<JobRunner> logger) : IJobRunner
{
    private readonly IReadOnlyList<ILifecycleJob> _jobs = jobs.ToList();

    public IReadOnlyCollection<string> GetRegisteredJobNames() =>
        _jobs.Select(job => job.Name).OrderBy(name => name, StringComparer.Ordinal).ToList();

    public async Task<Result> RunAsync(string jobName, CancellationToken cancellationToken)
    {
        var job = FindJob(jobName);
        if (job is null)
        {
            return ResultError.NotFound($"Job '{jobName}' is not registered.");
        }

        await ExecuteJobAsync(job, cancellationToken);
        return Result.Ok();
    }

    public async Task RunAllAsync(CancellationToken cancellationToken)
    {
        foreach (var job in _jobs)
        {
            try
            {
                await ExecuteJobAsync(job, cancellationToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                logger.LogError(exception, "Lifecycle job {JobName} failed.", job.Name);
            }
        }
    }

    private ILifecycleJob? FindJob(string jobName) =>
        _jobs.FirstOrDefault(job =>
            string.Equals(job.Name, jobName, StringComparison.OrdinalIgnoreCase));

    private async Task ExecuteJobAsync(ILifecycleJob job, CancellationToken cancellationToken)
    {
        await job.ExecuteAsync(cancellationToken);
        logger.LogInformation("Lifecycle job {JobName} completed.", job.Name);
    }
}
