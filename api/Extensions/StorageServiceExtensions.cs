using Amanah.Api.Options;
using Amanah.Api.Services.Storage;
using Microsoft.Extensions.Options;

namespace Amanah.Api.Extensions;

public static class StorageServiceExtensions
{
    public static IServiceCollection AddBucketStorage(this IServiceCollection services)
    {
        services.AddSingleton<IBucketStorage>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<BucketOptions>>().Value;
            return options.IsConfigured
                ? ActivatorUtilities.CreateInstance<R2BucketStorage>(sp)
                : new FakeBucketStorage();
        });

        return services;
    }
}
