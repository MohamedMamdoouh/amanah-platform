using Amanah.Contracts.Errors;
using Microsoft.AspNetCore.Mvc;

namespace Amanah.Api.Models.Errors;

public sealed record ResultError(
    string Code,
    string Message,
    int StatusCode,
    Dictionary<string, string[]>? Errors = null,
    int? RetryAfterSeconds = null)
{
    public static ResultError Create(
        string code,
        string message,
        int statusCode,
        int? retryAfterSeconds = null) =>
        new(code, message, statusCode, RetryAfterSeconds: retryAfterSeconds);

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

    public static ResultError TooManyRequests(
        string message,
        int retryAfterSeconds,
        string code) =>
        new(code, message, StatusCodes.Status429TooManyRequests, RetryAfterSeconds: retryAfterSeconds);

    public static ResultError ServiceUnavailable(
        string message,
        string code = ErrorCodes.SmsUnavailable) =>
        new(code, message, StatusCodes.Status503ServiceUnavailable);

    public IActionResult ToActionResult() => new ApiErrorResult(this);
}
