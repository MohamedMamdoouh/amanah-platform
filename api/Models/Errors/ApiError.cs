namespace Amanah.Api.Models.Errors;

public record ApiError(
    string Code,
    string Message,
    Dictionary<string, string[]>? Errors = null);
