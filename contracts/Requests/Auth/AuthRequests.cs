namespace Amanah.Contracts.Requests.Auth;

public sealed class SendOtpRequest
{
    public string Channel { get; init; } = string.Empty;

    public string Identifier { get; init; } = string.Empty;

    public string CaptchaToken { get; init; } = string.Empty;

    public string Purpose { get; init; } = "signup";
}

public sealed class VerifyOtpRequest
{
    public string Channel { get; init; } = string.Empty;

    public string Identifier { get; init; } = string.Empty;

    public string Code { get; init; } = string.Empty;

    public string Purpose { get; init; } = "signup";
}

public sealed class RegisterRequest
{
    public string SignupToken { get; init; } = string.Empty;

    public string DisplayName { get; init; } = string.Empty;

    public string Password { get; init; } = string.Empty;

    public bool AcceptTerms { get; init; }
}

public sealed class LoginRequest
{
    public string Channel { get; init; } = string.Empty;

    public string Identifier { get; init; } = string.Empty;

    public string Password { get; init; } = string.Empty;
}

public sealed class ResetPasswordRequest
{
    public string ResetToken { get; init; } = string.Empty;

    public string Password { get; init; } = string.Empty;
}

public sealed class LogoutRequest
{
    public string RefreshToken { get; init; } = string.Empty;
}

public sealed class RefreshRequest
{
    public string RefreshToken { get; init; } = string.Empty;
}
