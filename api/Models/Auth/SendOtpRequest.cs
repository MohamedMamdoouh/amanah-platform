namespace Amanah.Api.Models.Auth;

public sealed class SendOtpRequest
{
    public string Phone { get; init; } = string.Empty;

    public string CaptchaToken { get; init; } = string.Empty;
}
