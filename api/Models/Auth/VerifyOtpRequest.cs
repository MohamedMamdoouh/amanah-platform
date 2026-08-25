using System.ComponentModel.DataAnnotations;

namespace Amanah.Api.Models.Auth;

public sealed class VerifyOtpRequest
{
    [Required(ErrorMessage = "Phone number is required.")]
    public string Phone { get; init; } = string.Empty;

    [Required(ErrorMessage = "OTP code is required.")]
    public string Code { get; init; } = string.Empty;
}
