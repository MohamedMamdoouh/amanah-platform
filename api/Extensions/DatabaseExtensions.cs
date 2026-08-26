using Amanah.Api.Data;
using Amanah.Api.Data.Seeds;
using Microsoft.EntityFrameworkCore;

namespace Amanah.Api.Extensions;

public static class DatabaseExtensions
{
    public static IServiceCollection AddDatabase(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Default");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException("ConnectionStrings:Default is required.");
        }

        services.AddDbContext<AppDbContext>(options =>
            options.UseNpgsql(connectionString));

        services.AddScoped<CatalogSeeder>();
        services.AddHostedService<DatabaseMigrationHostedService>();

        return services;
    }
}
