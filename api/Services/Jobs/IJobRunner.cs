namespace Amanah.Api.Services.Jobs;

public interface ILifecycleJob
{
    string Name { get; }

    Task ExecuteAsync(CancellationToken cancellationToken);
}
