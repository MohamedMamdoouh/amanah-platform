using Amanah.Api.Validators.Auth;
using FluentValidation;
using FluentValidation.AspNetCore;

namespace Amanah.Api.Extensions;

public static class FluentValidationExtensions
{
    public static IServiceCollection AddApiValidation(this IServiceCollection services)
    {
        services.AddValidatorsFromAssemblyContaining<RegisterRequestValidator>();
        services.AddFluentValidationAutoValidation(options =>
        {
            options.DisableDataAnnotationsValidation = true;
        });

        return services;
    }
}
