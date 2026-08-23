namespace Amanah.Api.Models;

public class ApiException : Exception
{
    public ApiException(
        string code,
        string message,
        int statusCode,
        Dictionary<string, string[]>? errors = null,
        int? retryAfterSeconds = null)
    {
        Code = code;
        Message = message;
        StatusCode = statusCode;
        Errors = errors;
        RetryAfterSeconds = retryAfterSeconds;
    }

    public string Code { get; }

    public override string Message { get; }

    public int StatusCode { get; }

    public Dictionary<string, string[]>? Errors { get; }

    public int? RetryAfterSeconds { get; }

    public ApiError ToApiError() => new(Code, Message, Errors);
}
