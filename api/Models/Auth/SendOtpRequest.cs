using System.ComponentModel.DataAnnotations;

namespace Amanah.Api.Models.Auth;

public sealed class SendOtpRequest
{
    [Required(ErrorMessage = "Phone number is required.")]
    public string Phone { get; init; } = string.Empty;

    [Required(ErrorMessage = "CAPTCHA token is required.")]
    public string CaptchaToken { get; init; } = string.Empty;
}
