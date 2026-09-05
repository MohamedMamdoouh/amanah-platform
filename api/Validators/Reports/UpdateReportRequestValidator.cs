using Amanah.Contracts.Requests.Reports;
using FluentValidation;

namespace Amanah.Api.Validators.Reports;

public sealed class UpdateReportRequestValidator : AbstractValidator<UpdateReportRequest>
{
    public UpdateReportRequestValidator()
    {
        RuleFor(request => request.CategoryCode)
            .NotEmpty()
            .WithMessage("Category is required.");

        RuleFor(request => request.GovernorateCode)
            .NotEmpty()
            .WithMessage("Governorate is required.");
    }
}
