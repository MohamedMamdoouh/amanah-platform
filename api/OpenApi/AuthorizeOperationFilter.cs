using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Amanah.Api.OpenApi;

public sealed class AuthorizeOperationFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        if (!RequiresAuthorization(context.MethodInfo, context.MethodInfo.DeclaringType))
        {
            return;
        }

        operation.Security ??= [];
        operation.Security.Add(new OpenApiSecurityRequirement
        {
            [new OpenApiSecuritySchemeReference("Bearer", context.Document)] = [],
        });
    }

    private static bool RequiresAuthorization(MethodInfo method, Type? controllerType)
    {
        if (method.GetCustomAttributes(typeof(AllowAnonymousAttribute), inherit: true).Length > 0)
        {
            return false;
        }

        if (method.GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true).Length > 0)
        {
            return true;
        }

        if (controllerType?.GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true).Length > 0)
        {
            return true;
        }

        return false;
    }
}
