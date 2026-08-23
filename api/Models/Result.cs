using Microsoft.AspNetCore.Mvc;

namespace Amanah.Api.Models;

public readonly record struct Result<T>(T? Value, ResultError? Error)
{
    public bool IsSuccess => Error is null;

    public static Result<T> Ok(T value) => new(value, null);

    public static Result<T> Fail(ResultError error) => new(default, error);

    public static implicit operator Result<T>(T value) => Ok(value);

    public static implicit operator Result<T>(ResultError error) => Fail(error);

    public IActionResult ToActionResult() =>
        IsSuccess
            ? new OkObjectResult(Value)
            : new ObjectResult(new ApiError(Error!.Code, Error.Message, Error.Errors))
            {
                StatusCode = Error.StatusCode,
            };
}
