using Amanah.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace Amanah.Api.Extensions;

public static class DatabaseExtensions
{
    public static IServiceCollection AddDatabase(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<AppDbContext>(options =>
            options.UseNpgsql(configuration.GetConnectionString("Default") ?? throw new InvalidOperationException("Connection string is null")));

        return services;
    }
}
