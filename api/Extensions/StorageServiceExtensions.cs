using Amanah.Api.Options;
using Amanah.Api.Services.Storage;
using Microsoft.Extensions.Options;

namespace Amanah.Api.Extensions;

public static class StorageServiceExtensions
{
    public static IServiceCollection AddBucketStorage(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<StorageDeletionOutboxOptions>(
            configuration.GetSection(StorageDeletionOutboxOptions.SectionName));

        services.AddSingleton<IBucketStorage>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<BucketOptions>>().Value;
            return options.IsConfigured
                ? ActivatorUtilities.CreateInstance<R2BucketStorage>(sp)
                : new FakeBucketStorage();
        });

        services.AddScoped<StorageDeletionEnqueueService>();
        services.AddScoped<StorageDeletionOutboxDispatcher>();
        services.AddScoped<StorageDeletionOutboxBatchService>();
        services.AddHostedService<StorageDeletionOutboxProcessor>();

        return services;
    }
}
