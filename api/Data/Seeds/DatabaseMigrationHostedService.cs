using Microsoft.EntityFrameworkCore;

namespace Amanah.Api.Data.Seeds;

public sealed class DatabaseMigrationHostedService(
    IServiceProvider serviceProvider,
    IConfiguration configuration,
    ILogger<DatabaseMigrationHostedService> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (!configuration.GetValue("Database:AutoMigrate", true))
        {
            return;
        }

        logger.LogInformation("Applying database migrations and seeding catalog data.");

        await using var scope = serviceProvider.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await context.Database.MigrateAsync(cancellationToken);
        await scope.ServiceProvider.GetRequiredService<CatalogSeeder>().SeedAsync(cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
