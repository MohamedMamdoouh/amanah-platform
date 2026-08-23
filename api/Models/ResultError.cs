namespace Amanah.Api.Models;

public sealed record ResultError(
    string Code,
    string Message,
    int StatusCode,
    Dictionary<string, string[]>? Errors = null)
{
    public static ResultError BadRequest(
        string message,
        string code = ErrorCodes.ValidationFailed,
        Dictionary<string, string[]>? errors = null) =>
        new(code, message, StatusCodes.Status400BadRequest, errors);

    public static ResultError NotFound(
        string message,
        string code = ErrorCodes.NotFound) =>
        new(code, message, StatusCodes.Status404NotFound);

    public static ResultError Conflict(
        string message,
        string code = ErrorCodes.Conflict) =>
        new(code, message, StatusCodes.Status409Conflict);

    public static ResultError Unauthorized(
        string message,
        string code = ErrorCodes.Unauthorized) =>
        new(code, message, StatusCodes.Status401Unauthorized);

    public static ResultError Forbidden(
        string message,
        string code = ErrorCodes.Forbidden) =>
        new(code, message, StatusCodes.Status403Forbidden);
}
