using Amanah.Api.Models.Common;
using Amanah.Api.Models.Errors;
using Microsoft.AspNetCore.Mvc;

namespace Amanah.Api.Models.Results;

public readonly record struct Result(ResultError? Error)
{
    public bool IsSuccess => Error is null;

    public static Result Ok() => new(null);

    public static implicit operator Result(ResultError error) => new(error);

    public IActionResult ToActionResult() =>
        IsSuccess
            ? new NoContentResult()
            : Error!.ToActionResult();
}

public readonly record struct Result<T>(T? Value, ResultError? Error)
{
    public bool IsSuccess => Error is null;

    public static Result<T> Ok(T value) => new(value, null);

    public static implicit operator Result<T>(T value) => Ok(value);

    public static implicit operator Result<T>(ResultError error) => new(default, error);

    public IActionResult ToActionResult() =>
        IsSuccess
            ? new OkObjectResult(Value)
            : Error!.ToActionResult();
}

public sealed class ApiErrorResult(ResultError error) : IActionResult
{
    public async Task ExecuteResultAsync(ActionContext context)
    {
        var response = context.HttpContext.Response;
        response.StatusCode = error.StatusCode;
        response.ContentType = "application/json";

        if (error.RetryAfterSeconds is int retryAfterSeconds)
        {
            response.Headers.RetryAfter = retryAfterSeconds.ToString();
        }

        await response.WriteAsJsonAsync(
            new ApiError(error.Code, error.Message, error.Errors),
            ApiJson.SerializerOptions);
    }
}
