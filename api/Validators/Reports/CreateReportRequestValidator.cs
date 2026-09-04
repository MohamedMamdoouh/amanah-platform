using Amanah.Contracts.Requests.Reports;
using FluentValidation;

namespace Amanah.Api.Validators.Reports;

public sealed class CreateReportRequestValidator : AbstractValidator<CreateReportRequest>
{
    private static readonly string[] AllowedTypes = ["lost", "found"];

    public CreateReportRequestValidator()
    {
        RuleFor(request => request.Type)
            .NotEmpty()
            .WithMessage("Report type is required.")
            .Must(type => AllowedTypes.Contains(type, StringComparer.Ordinal))
            .WithMessage("Report type must be lost or found.");

        RuleFor(request => request.CategoryCode)
            .NotEmpty()
            .WithMessage("Category is required.");

        RuleFor(request => request.GovernorateCode)
            .NotEmpty()
            .WithMessage("Governorate is required.");
    }
}
