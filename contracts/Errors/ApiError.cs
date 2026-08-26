namespace Amanah.Contracts.Errors;

public record ApiError(
    string Code,
    string Message,
    Dictionary<string, string[]>? Errors = null);
