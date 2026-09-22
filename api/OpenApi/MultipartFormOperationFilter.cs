using Amanah.Api.Controllers;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Amanah.Api.OpenApi;

public sealed class MultipartFormOperationFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        var endpointName = context.MethodInfo.GetCustomAttributes(typeof(EndpointNameAttribute), inherit: false)
            .OfType<EndpointNameAttribute>()
            .FirstOrDefault()
            ?.EndpointName;

        if (endpointName is null)
        {
            return;
        }

        operation.RequestBody = endpointName switch
        {
            nameof(ReportsController.CreateReport) => ReportBody(
                "JSON serialized CreateReportRequest. May also be sent as a file part (e.g. Angular Blob)."),
            nameof(ReportsController.UpdateReport) => ReportBody(
                "JSON serialized UpdateReportRequest. May also be sent as a file part (e.g. Angular Blob)."),
            nameof(ReportsController.SubmitClaim) => ClaimBody(),
            _ => operation.RequestBody,
        };
    }

    private static OpenApiRequestBody ReportBody(string reportDescription)
    {
        var required = new HashSet<string> { "report" };

        return new OpenApiRequestBody
        {
            Required = true,
            Content = new Dictionary<string, OpenApiMediaType>
            {
                ["multipart/form-data"] = new()
                {
                    Schema = new OpenApiSchema
                    {
                        Type = JsonSchemaType.Object,
                        Required = required,
                        Properties = new Dictionary<string, IOpenApiSchema>
                        {
                            ["report"] = new OpenApiSchema
                            {
                                Type = JsonSchemaType.String,
                                Description = reportDescription,
                            },
                            ["photos"] = new OpenApiSchema
                            {
                                Type = JsonSchemaType.Array,
                                Description = "Up to 5 images.",
                                Items = new OpenApiSchema
                                {
                                    Type = JsonSchemaType.String,
                                    Format = "binary",
                                },
                            },
                        },
                    },
                },
            },
        };
    }

    private static OpenApiRequestBody ClaimBody() =>
        new()
        {
            Required = true,
            Content = new Dictionary<string, OpenApiMediaType>
            {
                ["multipart/form-data"] = new()
                {
                    Schema = new OpenApiSchema
                    {
                        Type = JsonSchemaType.Object,
                        Required = new HashSet<string> { "claim" },
                        Properties = new Dictionary<string, IOpenApiSchema>
                        {
                            ["claim"] = new OpenApiSchema
                            {
                                Type = JsonSchemaType.String,
                                Description = "JSON serialized SubmitClaimRequest.",
                            },
                            ["photo"] = new OpenApiSchema
                            {
                                Type = JsonSchemaType.String,
                                Format = "binary",
                                Description = "Optional claim photo (at most one).",
                            },
                        },
                    },
                },
            },
        };
}
