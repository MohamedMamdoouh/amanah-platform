using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Amanah.Api.OpenApi;

public sealed class EndpointSummaryOperationFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        var summary = context.MethodInfo.GetCustomAttributes(typeof(EndpointSummaryAttribute), inherit: false)
            .OfType<EndpointSummaryAttribute>()
            .FirstOrDefault()
            ?.Summary;

        if (!string.IsNullOrWhiteSpace(summary))
        {
            operation.Summary = summary;
        }
    }
}
