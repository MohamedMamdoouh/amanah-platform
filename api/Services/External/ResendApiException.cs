using System.Net;

namespace Amanah.Api.Services.External;

public sealed class ResendApiException(int statusCode, string message) : HttpRequestException(message, inner: null, statusCode: (HttpStatusCode)statusCode)
{
    public int StatusCodeValue { get; } = statusCode;

    public bool IsTransient => StatusCodeValue is 408 or 429 or 500 or 502 or 503 or 504;
}
