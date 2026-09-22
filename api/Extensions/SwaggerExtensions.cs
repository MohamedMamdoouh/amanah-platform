using Amanah.Api.OpenApi;
using Asp.Versioning.ApiExplorer;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Amanah.Api.Extensions;

public static class SwaggerExtensions
{
    public static IServiceCollection AddApiSwagger(this IServiceCollection services)
    {
        services.AddTransient<IConfigureOptions<SwaggerGenOptions>, ConfigureSwaggerOptions>();

        services.AddSwaggerGen(options =>
        {
            options.DocInclusionPredicate(
                (documentName, apiDescription) => apiDescription.GroupName == documentName);

            options.AddSecurityDefinition(
                "Bearer",
                new OpenApiSecurityScheme
                {
                    Description =
                        "JWT access token. Example: \"Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...\"",
                    Name = "Authorization",
                    In = ParameterLocation.Header,
                    Type = SecuritySchemeType.Http,
                    Scheme = "bearer",
                    BearerFormat = "JWT",
                });

            options.OperationFilter<EndpointSummaryOperationFilter>();
            options.OperationFilter<AuthorizeOperationFilter>();
            options.OperationFilter<MultipartFormOperationFilter>();
        });

        return services;
    }

    public static WebApplication UseApiSwagger(this WebApplication app)
    {
        app.UseSwagger();
        app.UseSwaggerUI(options =>
        {
            options.RoutePrefix = "swagger";

            var descriptions = app.Services
                .GetRequiredService<IApiVersionDescriptionProvider>()
                .ApiVersionDescriptions
                .OrderBy(d => d.ApiVersion);

            foreach (var description in descriptions)
            {
                options.SwaggerEndpoint(
                    $"/swagger/{description.GroupName}/swagger.json",
                    $"Amanah {description.GroupName.ToUpperInvariant()}");
            }
        });

        return app;
    }
}
