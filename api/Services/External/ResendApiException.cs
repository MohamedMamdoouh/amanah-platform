using System.Net;

namespace Amanah.Api.Services.External;

public sealed class ResendApiException(int statusCode, string message) : HttpRequestException(message, inner: null, statusCode: (HttpStatusCode)statusCode)
{
    public int StatusCodeValue { get; } = statusCode;

    public bool IsTransient => ResendHttpStatuses.IsTransient(StatusCodeValue);
}
