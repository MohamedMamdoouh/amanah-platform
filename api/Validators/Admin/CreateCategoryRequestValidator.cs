using Amanah.Api.Utilities.Admin;
using Amanah.Contracts.Requests.Admin;
using FluentValidation;

namespace Amanah.Api.Validators.Admin;

public sealed class CreateCategoryRequestValidator : AbstractValidator<CreateCategoryRequest>
{
    public CreateCategoryRequestValidator()
    {
        RuleFor(request => request.Code)
            .NotEmpty()
            .WithMessage("Category code is required.")
            .MaximumLength(40)
            .WithMessage("Category code must be at most 40 characters.")
            .Matches(CategoryCatalogConstraints.CategoryCodePattern)
            .WithMessage("Category code must use lowercase letters, numbers, and hyphens.");

        RuleFor(request => request.SortOrder)
            .GreaterThanOrEqualTo(0)
            .WithMessage("Sort order must be zero or greater.");
    }
}
