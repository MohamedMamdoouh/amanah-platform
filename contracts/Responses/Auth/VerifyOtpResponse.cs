namespace Amanah.Contracts.Responses.Auth;

public sealed class VerifyOtpResponse
{
    public required string Status { get; init; }

    public string? SignupToken { get; init; }

    public string? LoginToken { get; init; }
}
