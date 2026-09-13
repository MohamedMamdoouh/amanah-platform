using Amanah.Api.Services.Uploads;

namespace Amanah.Api.Extensions;

public static class UploadServiceExtensions
{
    public static IServiceCollection AddUploadServices(this IServiceCollection services)
    {
        services.AddSingleton<ReportImageProcessor>();
        services.AddScoped<ReportPhotoPresignService>();
        services.AddScoped<ClaimPhotoPresignService>();
        services.AddScoped<ChatAttachmentAttachService>();
        services.AddScoped<ChatAttachmentPresignService>();

        return services;
    }
}
