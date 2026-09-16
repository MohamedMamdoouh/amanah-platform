using Amanah.Api.Models.Errors;

namespace Amanah.Api.Services.Jobs;

public interface ILifecycleJob
{
    string Name { get; }

    Task ExecuteAsync(CancellationToken cancellationToken);
}

public interface IJobRunner
{
    IReadOnlyCollection<string> GetRegisteredJobNames();

    Task<Result> RunAsync(string jobName, CancellationToken cancellationToken);

    Task RunAllAsync(CancellationToken cancellationToken);
}
