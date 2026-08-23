namespace Amanah.Api.Models;

public record ApiError(
    string Code,
    string Message,
    Dictionary<string, string[]>? Errors = null);
