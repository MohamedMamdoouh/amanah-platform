namespace Amanah.Api.Models;

public static class ErrorCodes
{
    public const string ValidationFailed = "validation.failed";
    public const string Unauthorized = "auth.unauthorized";
    public const string Forbidden = "auth.forbidden";
    public const string NotFound = "resource.not_found";
    public const string Conflict = "resource.conflict";
    public const string RateLimitExceeded = "rate_limit.exceeded";
    public const string InternalError = "internal.error";
}
