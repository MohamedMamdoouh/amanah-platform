using Amanah.Api.Models.Auth;
using Amanah.Api.Services.Auth;
using FluentValidation;

namespace Amanah.Api.Validators.Auth;

public sealed class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    public LoginRequestValidator()
    {
        RuleFor(request => request.Phone)
            .NotEmpty()
            .WithMessage("Phone number is required.")
            .Must(phone => PhoneNormalizer.TryNormalize(phone, out _))
            .WithMessage("The phone number format is not accepted.");

        RuleFor(request => request.LoginToken)
            .NotEmpty()
            .WithMessage("Login token is required.");
    }
}
