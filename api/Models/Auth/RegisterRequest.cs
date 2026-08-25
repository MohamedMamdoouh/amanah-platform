namespace Amanah.Api.Models.Auth;

public sealed class RegisterRequest
{
    public string SignupToken { get; init; } = string.Empty;

    public string DisplayName { get; init; } = string.Empty;

    public bool AcceptTerms { get; init; }
}
