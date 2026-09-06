namespace Amanah.Api.Services.External;

public static class ResendHttpStatuses
{
    public static bool IsTransient(int statusCode) =>
        statusCode is 408 or 429 or 500 or 502 or 503 or 504;
}
